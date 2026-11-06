using System.Runtime.InteropServices;
using System.Text.Json;

namespace NXInstaller.Core;

public enum InstallMode { InstallRepair, Update }
public enum StepStatus { Pending, Running, Done, ActionRequired, Failed, Cancelled }
public record InstallEvent(string Id, string Title, StepStatus Status, string Detail, double? Fraction = null);
public record CommandResult(int ExitCode, string Output);
public record CommandSpec(string File, IReadOnlyList<string> Args, string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Environment = null, TimeSpan? Timeout = null);
public interface ICommandRunner { Task<CommandResult> RunAsync(CommandSpec command, Action<string>? log, CancellationToken ct); }
public record Compatibility(bool Allowed, bool Experimental, string Detail);
public static class HostPolicy
{
    public static Compatibility Evaluate(bool windows, Architecture arch, int build) => (windows, arch, build) switch
    {
        (false, _, _) => new(false, false, "This installer requires Windows 10/11."),
        (_, Architecture.X64, >= 19041) => new(true, false, "Windows x64 · toolchain target x64"),
        (_, Architecture.Arm64, >= 22000) => new(true, true, "Windows ARM64 · experimental x64 toolchain under emulation; every executable is checked"),
        (_, Architecture.Arm64, _) => new(false, true, "ARM64 requires Windows 11 with x64 emulation. Windows 10 ARM64 is blocked."),
        (_, Architecture.X64, _) => new(false, false, "Windows 10 version 2004 (build 19041) or newer is required."),
        _ => new(false, false, "Only x64 and Windows 11 ARM64 hosts are supported.")
    };
    public static Compatibility Current => Evaluate(OperatingSystem.IsWindows(), RuntimeInformation.OSArchitecture, Environment.OSVersion.Version.Build);
}

public sealed class AppleSelection
{
    public const string AgreementUrl = "https://www.apple.com/legal/sla/docs/xcode.pdf";
    public string SdkFolder { get; set; } = "";
    public string XcodeXip { get; set; } = "";
    public string MetalFolder { get; set; } = "";
    public bool MetalLicenseAccepted { get; set; }
    public bool HasWindowsSdkAuthorization { get; set; }
    public DateTimeOffset? AuthorizationAttestedAt { get; set; }
    public bool CanUseSdk => HasWindowsSdkAuthorization && AuthorizationAttestedAt != null;
}
public sealed class InstallState
{
    public int Schema { get; set; } = 1;
    public string Repository { get; set; } = "";
    public string Commit { get; set; } = "";
    public string Python { get; set; } = "";
    public Dictionary<string, string> Toolchain { get; set; } = new();
    public AppleSelection Apple { get; set; } = new();
    public bool Ready { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public bool ExperimentalArm64 { get; set; }
}
public sealed record InstallOptions(string Root, InstallMode Mode, AppleSelection Apple);
public sealed record HealthCheck(string Name, bool Ok, string Detail, bool Required = true);
public sealed record RunSummary(bool Ready, string Message, IReadOnlyList<HealthCheck> Checks);
public static class StateStore
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static string ValidateRoot(string root)
    {
        var p = Path.GetFullPath(root.Trim());
        if (p == Path.GetPathRoot(p) || p.StartsWith(@"\\", StringComparison.Ordinal))
            throw new InvalidOperationException("Choose a local installation folder, not a drive root or network share.");
        if (OperatingSystem.IsWindows())
        {
            var forbidden = new[] { Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) };
            if (forbidden.Where(x => x.Length > 0).Any(x => p.Equals(x, StringComparison.OrdinalIgnoreCase) ||
                p.StartsWith(x.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Choose a writable personal folder outside Windows and Program Files.");
        }
        return p;
    }
    public static InstallState Load(string root)
    {
        var p = Path.Combine(root, "installer-state.json");
        if (!File.Exists(p)) return new();
        return JsonSerializer.Deserialize<InstallState>(File.ReadAllText(p), Json) ?? throw new InvalidDataException("Installation state is empty.");
    }
    public static void Save(string root, InstallState state) => AtomicJson(Path.Combine(root, "installer-state.json"), state);
    public static void AtomicJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(value, Json)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
