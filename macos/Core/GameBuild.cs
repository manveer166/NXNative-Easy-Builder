using System.Text.Json;
using System.Text.RegularExpressions;

namespace NXNativeEasyBuilder;

public sealed record BuildRequest(string Game, string? Keys, string? OutputFolder, bool Ipa, bool PrebuildGpu)
{
    private static readonly HashSet<string> PackageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".nsp", ".xci", ".nsz", ".xcz" };

    public string[] Arguments(Release release)
    {
        if (string.IsNullOrWhiteSpace(Game)) throw new IOException("Choose the game folder or a .nup file first.");
        var folder = Directory.Exists(Game);
        if (!folder && (!File.Exists(Game) || !Path.GetExtension(Game).Equals(".nup", StringComparison.OrdinalIgnoreCase)))
            throw new IOException("The game selection is missing. Choose its folder or .nup file again.");
        if (folder && Directory.EnumerateFiles(Game).Any(path => PackageExtensions.Contains(Path.GetExtension(path))) &&
            !File.Exists(Keys)) throw new IOException("Select your prod.keys file for this NSP / XCI game.");
        if (!string.IsNullOrEmpty(Keys) && !File.Exists(Keys)) throw new IOException("The selected prod.keys file is missing.");
        var workParent = folder ? Game : Path.GetDirectoryName(Game)!;
        CheckWritable(workParent);
        var args = new List<string> { "-u", release.Script, Game, "--platform", Ipa ? "ios" : "mac-app",
            "--progress", "json", "--keep-extracted" };
        if (!string.IsNullOrEmpty(Keys)) args.AddRange(["-k", Keys]);
        if (!PrebuildGpu) args.AddRange(["--gpu", "none"]);
        if (!string.IsNullOrWhiteSpace(OutputFolder))
        {
            CheckWritable(OutputFolder);
            var basename = folder ? Path.GetFileName(Path.TrimEndingDirectorySeparator(Game)) : Path.GetFileNameWithoutExtension(Game);
            var extension = Ipa ? ".ipa" : ".app";
            var output = Path.Combine(OutputFolder, (basename.Length == 0 ? "NXNative" : basename) + extension);
            var number = 2;
            while (File.Exists(output) || Directory.Exists(output))
                output = Path.Combine(OutputFolder, $"{basename}-{number++}{extension}");
            args.AddRange(["-o", output]);
        }
        return [.. args];
    }

    private static void CheckWritable(string folder)
    {
        if (!Directory.Exists(folder)) throw new IOException($"Folder is missing. {folder}");
        try
        {
            using var probe = new FileStream(Path.Combine(folder, ".nxnative-write-" + Guid.NewGuid().ToString("N")),
                FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"Choose a writable folder. {folder}", error);
        }
    }
}

public sealed class BuildProgress(Action<string> log, Action<string, double?> update)
{
    private int stages;
    public string? Artifact { get; private set; }
    public string? Error { get; private set; }

    public void Receive(string line)
    {
        if (!line.StartsWith('{')) { log(line); return; }
        try
        {
            using var json = JsonDocument.Parse(line);
            var data = json.RootElement;
            if (data.ValueKind != JsonValueKind.Object) { log(line); return; }
            string? Text(string key) => data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            int Integer(string key) => data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n) ? n : 0;
            long Count(string key) => data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var n) ? n : 0;
            var name = Text("name") ?? "Building";
            var step = Text("step");
            var label = string.IsNullOrWhiteSpace(step) ? name : name + " · " + step;
            var number = Integer("k");
            var position = stages > 0 && number > 0 ? (number - 1) / (double)stages : (double?)null;
            switch (Text("ev"))
            {
                case "stage":
                    stages = Math.Max(1, Integer("n"));
                    log($"[{number}/{stages}] {label}");
                    update(label, Math.Clamp((number - 1) / (double)stages, 0, 0.99));
                    break;
                case "step":
                    update(label, position);
                    break;
                case "progress":
                    if (position.HasValue && data.TryGetProperty("frac", out var fraction) && fraction.ValueKind == JsonValueKind.Number)
                        position = Math.Clamp(position.Value + fraction.GetDouble() / stages, 0, 0.99);
                    var done = Count("done");
                    var total = Count("total");
                    if (total > 0) label += $" · {done:N0}/{total:N0} {Text("unit")}";
                    update(label, position);
                    break;
                case "stage_end":
                    var good = data.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True;
                    log($"{(good ? "✓" : "✗")}  {name} · {Text("summary")}");
                    break;
                case "say":
                    var message = Text("msg") ?? "";
                    if (message.StartsWith("[RED] ")) Error = message[6..];
                    if (message.StartsWith("done: "))
                    {
                        var match = Regex.Match(message, @"^done: (.+\.(?:ipa|app)) \([0-9.]+ GB\)(?:;|$)");
                        if (match.Success) Artifact = match.Groups[1].Value;
                    }
                    log(message);
                    break;
                default:
                    log(line);
                    break;
            }
        }
        catch (JsonException) { log(line); }
    }
}

public static class GameBuild
{
    public static async Task<string> RunAsync(BuildRequest request, Release release, MacTools tools, string[] args,
        BuildProgress events, CancellationToken cancellation)
    {
        var result = await ProcessRunner.RunAsync(tools.Python, args, cancellation, events.Receive, release.Folder,
            tools.Environment(release));
        if (result.ExitCode != 0)
            throw new IOException(events.Error ?? $"NXNative stopped with exit code {result.ExitCode}. See the activity log.");
        var path = events.Artifact;
        var exists = path != null && (request.Ipa ? File.Exists(path) : File.Exists(Path.Combine(path, "Contents/Info.plist")));
        if (!exists) throw new IOException("NXNative exited without a verified output path. See its final log lines.");
        return path!;
    }
}
