using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NXNativeEasyBuilder;

public sealed record Release(string Commit, string Folder, string XcodeVersion)
{
    public string Script => Path.Combine(Folder, "scripts/build_ipa_from_nsp.py");
    public string ShaderTools => Path.Combine(Folder, "dm/gfx/tools");
    public string SpirvCross => Path.Combine(ShaderTools, "spirv_cross_bin/spirv-cross");
    public string ShortCommit => Commit[..8];
}

public sealed record CommitInfo(string Sha, string Subject);

public sealed class Repository(string root, HttpClient http)
{
    private string Releases => Path.Combine(root, "Releases");
    private string Pointer => Path.Combine(root, "active-commit.txt");

    public Release? Active()
    {
        if (!File.Exists(Pointer)) return null;
        var sha = File.ReadAllText(Pointer).Trim();
        return IsSha(sha) ? Read(sha, Path.Combine(Releases, sha)) : null;
    }

    public async Task<CommitInfo> LatestAsync(CancellationToken cancellation)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        stop.CancelAfter(TimeSpan.FromSeconds(25));
        try
        {
            using var response = await http.GetAsync("https://api.github.com/repos/seedds/nxnative/commits/main", stop.Token);
            response.EnsureSuccessStatusCode();
            using var data = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(stop.Token));
            var sha = data.RootElement.GetProperty("sha").GetString();
            if (sha == null || !IsSha(sha)) throw new IOException("GitHub did not return a valid commit SHA.");
            var subject = data.RootElement.GetProperty("commit").GetProperty("message").GetString()?.Split('\n')[0] ?? "";
            return new CommitInfo(sha, subject);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            throw new TimeoutException("GitHub did not respond in time.");
        }
    }

    public async Task<Release> StageAsync(CommitInfo commit, Action<string> log, Action<double?> progress,
        CancellationToken cancellation)
    {
        if (!IsSha(commit.Sha)) throw new ArgumentException("Invalid repository commit.", nameof(commit));
        var target = Path.Combine(Releases, commit.Sha);
        if (Read(commit.Sha, target) is { } existing && FingerprintsMatch(existing))
        {
            log($"Repository {existing.ShortCommit} is already downloaded.");
            return existing;
        }

        Directory.CreateDirectory(Releases);
        var stage = Path.Combine(Releases, ".download-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            var archive = Path.Combine(stage, "source.tar.gz");
            log($"Downloading NXNative {commit.Sha[..8]} from GitHub.");
            await DownloadAsync(http, $"https://codeload.github.com/seedds/nxnative/tar.gz/{commit.Sha}",
                archive, progress, cancellation, log);
            log("Extracting the repository and checking its shader tools.");
            progress(null);
            await ExtractAsync(archive, stage, cancellation);
            var extracted = Path.Combine(stage, "nxnative-" + commit.Sha);
            var release = Read(commit.Sha, extracted);
            if (release == null || !FingerprintsMatch(release))
                throw new IOException("The repository archive is incomplete or its shader tools do not match its lock.");
            cancellation.ThrowIfCancellationRequested();
            if (Directory.Exists(target)) Directory.Move(target, target + ".backup-" + Guid.NewGuid().ToString("N"));
            Directory.Move(extracted, target);
            return release with { Folder = target };
        }
        finally { Directory.Delete(stage, recursive: true); }
    }

    public void Activate(Release release)
    {
        Directory.CreateDirectory(root);
        var temp = Pointer + "." + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temp, release.Commit + "\n");
            File.Move(temp, Pointer, overwrite: true);
        }
        finally { File.Delete(temp); }
    }

    public static bool FingerprintsMatch(Release release)
    {
        var tools = release.ShaderTools;
        var expected = File.ReadLines(Path.Combine(tools, "dmsh_chain.lock"))
            .Where(line => !line.StartsWith('#') && line.Contains('\t'))
            .Select(line => line.Split('\t', 2))
            .ToDictionary(parts => parts[0], parts => parts[1]);
        var files = new Dictionary<string, string> { ["spirv-cross"] = release.SpirvCross };
        foreach (var dll in new[] { "dmsh_xlate.dll", "Ryujinx.Graphics.Shader.dll", "Ryujinx.Common.dll" })
            files[dll] = Path.Combine(tools, "dmsh_xlate_bin", dll);
        foreach (var (name, path) in files)
        {
            if (!File.Exists(path) || !expected.TryGetValue(name, out var sha)) return false;
            using var stream = File.OpenRead(path);
            if (!Convert.ToHexString(SHA256.HashData(stream))[..16].Equals(sha, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    private static Release? Read(string sha, string folder)
    {
        var tools = Path.Combine(folder, "dm/gfx/tools");
        var compilers = Path.Combine(tools, "own_compilers.py");
        if (!File.Exists(Path.Combine(folder, "scripts/build_ipa_from_nsp.py")) || !File.Exists(compilers) ||
            !File.Exists(Path.Combine(tools, "dmsh_chain.lock"))) return null;
        var version = Regex.Match(File.ReadAllText(compilers), "(?m)^XCODE_VERSION\\s*=\\s*['\"]([0-9.]+)['\"]");
        return version.Success ? new Release(sha, folder, version.Groups[1].Value) : null;
    }

    private static bool IsSha(string value) => value.Length == 40 && value.All(Uri.IsHexDigit);

    public static async Task DownloadAsync(HttpClient http, string url, string destination,
        Action<double?> progress, CancellationToken cancellation, Action<string>? log = null)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation);
        response.EnsureSuccessStatusCode();
        var length = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(cancellation);
        await using var output = File.Create(destination);
        var buffer = new byte[256 * 1024];
        long received = 0;
        var last = DateTimeOffset.MinValue;
        var lastReport = DateTimeOffset.UtcNow;
        progress(null);
        int count;
        while ((count = await source.ReadAsync(buffer, cancellation).AsTask().WaitAsync(TimeSpan.FromSeconds(40), cancellation)) != 0)
        {
            received += count;
            if (received > 2L * 1024 * 1024 * 1024) throw new IOException("Download exceeded 2 GB.");
            await output.WriteAsync(buffer.AsMemory(0, count), cancellation);
            if (length > 0 && DateTimeOffset.UtcNow - last > TimeSpan.FromMilliseconds(250))
            {
                progress(Math.Clamp(received / (double)length.Value, 0, 1));
                last = DateTimeOffset.UtcNow;
            }
            if (DateTimeOffset.UtcNow - lastReport > TimeSpan.FromSeconds(5))
            {
                log?.Invoke($"Repository download · {received / 1048576.0:N1} MiB received.");
                lastReport = DateTimeOffset.UtcNow;
            }
        }
        if (length.HasValue && received != length.Value) throw new IOException("The download was incomplete.");
    }

    public static async Task ExtractAsync(string archive, string folder, CancellationToken cancellation)
    {
        await using var input = File.OpenRead(archive);
        await using var decompressed = new GZipStream(input, CompressionMode.Decompress);
        await TarFile.ExtractToDirectoryAsync(decompressed, folder, overwriteFiles: false, cancellation);
    }
}
