using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace NXInstaller.Core;

public sealed record DownloadPolicy(TimeSpan? HeaderTimeout = null, TimeSpan? IdleTimeout = null,
    TimeSpan? TotalTimeout = null, TimeSpan? HeartbeatInterval = null, TimeSpan? RetryDelay = null, int MaxAttempts = 3);
public sealed record TransferProgress(string Phase, long Bytes, long? Total, int Attempt, TimeSpan Elapsed)
{
    public double? Fraction => Total is > 0 ? Math.Clamp((double)Bytes / Total.Value, 0, 1) : null;
    public string Detail => $"{Phase} · {Bytes / 1048576.0:F1} MiB" +
        (Total is > 0 ? $" / {Total.Value / 1048576.0:F1} MiB" : " · total size not provided") +
        $" · {Elapsed.TotalSeconds:F0}s · attempt {Attempt}";
}
public sealed class Downloads : IDisposable
{
    private readonly HttpClient http;
    private readonly DownloadPolicy policy;
    public Downloads(HttpMessageHandler? handler = null, DownloadPolicy? policy = null)
    {
        this.policy = policy ?? new();
        http = handler == null ? new HttpClient() : new HttpClient(handler);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("NXNativeWindows-EasyInstaller/2.0.2");
        http.Timeout = Timeout.InfiniteTimeSpan;
    }
    public async Task<JsonDocument?> JsonAsync(string url, CancellationToken ct, bool allow404 = false)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(policy.HeaderTimeout ?? TimeSpan.FromSeconds(60));
        try
        {
            using var response = await http.GetAsync(url, timeout.Token);
            if (allow404 && response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested)
        { throw new TimeoutException("GitHub did not respond within the request time limit. Check your connection and try again.", e); }
    }
    public async Task<string> FileAsync(string url, string target, string? sha256, Action<double>? progress,
        CancellationToken ct, Action<TransferProgress>? status = null)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https")
            throw new InvalidDataException("Downloads require HTTPS.");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (sha256 != null && File.Exists(target) && await HashMatches(target, sha256, ct)) return target;
        var partial = target + ".part";
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(policy.TotalTimeout ?? TimeSpan.FromMinutes(15));
        using var heartbeatStop = new CancellationTokenSource();
        var elapsed = Stopwatch.StartNew();
        var gate = new object();
        var current = new TransferProgress("Connecting to " + uri.Host, 0, null, 1, TimeSpan.Zero);
        TimeSpan last = TimeSpan.MinValue;
        void Publish(string phase, long bytes, long? total, int attempt, bool force = false)
        {
            TransferProgress snapshot;
            lock (gate)
            {
                current = new(phase, bytes, total, attempt, elapsed.Elapsed); snapshot = current;
                if (!force && last != TimeSpan.MinValue && elapsed.Elapsed - last < TimeSpan.FromMilliseconds(250)) return;
                last = elapsed.Elapsed;
            }
            status?.Invoke(snapshot);
            if (snapshot.Fraction is {} fraction) progress?.Invoke(fraction);
        }
        async Task Heartbeat()
        {
            using var timer = new PeriodicTimer(policy.HeartbeatInterval ?? TimeSpan.FromSeconds(5));
            try
            {
                while (await timer.WaitForNextTickAsync(heartbeatStop.Token))
                {
                    TransferProgress snapshot; lock (gate) snapshot = current with { Elapsed = elapsed.Elapsed };
                    status?.Invoke(snapshot);
                }
            }
            catch (OperationCanceledException) when (heartbeatStop.IsCancellationRequested) { }
        }
        var heartbeat = status == null ? Task.CompletedTask : Heartbeat();
        try
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    budget.Token.ThrowIfCancellationRequested();
                    Publish("Connecting to " + uri.Host, 0, null, attempt, true);
                    using var request = CancellationTokenSource.CreateLinkedTokenSource(budget.Token);
                    request.CancelAfter(policy.HeaderTimeout ?? TimeSpan.FromSeconds(60));
                    using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, request.Token);
                    request.CancelAfter(Timeout.InfiniteTimeSpan);
                    response.EnsureSuccessStatusCode();
                    if (response.RequestMessage?.RequestUri?.Scheme != "https") throw new InvalidDataException("Insecure download redirect.");
                    var expected = response.Content.Headers.ContentLength;
                    if (expected is > 4L * 1024 * 1024 * 1024) throw new InvalidDataException("Download exceeds the 4 GiB safety limit.");
                    await using var src = await response.Content.ReadAsStreamAsync(budget.Token);
                    long done = 0;
                    Publish("Downloading", done, expected, attempt, true);
                    await using (var dst = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 131072, true))
                    {
                        var buffer = new byte[131072];
                        while (true)
                        {
                            using var stall = CancellationTokenSource.CreateLinkedTokenSource(budget.Token);
                            var idle = policy.IdleTimeout ?? TimeSpan.FromSeconds(45); stall.CancelAfter(idle);
                            int n;
                            try { n = await src.ReadAsync(buffer, stall.Token); }
                            catch (OperationCanceledException e) when (!budget.IsCancellationRequested)
                            { throw new IOException($"Download stalled: no data for {idle.TotalSeconds:F0} seconds.", e); }
                            if (n == 0) break;
                            await dst.WriteAsync(buffer.AsMemory(0, n), budget.Token); done += n;
                            if (done > 4L * 1024 * 1024 * 1024) throw new InvalidDataException("Download exceeds the 4 GiB safety limit.");
                            Publish("Downloading", done, expected, attempt);
                        }
                        if (expected != null && done != expected) throw new IOException("Incomplete download.");
                    }
                    Publish("Download complete; validating", done, expected, attempt, true);
                    if (sha256 != null && !await HashMatches(partial, sha256, budget.Token)) throw new InvalidDataException("SHA-256 mismatch. The download will not be used.");
                    File.Move(partial, target, true); progress?.Invoke(1); return target;
                }
                catch (Exception e) when (!budget.IsCancellationRequested &&
                    e is HttpRequestException or IOException or OperationCanceledException)
                {
                    if (attempt >= policy.MaxAttempts) throw new IOException($"Download failed after {attempt} attempt(s): {e.Message} Check access to {uri.Host}, then run Install / Repair again.", e);
                    Publish("Retrying after " + e.Message, 0, null, attempt, true);
                    await Task.Delay(policy.RetryDelay ?? TimeSpan.FromSeconds(attempt * 2 - 1), budget.Token);
                }
                finally { if (File.Exists(partial)) File.Delete(partial); }
            }
        }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested && budget.IsCancellationRequested)
        { throw new TimeoutException("Download exceeded the total time limit. Your previous repository has been retained; run Install / Repair to retry.", e); }
        finally { heartbeatStop.Cancel(); await heartbeat; }
    }
    public static async Task<bool> HashMatches(string file, string hash, CancellationToken ct)
    {
        await using var stream = File.OpenRead(file);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).Equals(hash.Replace("sha256:", ""), StringComparison.OrdinalIgnoreCase);
    }
    public void Dispose() => http.Dispose();
}

public sealed record ExtractionProgress(int Files, int TotalFiles, long Bytes, long TotalBytes)
{
    public double Fraction => TotalBytes > 0 ? Math.Clamp((double)Bytes / TotalBytes, 0, 1) : (double)Files / Math.Max(1, TotalFiles);
    public string Detail => $"Extracting repository · {Files:N0}/{TotalFiles:N0} files · {Bytes / 1048576.0:F1}/{TotalBytes / 1048576.0:F1} MiB";
}
public static class SafeZip
{
    internal static string SafeName(ZipArchiveEntry entry)
    {
        var name = entry.FullName.Replace('\\', '/');
        if (name.StartsWith('/') || name.Contains(':') || name.Split('/').Any(p => p is ".." or ".") ||
            name.Split('/').Any(p => p.TrimEnd(' ', '.') != p) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
            throw new InvalidDataException("Unsafe archive path or symbolic link: " + entry.FullName);
        return name;
    }
    public static void Extract(string zip, string directory, CancellationToken ct, long maxBytes = 3L * 1024 * 1024 * 1024,
        Action<ExtractionProgress>? progress = null)
    {
        Directory.CreateDirectory(directory);
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(zip);
        if (archive.Entries.Count > 100000) throw new InvalidDataException("Archive has too many entries.");
        long total = 0, done = 0; int files = 0;
        foreach (var entry in archive.Entries)
        { SafeName(entry); total = checked(total + entry.Length); if (total > maxBytes) throw new InvalidDataException("Archive exceeds unpacked size limit."); }
        int count = archive.Entries.Count(e => !e.FullName.EndsWith('/'));
        var watch = Stopwatch.StartNew(); TimeSpan last = TimeSpan.MinValue;
        void Report(bool force = false)
        { if (force || last == TimeSpan.MinValue || watch.Elapsed - last > TimeSpan.FromMilliseconds(250)) { last = watch.Elapsed; progress?.Invoke(new(files, count, done, total)); } }
        Report(true);
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested(); var name = SafeName(entry);
            var target = Path.GetFullPath(Path.Combine(directory, name));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Archive path escapes extraction folder.");
            if (name.EndsWith('/')) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var source = entry.Open(); using var dest = new FileStream(target, FileMode.CreateNew);
            var buffer = new byte[131072]; long copied = 0; int n;
            while ((n = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested(); copied += n; done += n;
                if (copied > entry.Length) throw new InvalidDataException("Invalid archive entry length.");
                dest.Write(buffer, 0, n); Report();
            }
            if (copied != entry.Length) throw new InvalidDataException("Truncated archive entry.");
            files++; Report();
        }
        Report(true);
    }
}
