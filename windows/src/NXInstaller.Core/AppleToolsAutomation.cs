using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace NXInstaller.Core;

public sealed class AppleToolsAutomation(string root, ICommandRunner runner, Action<string> log)
{
    public const string OfficialMetalUrl = "https://developer.apple.com/download/all/?q=metal%20developer%20tools%20for%20windows";
    public static bool ValidSdkRoot(string? folder)
    {
        if(string.IsNullOrWhiteSpace(folder)||!Directory.Exists(folder))return false;
        if(!File.Exists(Path.Combine(folder,"System","Library","Frameworks","Foundation.framework","Headers","Foundation.h"))||
           !File.Exists(Path.Combine(folder,"System","Library","Frameworks","UIKit.framework","Headers","UIKit.h")))return false;
        try
        {
            using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"SDKSettings.json")));
            var version=json.RootElement.GetProperty("Version").GetString();
            return int.TryParse(version?.Split('.')[0],out int major)&&major>=26;
        }
        catch(Exception e)when(e is IOException or JsonException or KeyNotFoundException or InvalidOperationException){return false;}
    }
    public static string? FindSdkUnder(string folder)
    {
        if(!Directory.Exists(folder))return null;
        if(ValidSdkRoot(folder))return folder;
        // A common old selection was the repository root, not the SDK itself.
        var direct=new[]{Path.Combine(folder,"win","sdk","out"),Path.Combine(folder,"sdk"),folder};
        foreach(var basePath in direct.Where(Directory.Exists))
        {
            var pending=new Queue<(string Path,int Depth)>();pending.Enqueue((basePath,0));int visited=0;
            while(pending.Count>0&&visited++<1500)
            {
                var(current,depth)=pending.Dequeue();if(depth>4)continue;
                foreach(var child in Directory.EnumerateDirectories(current))
                {
                    if(SdkExtractor.IsStagingDirectory(child))continue;
                    if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0)continue;
                    if(Path.GetFileName(child).StartsWith("iPhoneOS",StringComparison.OrdinalIgnoreCase)&&ValidSdkRoot(child))return child;
                    pending.Enqueue((child,depth+1));
                }
            }
        }
        return null;
    }
    public static bool ValidMetalRoot(string? folder) => !string.IsNullOrEmpty(folder) && Directory.Exists(folder) &&
        new[] { "ios", "macos" }.All(target => ToolLocator.MetalExe(folder, target, "metal") != null && ToolLocator.MetalExe(folder, target, "metallib") != null);
    public static bool IsMetalPackage(string path)
    {
        var name = Path.GetFileName(path); var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".exe" or ".msi" or ".zip" && name.Contains("metal", StringComparison.OrdinalIgnoreCase) &&
            (name.Contains("tool", StringComparison.OrdinalIgnoreCase) || name.Contains("developer", StringComparison.OrdinalIgnoreCase));
    }
    public static IEnumerable<string> SearchDirectories(string? xip)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new[] { string.IsNullOrEmpty(xip) ? null : Path.GetDirectoryName(xip), Path.Combine(home, "Downloads"),
            Path.Combine(Path.GetPathRoot(home) ?? @"C:\", "Downloads"), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) }
            .Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p)).Select(p => p!).Distinct(StringComparer.OrdinalIgnoreCase);
    }
    public static string? FindPackage(IEnumerable<string> folders)
    {
        var candidates = new List<string>();
        foreach (var folder in folders)
        {
            try { candidates.AddRange(Directory.EnumerateFiles(folder).Where(IsMetalPackage)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return candidates.OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }
    public static string? FindMetalUnder(string folder)
    {
        if (!Directory.Exists(folder)) return null;
        if (ValidMetalRoot(folder)) return folder;
        var pending = new Queue<(string Path, int Depth)>(); pending.Enqueue((folder, 0)); int visited = 0;
        while (pending.Count > 0 && visited++ < 3000)
        {
            var (current, depth) = pending.Dequeue();
            if (depth > 6) continue;
            foreach (var child in Directory.EnumerateDirectories(current))
            {
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                if (ValidMetalRoot(child)) return child;
                pending.Enqueue((child, depth + 1));
            }
        }
        return null;
    }
    public void Detect(AppleSelection selection, string? repository = null)
    {
        if (!ValidMetalRoot(selection.MetalFolder))
        {
            string store = Path.Combine(root, "apple", "metal");
            var cached = Directory.Exists(store) ? Directory.GetDirectories(store).Where(p=>File.Exists(Path.Combine(p,"apple-signature-verified.json"))).Select(FindMetalUnder).FirstOrDefault(p=>p!=null) : null;
            selection.MetalFolder = ToolLocator.MetalRoot() is { } known && ValidMetalRoot(known) ? known : cached ?? "";
        }
        if (!SdkExtractor.UsableSdk(selection.SdkFolder, root))
        {
            var sdkBase = Path.Combine(root, "sdk");
            var previous = selection.SdkFolder;
            if(ValidSdkRoot(previous)) { log("Previous SDK extraction has no verified completion; retrying in a clean staging folder."); previous = ""; }
            var bases = new[] { previous, sdkBase, repository == null ? "" : Path.Combine(repository, "win", "sdk", "out") };
            selection.SdkFolder = bases.Where(Directory.Exists).Select(FindSdkUnder).FirstOrDefault(p=>p!=null) ?? "";
        }
    }
    public async Task<bool> PrepareMetalAsync(AppleSelection selection, CancellationToken ct)
    {
        Detect(selection);
        if (ValidMetalRoot(selection.MetalFolder)) { log("Metal tools detected automatically: " + selection.MetalFolder); return true; }
        var package = FindPackage(SearchDirectories(selection.XcodeXip));
        if (package == null) return false;
        log("Found downloaded Metal package: " + package);
        async Task VerifyAppleSignature(string file)
        {
            var script = "$s=Get-AuthenticodeSignature -LiteralPath " + PowerShell.Literal(file) +
                "; if($s.Status -ne 'Valid' -or $s.SignerCertificate.Subject -notmatch 'O=Apple Inc') { throw 'A valid Apple signature is required.' }; $s.SignerCertificate.Subject";
            var result = await runner.RunAsync(PowerShell.Command(script, TimeSpan.FromMinutes(1)), log, ct);
            if (result.ExitCode != 0) throw new IOException("Apple signature validation failed for " + Path.GetFileName(file) + ". Use the official Apple download.");
        }
        if (Path.GetExtension(package).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            if (!selection.MetalLicenseAccepted) throw new InvalidOperationException("Confirm the applicable official Metal package license in Apple Setup before using the downloaded ZIP.");
            await using var stream = File.OpenRead(package); var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
            var target = Path.Combine(root, "apple", "metal", hash);
            if (!File.Exists(Path.Combine(target,"apple-signature-verified.json")))
            {
                var stage = target + ".staging-" + Guid.NewGuid().ToString("N");
                try
                {
                    SafeZip.Extract(package, stage, ct);
                    var stagedTools=FindMetalUnder(stage)??throw new IOException("The Metal ZIP did not contain the Windows metal.exe and metallib.exe tools.");
                    await VerifyAppleSignature(ToolLocator.MetalExe(stagedTools,"ios","metal")!);
                    await VerifyAppleSignature(ToolLocator.MetalExe(stagedTools,"ios","metallib")!);
                    StateStore.AtomicJson(Path.Combine(stage,"apple-signature-verified.json"),new{SourceSha256=hash,VerifiedAt=DateTimeOffset.UtcNow});
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    if(Directory.Exists(target))Directory.Move(target,target+".unverified-"+Guid.NewGuid().ToString("N"));
                    Directory.Move(stage,target);
                }
                finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
            }
            var found = FindMetalUnder(target) ?? throw new IOException("The Metal ZIP did not contain the Windows metal.exe and metallib.exe tools.");
            selection.MetalFolder = found; return true;
        }
        await VerifyAppleSignature(package);
        log("Opening Apple's own Metal installer and license screen. Finish it to continue.");
        using var process = Process.Start(new ProcessStartInfo(package) { UseShellExecute = true });
        if (process != null) await process.WaitForExitAsync(ct);
        Detect(selection);
        if (!ValidMetalRoot(selection.MetalFolder)) throw new IOException("Metal installation has not been detected yet. Finish the Apple installer, then press Check Again.");
        return true;
    }
}
