using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NXInstaller.Core;

public sealed record RepositoryVersion(string Commit, string Branch, string Message, DateTimeOffset? PublishedAt);
public sealed record RepositoryProgress(string Detail, double? Fraction);
public sealed record RepositoryFile(string Path, long Bytes, string Sha256);
public sealed record RepositoryManifest(string Commit, string ArchiveSha256, List<RepositoryFile> Files);

public sealed class RepositoryInstaller(Downloads downloads)
{
    public const string Api = "https://api.github.com/repos/zenvyzenshiro/NXNativeWindows";
    private static readonly string[] RequiredFiles = ["win/doctor.py", "win/gui.pyw", "win/toolchain.py", "win/sdk/extract_xip.py", "dm/gfx/tools/dmsh_xlate_bin/dmsh_xlate.dll"];
    private static bool Mutable(string path) => path is "win/gui_settings.json" or "win/toolchain.json";
    public async Task<RepositoryVersion> CheckLatestAsync(CancellationToken ct)
    {
        using var repo = await downloads.JsonAsync(Api, ct);
        var reference = repo!.RootElement.GetProperty("default_branch").GetString()!;
        using var commit = await downloads.JsonAsync(Api + "/commits/" + Uri.EscapeDataString(reference), ct);
        var sha = commit!.RootElement.GetProperty("sha").GetString()!;
        if (!Regex.IsMatch(sha, "^[a-f0-9]{40}$")) throw new InvalidDataException("Invalid GitHub commit ID.");
        var metadata = commit.RootElement.GetProperty("commit");
        return new(sha, reference, metadata.GetProperty("message").GetString()!.Split('\n')[0],
            metadata.GetProperty("committer").GetProperty("date").GetDateTimeOffset());
    }
    public async Task<(string Directory, string Commit)> FetchAsync(string root, RepositoryVersion version,
        Action<string> log, Action<double> progress, CancellationToken ct, Action<RepositoryProgress>? status = null)
    {
        var sha = version.Commit;
        if (!Regex.IsMatch(sha, "^[a-f0-9]{40}$")) throw new InvalidDataException("Invalid GitHub commit ID.");
        var target = Path.Combine(root, "releases", sha);
        var zip = Path.Combine(root, "downloads", sha + ".zip");
        var manifestPath = Path.Combine(root, "releases", sha + ".integrity.json");
        var clock = Stopwatch.StartNew(); TimeSpan lastLog = TimeSpan.Zero;
        var reporting = new object();
        void Report(string detail, double? fraction, bool forceLog = false)
        {
            lock (reporting)
            {
                status?.Invoke(new(detail, fraction)); if (fraction is {} f) progress(f);
                if (forceLog || clock.Elapsed - lastLog >= TimeSpan.FromSeconds(5))
                { lastLog = clock.Elapsed; log(detail); }
            }
        }
        Report($"GitHub branch {version.Branch}; pinned commit {sha}", 0, true);
        RepositoryManifest? manifest = LoadManifest(manifestPath, sha);
        // Legacy v1/v2 installations have an archive but no integrity manifest. Migrate it once.
        if (manifest == null && File.Exists(zip))
        {
            Report("Checking the existing repository archive; no download needed if it is usable.", 0, true);
            try { manifest = await Task.Run(() => ReadArchive(zip, sha, ct, (s, f) => Report(s, .2 * f)), ct); }
            catch (Exception e) when (e is IOException or InvalidDataException)
            { Report("Existing archive is incomplete or invalid: " + e.Message + " A fresh copy will be downloaded.", 0, true); }
        }
        if (manifest != null && Directory.Exists(target))
        {
            Report($"Checking {manifest.Files.Count:N0} local source files before reusing commit {sha[..8]}…", 0, true);
            bool valid = await Task.Run(() => VerifyLocal(target, manifest, ct, (s, f) => Report(s, .2 * f)), ct);
            if (valid)
            {
                StateStore.AtomicJson(manifestPath, manifest);
                Report("Repository source verified and already current; reusing it without download or extraction.", 1, true);
                return (target, sha);
            }
            Report("Missing or changed source detected. Repairing while preserving builder settings.", .2, true);
        }
        bool cacheOk = false;
        if (manifest != null && File.Exists(zip))
        {
            Report("Validating the cached ZIP checksum before extraction…", .2, true);
            cacheOk = await Downloads.HashMatches(zip, manifest.ArchiveSha256, ct);
        }
        if (!cacheOk)
        {
            Report("Downloading the pinned GitHub archive. Bytes and connection status will appear here every 5 seconds.", 0, true);
            await downloads.FileAsync("https://codeload.github.com/zenvyzenshiro/NXNativeWindows/zip/" + sha,
                zip, null, null, ct, transfer => Report(transfer.Detail, transfer.Fraction is {} f ? .5 * f : null,
                    transfer.Phase.StartsWith("Connecting") || transfer.Phase.StartsWith("Retrying") || transfer.Phase.StartsWith("Download complete")));
            Report("Download finished. Checking archive layout and source checksums…", .5, true);
            manifest = await Task.Run(() => ReadArchive(zip, sha, ct, (s, f) => Report(s, .5 + .1 * f)), ct);
        }
        else Report("Cached archive verified; repairing from disk without downloading again.", .6, true);

        var staging = Path.Combine(root, "work", "repository-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            Report("Extracting repository into a temporary folder. Your previous checkout remains in place.", .6, true);
            await Task.Run(() => SafeZip.Extract(zip, staging, ct, progress: p => Report(p.Detail, .6 + .35 * p.Fraction)), ct);
            var tree = Directory.GetDirectories(staging).Single();
            foreach (var f in RequiredFiles)
                if (!File.Exists(Path.Combine(tree, f))) throw new InvalidDataException("Upstream layout changed: missing " + f);
            ct.ThrowIfCancellationRequested();
            var settingsSource = Directory.Exists(target) ? target : StateStore.Load(root).Repository;
            foreach (var f in new[] { "win/gui_settings.json", "win/toolchain.json" })
                if (!string.IsNullOrEmpty(settingsSource) && File.Exists(Path.Combine(settingsSource, f)))
                    File.Copy(Path.Combine(settingsSource, f), Path.Combine(tree, f), true);
            Report("Extraction complete. Activating the verified repository…", .98, true);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (Directory.Exists(target))
            {
                var backup = target + ".backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N")[..6];
                Directory.Move(target, backup);
                try { Directory.Move(tree, target); } catch { Directory.Move(backup, target); throw; }
                log("Previous checkout retained at " + backup);
            }
            else Directory.Move(tree, target);
            StateStore.AtomicJson(manifestPath, manifest!);
            Report("Repository ready · " + sha[..8], 1, true);
            return (target, sha);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }
    private static RepositoryManifest? LoadManifest(string path, string sha)
    {
        try
        {
            var value = JsonSerializer.Deserialize<RepositoryManifest>(File.ReadAllText(path));
            if (value == null || value.Commit != sha || value.ArchiveSha256 == null || !Regex.IsMatch(value.ArchiveSha256, "^[A-Fa-f0-9]{64}$") || value.Files == null || value.Files.Count is < 1 or > 100000) return null;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in value.Files)
                if (file == null || file.Bytes < 0 || file.Sha256 == null || !Regex.IsMatch(file.Sha256, "^[A-Fa-f0-9]{64}$") || file.Path == null || !SafeRelative(file.Path) || !names.Add(file.Path)) return null;
            return RequiredFiles.All(names.Contains) ? value : null;
        }
        catch (Exception e) when (e is IOException or JsonException or ArgumentException) { return null; }
    }
    private static bool SafeRelative(string path) => path.Length > 0 && !path.StartsWith('/') && !path.Contains('\\') && !path.Contains(':') &&
        path.Split('/').All(s => s.Length > 0 && s is not "." and not ".." && s.TrimEnd(' ', '.') == s);
    private static RepositoryManifest ReadArchive(string zip, string sha, CancellationToken ct, Action<string, double> progress)
    {
        using var archive = ZipFile.OpenRead(zip);
        if (archive.Entries.Count is < 1 or > 100000) throw new InvalidDataException("Invalid repository archive entry count.");
        long total = 0;
        var entries = new List<(ZipArchiveEntry Entry, string Path)>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); string? prefix = null;
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested(); var name = SafeZip.SafeName(entry);
            total = checked(total + entry.Length); if (total > 3L * 1024 * 1024 * 1024) throw new InvalidDataException("Repository archive exceeds unpacked size limit.");
            if (name.EndsWith('/')) continue;
            int slash = name.IndexOf('/');
            if (slash < 1) throw new InvalidDataException("Repository ZIP must contain a single top-level folder.");
            prefix ??= name[..slash]; if (prefix != name[..slash]) throw new InvalidDataException("Repository ZIP contains multiple root folders.");
            var relative = name[(slash + 1)..];
            if (!SafeRelative(relative) || !names.Add(relative)) throw new InvalidDataException("Invalid or duplicate repository path: " + relative);
            entries.Add((entry, relative));
        }
        foreach (var f in RequiredFiles) if (!names.Contains(f)) throw new InvalidDataException("Upstream layout changed: missing " + f);
        var files = new List<RepositoryFile>(); long bytes = 0; int count = 0;
        var watch = Stopwatch.StartNew(); TimeSpan last = TimeSpan.Zero;
        foreach (var (entry, path) in entries)
        {
            ct.ThrowIfCancellationRequested();
            using var stream = entry.Open(); using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[131072]; long copied = 0; int n;
            while ((n = stream.Read(buffer)) > 0)
            {
                ct.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, n); bytes += n; copied += n;
                if (copied > entry.Length) throw new InvalidDataException("Invalid ZIP entry length.");
                if (watch.Elapsed - last >= TimeSpan.FromSeconds(1))
                { last = watch.Elapsed; progress($"Checking archive · {count:N0}/{entries.Count:N0} files · {bytes / 1048576.0:F1} MiB", (double)bytes / Math.Max(1, total)); }
            }
            if (copied != entry.Length) throw new InvalidDataException("Truncated ZIP entry.");
            if (!Mutable(path)) files.Add(new(path, entry.Length, Convert.ToHexString(hash.GetHashAndReset())));
            count++;
        }
        using var zipStream = File.OpenRead(zip);
        string archiveHash = Convert.ToHexString(SHA256.HashData(zipStream));
        progress($"Archive validated · {count:N0} files", 1);
        return new(sha, archiveHash, files);
    }
    private static bool VerifyLocal(string directory, RepositoryManifest manifest, CancellationToken ct, Action<string, double> progress)
    {
        int done = 0; var watch = Stopwatch.StartNew(); TimeSpan last = TimeSpan.Zero;
        foreach (var file in manifest.Files)
        {
            ct.ThrowIfCancellationRequested(); var path = Path.Combine(directory, file.Path);
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length != file.Bytes) return false;
                using var stream = File.OpenRead(path); using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[131072]; int n;
                while ((n = stream.Read(buffer)) > 0)
                { ct.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, n); }
                if (!Convert.ToHexString(hash.GetHashAndReset()).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase)) return false;
            }
            catch (IOException) { return false; }
            done++;
            if (watch.Elapsed - last >= TimeSpan.FromSeconds(1))
            { last = watch.Elapsed; progress($"Checking local source · {done:N0}/{manifest.Files.Count:N0} files", (double)done / manifest.Files.Count); }
        }
        progress($"Local source checksums match · {done:N0} files", 1); return true;
    }
}
