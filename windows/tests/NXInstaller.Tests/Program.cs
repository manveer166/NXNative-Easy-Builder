using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NXInstaller.Core;

var results = new List<object>(); int failures = 0;
var workspace = Path.Combine(Path.GetTempPath(), "nxw-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(workspace);
async Task Test(string name, Func<Task> test)
{
    try { await test(); results.Add(new { Name = name, Passed = true }); Console.WriteLine("PASS  " + name); }
    catch (Exception e) { failures++; results.Add(new { Name = name, Passed = false, Error = e.ToString() }); Console.WriteLine("FAIL  " + name + " · " + e.Message); }
}
void Assert(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
async Task Throws<T>(Func<Task> action) where T : Exception
{ try { await action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
Task Sync(Action action) { action(); return Task.CompletedTask; }
string Zip(string name, params (string Path, string Content)[] entries)
{
    var p = Path.Combine(workspace, name + ".zip"); using var z = ZipFile.Open(p, ZipArchiveMode.Create);
    foreach (var item in entries) { var e = z.CreateEntry(item.Path); using var writer = new StreamWriter(e.Open()); writer.Write(item.Content); }
    return p;
}
void FixtureSdk(string folder,string version="27.3")
{
    foreach(string framework in new[]{"Foundation","UIKit"})
    {string headers=Path.Combine(folder,"System","Library","Frameworks",framework+".framework","Headers");Directory.CreateDirectory(headers);File.WriteAllText(Path.Combine(headers,framework+".h"),"fixture");}
    File.WriteAllText(Path.Combine(folder,"SDKSettings.json"),JsonSerializer.Serialize(new{Version=version}));
}
await Test("Host policy accepts Windows x64 build 19041+", () => Sync(() => Assert(HostPolicy.Evaluate(true, Architecture.X64, 19041).Allowed)));
await Test("Host policy rejects old x64 Windows", () => Sync(() => Assert(!HostPolicy.Evaluate(true, Architecture.X64, 18363).Allowed)));
await Test("Host policy rejects Windows 10 ARM64", () => Sync(() => Assert(!HostPolicy.Evaluate(true, Architecture.Arm64, 19045).Allowed)));
await Test("Windows 11 ARM64 is allowed only as experimental", () => Sync(() => { var h = HostPolicy.Evaluate(true, Architecture.Arm64, 22631); Assert(h.Allowed && h.Experimental); }));
await Test("Host policy rejects x86", () => Sync(() => Assert(!HostPolicy.Evaluate(true, Architecture.X86, 26100).Allowed)));
await Test("Host policy rejects non-Windows", () => Sync(() => Assert(!HostPolicy.Evaluate(false, Architecture.X64, 26100).Allowed)));
await Test("Apple authorization defaults to blocked", () => Sync(() => Assert(!new AppleSelection().CanUseSdk)));
await Test("Authorization flag alone is insufficient", () => Sync(() => Assert(!new AppleSelection { HasWindowsSdkAuthorization = true }.CanUseSdk)));
await Test("Explicit authorization plus attestation is recorded", () => Sync(() => Assert(new AppleSelection { HasWindowsSdkAuthorization = true, AuthorizationAttestedAt = DateTimeOffset.UtcNow }.CanUseSdk)));
await Test("Unlicensed SDK verification executes no external command", async () =>
{
    var runner = new CountingRunner(); var checks = await new HealthVerifier(runner, _ => { }).VerifyAsync(workspace, new(), true, default);
    Assert(runner.Calls == 0 && checks.Any(c => c.Required && !c.Ok));
});
await Test("Launch blocked when setup is not verified", async () =>
{
    var d = Path.Combine(workspace, "unverified"); StateStore.Save(d, new());
    await Throws<InvalidOperationException>(() => Sync(() => InstallerEngine.LaunchCommand(d)));
});
await Test("Launch blocked without SDK authorization even with stale ready state", async () =>
{
    var d = Path.Combine(workspace, "unauthorized"); StateStore.Save(d, new() { Ready = true });
    await Throws<InvalidOperationException>(() => Sync(() => InstallerEngine.LaunchCommand(d)));
});
await Test("Launch command preserves paths with spaces and uses the managed Python", () => Sync(() =>
{
    var d = Path.Combine(workspace, "launch paths with spaces"); var repo = Path.Combine(d, "source repo"); var scripts = Path.Combine(d, "runtime", "Scripts");
    Directory.CreateDirectory(Path.Combine(repo, "win")); Directory.CreateDirectory(scripts);
    File.WriteAllText(Path.Combine(scripts, "pythonw.exe"), "fixture"); File.WriteAllText(Path.Combine(repo, "win", "gui.pyw"), "fixture");
    StateStore.Save(d, new() { Ready = true, Repository = repo, Python = Path.Combine(scripts, "python.exe"),
        Apple = new() { HasWindowsSdkAuthorization = true, AuthorizationAttestedAt = DateTimeOffset.UtcNow } });
    var command = InstallerEngine.LaunchCommand(d);
    Assert(command.File == Path.Combine(scripts, "pythonw.exe") && command.Args.Count == 1 && command.Args[0] == Path.Combine(repo, "win", "gui.pyw") && command.WorkingDirectory == repo);
}));
await Test("Managed child environment contains explicit tools and clears Python overrides", () => Sync(() =>
{
    var state = new InstallState { Python = Path.Combine(workspace, "runtime", "Scripts", "python.exe"), Toolchain = new() { ["ios_sdk"] = "authorized SDK", ["llvm"] = "my LLVM" } };
    var env = HealthVerifier.BuildEnvironment(state);
    Assert(env["NXW_IOS_SDK"] == "authorized SDK" && env["NXW_LLVM"] == "my LLVM" && env["PYTHONHOME"] == "" && env["PYTHONPATH"] == "" && env["PYTHONUTF8"] == "1");
}));
await Test("State is atomically saved and round trips", () => Sync(() =>
{
    var d = Path.Combine(workspace, "state"); StateStore.Save(d, new() { Commit = "abc", Toolchain = new() { ["llvm"] = "a path with spaces" } });
    var s = StateStore.Load(d); Assert(s.Commit == "abc" && s.Toolchain["llvm"] == "a path with spaces" && !Directory.GetFiles(d, "*.tmp").Any());
}));
await Test("Corrupt state fails instead of silently resetting", async () =>
{
    var d = Path.Combine(workspace, "badstate"); Directory.CreateDirectory(d); await File.WriteAllTextAsync(Path.Combine(d, "installer-state.json"), "not json");
    await Throws<JsonException>(() => Sync(() => StateStore.Load(d)));
});
await Test("Drive-root install path is rejected", async () => await Throws<InvalidOperationException>(() => Sync(() => StateStore.ValidateRoot(Path.GetPathRoot(workspace)!))));
await Test("Path with spaces is retained", () => Sync(() => Assert(StateStore.ValidateRoot(Path.Combine(workspace, "my installation")).EndsWith("my installation"))));
await Test("PowerShell user strings are literal quoted", () => Sync(() => Assert(PowerShell.Literal("a'$(bad);b") == "'a''$(bad);b'")));
await Test("Package plan uses the official winget source and explicit x64", () => Sync(() =>
{
    foreach (var p in PackagePlan.Packages) { var a = PackagePlan.Arguments(p, false).ToList(); Assert(a[a.IndexOf("--source") + 1] == "winget" && a[a.IndexOf("--architecture") + 1] == "x64"); }
}));
await Test("Package plan never bypasses hashes or triggers reboots", () => Sync(() =>
{
    Assert(PackagePlan.Packages.All(p => !PackagePlan.Arguments(p, false).Any(a => a.Contains("ignore-security") || a == "--allow-reboot")));
}));
await Test("Update plan targets each package rather than all user software", () => Sync(() =>
{
    Assert(PackagePlan.Packages.All(p => PackagePlan.Arguments(p, true)[0] == "upgrade" && !PackagePlan.Arguments(p, true).Contains("--all")));
}));
await Test("Doctor parser respects optional checks", () => Sync(() =>
{
    var r = HealthVerifier.ParseDoctor("[{\"name\":\"optional\",\"ok\":false,\"detail\":\"missing\",\"required\":false}]", 0);
    Assert(r.Count == 1 && r.All(c => !c.Required || c.Ok));
}));
await Test("Doctor parser catches required missing tool", () => Sync(() =>
{ Assert(HealthVerifier.ParseDoctor("[{\"name\":\"clang\",\"ok\":false,\"detail\":\"missing\",\"required\":true}]", 1).Any(c => c.Required && !c.Ok)); }));
await Test("Doctor parser fails closed on invalid JSON", () => Sync(() => Assert(HealthVerifier.ParseDoctor("traceback", 1).Any(c => !c.Ok))));
await Test("Doctor parser fails closed on empty check list", () => Sync(() => Assert(HealthVerifier.ParseDoctor("[]", 0).Any(c => !c.Ok))));
await Test("Doctor nonzero exit code cannot report success", () => Sync(() => Assert(HealthVerifier.ParseDoctor("[{\"name\":\"x\",\"ok\":true,\"detail\":\"ok\"}]", 4).Any(c => !c.Ok))));
await Test("ZIP extracts a normal archive without changing source bytes", () => Sync(() =>
{
    var p = Zip("normal", ("repo/win/test.py", "a\nb\n")); var d = Path.Combine(workspace, "normal"); SafeZip.Extract(p, d, default); Assert(File.ReadAllText(Path.Combine(d, "repo/win/test.py")) == "a\nb\n");
}));
foreach (var (name, entry) in new[] { ("traversal", "../escape.txt"), ("backslash traversal", "..\\escape.txt"), ("absolute", "/escape.txt"), ("drive path", "C:/escape.txt"), ("alternate data stream", "file:stream"), ("trailing dot", "folder./file"), ("dot segment", "a/./b") })
    await Test("ZIP rejects " + name, async () => { var p = Zip(Guid.NewGuid().ToString("N"), (entry, "no")); await Throws<InvalidDataException>(() => Sync(() => SafeZip.Extract(p, Path.Combine(workspace, Guid.NewGuid().ToString("N")), default))); });
await Test("ZIP rejects symbolic links", async () =>
{
    var p = Zip("symlink", ("link", "../outside")); using (var z = ZipFile.Open(p, ZipArchiveMode.Update)) z.Entries[0].ExternalAttributes = 0xA1FF << 16;
    await Throws<InvalidDataException>(() => Sync(() => SafeZip.Extract(p, Path.Combine(workspace, "linkout"), default)));
});
await Test("ZIP rejects duplicate files", async () =>
{
    var p = Zip("duplicate", ("same", "one"), ("same", "two")); await Throws<IOException>(() => Sync(() => SafeZip.Extract(p, Path.Combine(workspace, "dupout"), default)));
});
await Test("ZIP enforces unpacked size limit", async () =>
{ var p = Zip("size", ("big", new string('x', 100))); await Throws<InvalidDataException>(() => Sync(() => SafeZip.Extract(p, Path.Combine(workspace, "sizeout"), default, 50))); });
await Test("ZIP cancellation interrupts extraction", async () =>
{ var p = Zip("cancel", ("file", "data")); using var c = new CancellationTokenSource(); c.Cancel(); await Throws<OperationCanceledException>(() => Sync(() => SafeZip.Extract(p, Path.Combine(workspace, "cancelout"), c.Token))); });
await Test("Downloads reject plaintext HTTP", async () =>
{ using var d = new Downloads(); await Throws<InvalidDataException>(() => d.FileAsync("http://example.test/a", Path.Combine(workspace, "http"), null, null, default)); });
await Test("SHA-256 mismatches are rejected without committing the download", async () =>
{
    using var d = new Downloads(new FakeHttp(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) }));
    var path = Path.Combine(workspace, "hashfail"); await Throws<InvalidDataException>(() => d.FileAsync("https://example.test/a", path, new string('0', 64), null, default)); Assert(!File.Exists(path));
});
await Test("Verified cached downloads are reused", async () =>
{
    var path = Path.Combine(workspace, "cached"); await File.WriteAllBytesAsync(path, [1, 2, 3]);
    using var d = new Downloads(new FakeHttp(_ => throw new Exception("Unexpected network request")));
    await d.FileAsync("https://example.test/a", path, Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2, 3 })), null, default); Assert(File.ReadAllBytes(path).Length == 3);
});
await Test("Download reports bytes without Content-Length and leaves no partial file", async () =>
{
    var updates = new List<TransferProgress>();
    using var d = new Downloads(new FakeHttp(_ => new(HttpStatusCode.OK) { Content = new StreamContent(new DelayedStream(new byte[4096], TimeSpan.FromMilliseconds(10), 512)) }));
    var path = Path.Combine(workspace, "unknown-length");
    await d.FileAsync("https://example.test/data", path, null, null, default, p => { lock(updates) updates.Add(p); });
    Assert(new FileInfo(path).Length == 4096 && !File.Exists(path + ".part"));
    Assert(updates.Any(p => p.Bytes > 0 && p.Total == null && p.Detail.Contains("MiB")));
});
await Test("Download heartbeats show a connection waiting for response headers", async () =>
{
    var updates = new List<TransferProgress>();
    var heartbeatSeen=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    using var d = new Downloads(new AsyncHttp(async (request, ct) => { await heartbeatSeen.Task.WaitAsync(TimeSpan.FromSeconds(5),ct); return new(HttpStatusCode.OK) { RequestMessage = request, Content = new ByteArrayContent([1]) }; }),
        new(HeartbeatInterval: TimeSpan.FromMilliseconds(20)));
    await d.FileAsync("https://example.test/data", Path.Combine(workspace,"waiting-headers"), null, null, default, p =>
    { lock(updates) {updates.Add(p);if(updates.Count(x=>x.Phase.StartsWith("Connecting"))>=2)heartbeatSeen.TrySetResult(true);} });
    Assert(heartbeatSeen.Task.IsCompletedSuccessfully);
});
await Test("Download header timeout retries finitely and preserves an existing target", async () =>
{
    int calls = 0; var updates = new List<TransferProgress>();
    using var d = new Downloads(new AsyncHttp(async (_, ct) => { calls++; await Task.Delay(Timeout.Infinite, ct); throw new Exception("unreachable"); }),
        new(HeaderTimeout: TimeSpan.FromMilliseconds(60), HeartbeatInterval: TimeSpan.FromMilliseconds(20), RetryDelay: TimeSpan.Zero, MaxAttempts: 2));
    var path = Path.Combine(workspace,"header-timeout"); File.WriteAllText(path,"keep me");
    await Throws<IOException>(() => d.FileAsync("https://example.test/data", path, null, null, default, p => updates.Add(p)));
    Assert(calls == 2 && File.ReadAllText(path) == "keep me" && !File.Exists(path + ".part"));
    Assert(updates.Any(p => p.Phase.StartsWith("Retrying")));
});
await Test("A stalled response stream times out, logs retry and cleans partial bytes", async () =>
{
    int calls = 0; var updates = new List<TransferProgress>();
    using var d = new Downloads(new FakeHttp(_ => { calls++; return new(HttpStatusCode.OK) { Content = new StreamContent(new DelayedStream([1], TimeSpan.FromSeconds(30), 1)) }; }),
        new(IdleTimeout: TimeSpan.FromMilliseconds(60), HeartbeatInterval: TimeSpan.FromMilliseconds(20), RetryDelay: TimeSpan.Zero, MaxAttempts: 2));
    var path = Path.Combine(workspace,"stalled-download");
    await Throws<IOException>(() => d.FileAsync("https://example.test/data", path, null, null, default, p => { lock(updates) updates.Add(p); }));
    Assert(calls == 2 && !File.Exists(path) && !File.Exists(path + ".part"));
    Assert(updates.Any(p => p.Phase.Contains("stalled")));
});
await Test("Slow but active downloads still obey the overall time limit", async () =>
{
    using var d = new Downloads(new FakeHttp(_ => new(HttpStatusCode.OK) { Content = new StreamContent(new DelayedStream(new byte[10000], TimeSpan.FromMilliseconds(15), 1)) }),
        new(IdleTimeout: TimeSpan.FromSeconds(10), TotalTimeout: TimeSpan.FromMilliseconds(120)));
    var path = Path.Combine(workspace,"download-budget");
    await Throws<TimeoutException>(() => d.FileAsync("https://example.test/data", path, null, null, default));
    Assert(!File.Exists(path) && !File.Exists(path + ".part"));
});
await Test("Stop cancels downloading immediately without retrying", async () =>
{
    int calls = 0;
    using var d = new Downloads(new FakeHttp(_ => { calls++; return new(HttpStatusCode.OK) { Content = new StreamContent(new DelayedStream(new byte[100],TimeSpan.FromSeconds(10),1)) }; }));
    using var cancel = new CancellationTokenSource(60); var path = Path.Combine(workspace,"download-stop");
    await Throws<OperationCanceledException>(() => d.FileAsync("https://example.test/data",path,null,null,cancel.Token));
    Assert(calls == 1 && !File.Exists(path + ".part"));
});
await Test("Truncated downloads are not activated", async () =>
{
    using var d = new Downloads(new FakeHttp(_ => { var content = new ByteArrayContent([1,2]); content.Headers.ContentLength = 100; return new(HttpStatusCode.OK) { Content = content }; }), new(RetryDelay: TimeSpan.Zero, MaxAttempts: 1));
    var path = Path.Combine(workspace,"truncated-download"); await Throws<IOException>(() => d.FileAsync("https://example.test/data",path,null,null,default));
    Assert(!File.Exists(path) && !File.Exists(path + ".part"));
});
await Test("Update checker loads default branch HEAD without downloading code", async () =>
{
    var paths = new List<string>(); var sha = new string('a', 40);
    using var d = new Downloads(new FakeHttp(uri => { paths.Add(uri.AbsolutePath); return Json(uri.AbsolutePath.EndsWith("/commits/develop") ? Commit(sha) : "{\"default_branch\":\"develop\"}"); }));
    var v = await new RepositoryInstaller(d).CheckLatestAsync(default); Assert(v.Branch == "develop" && v.Commit == sha && paths.Count == 2 && paths.All(p => !p.Contains("/zip/")));
});
await Test("Update checker rejects invalid commit IDs", async () =>
{
    using var d = new Downloads(new FakeHttp(uri => Json(uri.AbsolutePath.Contains("/commits/") ? Commit("../bad") : "{\"default_branch\":\"main\"}")));
    await Throws<InvalidDataException>(() => new RepositoryInstaller(d).CheckLatestAsync(default));
});
await Test("Repository download is pinned and repair preserves builder settings", async () =>
{
    var sha = new string('b', 40); var files = new[] { "win/doctor.py", "win/gui.pyw", "win/toolchain.py", "win/sdk/extract_xip.py", "dm/gfx/tools/dmsh_xlate_bin/dmsh_xlate.dll" };
    var z = Zip("repository", files.Select(f => ("repo/" + f, "original\n")).ToArray());
    using var d = new Downloads(new FakeHttp(uri => { Assert(uri.AbsolutePath.EndsWith("/zip/" + sha)); return new(HttpStatusCode.OK) { Content = new ByteArrayContent(File.ReadAllBytes(z)) }; }));
    var repo = new RepositoryInstaller(d); var folder = Path.Combine(workspace, "managed install");
    var one = await repo.FetchAsync(folder, new(sha, "main", "test", null), _ => { }, _ => { }, default);
    await File.WriteAllTextAsync(Path.Combine(one.Directory, "win/gui_settings.json"), "user selection");
    await File.WriteAllTextAsync(Path.Combine(one.Directory, "win/doctor.py"), "corrupt");
    var two = await repo.FetchAsync(folder, new(sha, "main", "test", null), _ => { }, _ => { }, default);
    Assert(File.ReadAllText(Path.Combine(two.Directory, "win/gui_settings.json")) == "user selection");
    Assert(File.ReadAllText(Path.Combine(two.Directory, "win/doctor.py")) == "original\n");
    Assert(Directory.GetDirectories(Path.Combine(folder, "releases"), "*.backup-*").Length == 1);
});
await Test("New-commit updates preserve active builder selections and custom config", async () =>
{
    var oldSha = new string('d', 40); var newSha = new string('e', 40); var folder = Path.Combine(workspace, "new commit update");
    var previous = Path.Combine(folder, "releases", oldSha); Directory.CreateDirectory(Path.Combine(previous, "win"));
    await File.WriteAllTextAsync(Path.Combine(previous, "win/gui_settings.json"), "selected files");
    await File.WriteAllTextAsync(Path.Combine(previous, "win/toolchain.json"), "{\"custom\":\"retained\"}");
    StateStore.Save(folder, new() { Repository = previous, Commit = oldSha });
    var files = new[] { "win/doctor.py", "win/gui.pyw", "win/toolchain.py", "win/sdk/extract_xip.py", "dm/gfx/tools/dmsh_xlate_bin/dmsh_xlate.dll" };
    var z = Zip("newcommit", files.Select(f => ("repo/" + f, "source\n")).ToArray());
    using var d = new Downloads(new FakeHttp(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(File.ReadAllBytes(z)) }));
    var next = await new RepositoryInstaller(d).FetchAsync(folder, new(newSha, "main", "update", null), _ => { }, _ => { }, default);
    Assert(File.ReadAllText(Path.Combine(next.Directory, "win/gui_settings.json")) == "selected files");
    Assert(File.ReadAllText(Path.Combine(next.Directory, "win/toolchain.json")) == "{\"custom\":\"retained\"}");
    Assert(Directory.Exists(previous));
});
foreach (var scenario in new[] { "reuse", "legacy", "missing-cache", "repair-extra-source", "bad-cache", "bad-manifest", "restore-missing-checkout" })
await Test("Repository cache regression: " + scenario, async () =>
{
    var sha = new string('f',40); var folder = Path.Combine(workspace,"cache-" + scenario); int downloads = 0;
    var files = new[] { "win/doctor.py", "win/gui.pyw", "win/toolchain.py", "win/sdk/extract_xip.py", "dm/gfx/tools/dmsh_xlate_bin/dmsh_xlate.dll", "dm/other-source.c" };
    var archive = Zip("cache-fixture-" + scenario,files.Select(f => ("repo/" + f,"original")).ToArray());
    using var d = new Downloads(new FakeHttp(_ => { downloads++; return new(HttpStatusCode.OK) { Content = new ByteArrayContent(File.ReadAllBytes(archive)) }; }));
    var repository = new RepositoryInstaller(d); var version = new RepositoryVersion(sha,"main","fixture",null);
    var first = await repository.FetchAsync(folder,version,_ => {},_ => {},default);
    string manifest = Path.Combine(folder,"releases",sha + ".integrity.json"), cache = Path.Combine(folder,"downloads",sha + ".zip");
    string extra = Path.Combine(first.Directory,"dm/other-source.c");
    if (scenario == "legacy") File.Delete(manifest);
    if (scenario == "missing-cache") File.Delete(cache);
    if (scenario is "repair-extra-source" or "bad-cache") File.WriteAllText(extra,"modified");
    if (scenario == "bad-cache") File.WriteAllText(cache,"broken zip");
    if (scenario == "bad-manifest") File.WriteAllText(manifest,"{\"Files\":null}");
    if (scenario == "restore-missing-checkout") Directory.Delete(first.Directory,true);
    // These user-managed settings must not trigger source repair or get lost.
    if (Directory.Exists(first.Directory)) File.WriteAllText(Path.Combine(first.Directory,"win/toolchain.json"),"custom config");
    var log = new List<string>(); var second = await repository.FetchAsync(folder,version,log.Add,_ => {},default);
    Assert(File.ReadAllText(Path.Combine(second.Directory,"dm/other-source.c")) == "original");
    Assert(downloads == (scenario == "bad-cache" ? 2 : 1), "Unnecessary network request or corrupt cache was reused");
    if (scenario != "restore-missing-checkout") Assert(File.ReadAllText(Path.Combine(second.Directory,"win/toolchain.json")) == "custom config");
    if (scenario is "reuse" or "legacy" or "missing-cache" or "bad-manifest")
        Assert(log.Any(l => l.Contains("reusing it without download")) && Directory.GetDirectories(Path.Combine(folder,"releases"),"*.backup-*").Length == 0);
});
await Test("Repository extraction reports file and byte counts",()=>Sync(()=>
{
    string zip = Zip("extract-progress",("repo/a","one"),("repo/b","two")); var updates = new List<ExtractionProgress>();
    SafeZip.Extract(zip,Path.Combine(workspace,"extract-progress"),default,progress:updates.Add);
    Assert(updates.First().Files == 0 && updates.Last().Files == 2 && updates.Last().Fraction == 1 && updates.Last().Bytes == 6);
}));
await Test("Cancelled repository extraction retains the previous checkout and removes staging",async()=>
{
    var sha = new string('9',40); string folder = Path.Combine(workspace,"cancel-repository"), previous = Path.Combine(folder,"releases",sha);
    Directory.CreateDirectory(previous); File.WriteAllText(Path.Combine(previous,"keep.txt"),"retained");
    var files = new[] { "win/doctor.py", "win/gui.pyw", "win/toolchain.py", "win/sdk/extract_xip.py", "dm/gfx/tools/dmsh_xlate_bin/dmsh_xlate.dll" };
    string zip = Zip("cancel-repo",files.Select(f=>("repo/"+f,"fixture")).ToArray());
    using var d = new Downloads(new FakeHttp(_=>new(HttpStatusCode.OK){Content=new ByteArrayContent(File.ReadAllBytes(zip))}));
    using var cancel = new CancellationTokenSource();
    await Throws<OperationCanceledException>(()=>new RepositoryInstaller(d).FetchAsync(folder,new(sha,"main","fixture",null),_=>{},_=>{},cancel.Token,
        p=>{if(p.Detail.StartsWith("Extracting repository ·"))cancel.Cancel();}));
    Assert(File.ReadAllText(Path.Combine(previous,"keep.txt"))=="retained");
    Assert(Directory.GetDirectories(Path.Combine(folder,"work"),"repository-*").Length==0);
});
await Test("Repository with changed upstream layout is not activated", async () =>
{
    var z = Zip("incomplete", ("repo/README.md", "incomplete")); using var d = new Downloads(new FakeHttp(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(File.ReadAllBytes(z)) }));
    var folder = Path.Combine(workspace, "incomplete-repo"); var sha = new string('c', 40);
    await Throws<InvalidDataException>(() => new RepositoryInstaller(d).FetchAsync(folder, new(sha, "main", "test", null), _ => { }, _ => { }, default));
    Assert(!Directory.Exists(Path.Combine(folder, "releases", sha)));
});
await Test("Process runner preserves argument boundaries and shell metacharacters", async () =>
{
    var text = "spaces ' \" ; $(touch NEVER) & value";
    CommandSpec spec = OperatingSystem.IsWindows() ? PowerShell.Command("[Console]::Write(" + PowerShell.Literal(text) + ")") : new("/bin/echo", [text]);
    var result = await new ProcessRunner().RunAsync(spec, null, default); Assert(result.ExitCode == 0 && result.Output.TrimEnd() == text);
});
await Test("Process runner cancellation stops the child process", async () =>
{
    using var ct = new CancellationTokenSource(200); var command = OperatingSystem.IsWindows() ? PowerShell.Command("Start-Sleep -Seconds 20") : new CommandSpec("/bin/sleep", ["20"]);
    await Throws<OperationCanceledException>(() => new ProcessRunner().RunAsync(command, null, ct.Token));
});
await Test("Process runner timeouts fail explicitly", async () =>
{
    var command = OperatingSystem.IsWindows() ? PowerShell.Command("Start-Sleep -Seconds 20", TimeSpan.FromMilliseconds(200)) : new CommandSpec("/bin/sleep", ["20"], Timeout: TimeSpan.FromMilliseconds(200));
    await Throws<TimeoutException>(() => new ProcessRunner().RunAsync(command, null, default));
});
await Test("Metal package detection accepts only complete official-style package names", () => Sync(() =>
{
    Assert(AppleToolsAutomation.IsMetalPackage("Metal_Developer_Tools_for_Windows.exe"));
    Assert(AppleToolsAutomation.IsMetalPackage("Metal_Developer_Tools.zip"));
    Assert(!AppleToolsAutomation.IsMetalPackage("Metal_Developer_Tools.exe.crdownload"));
    Assert(!AppleToolsAutomation.IsMetalPackage("metal.exe"));
    Assert(!AppleToolsAutomation.IsMetalPackage("random-setup.exe"));
}));
await Test("Metal package lookup chooses the latest completed download", () => Sync(() =>
{
    string folder=Path.Combine(workspace,"metal-packages");Directory.CreateDirectory(folder);
    string old=Path.Combine(folder,"Metal_Developer_Tools_old.exe"), latest=Path.Combine(folder,"Metal_Developer_Tools_new.zip");
    File.WriteAllText(old,"old");File.WriteAllText(latest,"new");File.SetLastWriteTimeUtc(old,DateTime.UtcNow.AddDays(-1));
    Assert(AppleToolsAutomation.FindPackage([folder])==latest);
}));
await Test("Metal tool root requires both compiler and library linker", () => Sync(() =>
{
    string folder=Path.Combine(workspace,"metal-root");Directory.CreateDirectory(Path.Combine(folder,"bin"));File.WriteAllText(Path.Combine(folder,"bin","metal.exe"),"fixture");
    Assert(!AppleToolsAutomation.ValidMetalRoot(folder));File.WriteAllText(Path.Combine(folder,"bin","metallib.exe"),"fixture");Assert(AppleToolsAutomation.ValidMetalRoot(folder));
}));
await Test("Metal tool lookup resolves nested extracted tool roots automatically", () => Sync(() =>
{
    string folder=Path.Combine(workspace,"nested-metal");string target=Path.Combine(folder,"package","Windows Tools");Directory.CreateDirectory(Path.Combine(target,"bin"));
    File.WriteAllText(Path.Combine(target,"bin","metal.exe"),"fixture");File.WriteAllText(Path.Combine(target,"bin","metallib.exe"),"fixture");
    Assert(AppleToolsAutomation.FindMetalUnder(folder)==target);
}));
await Test("SDK lookup restores an extracted SDK path without a folder picker", () => Sync(() =>
{
    string folder=Path.Combine(workspace,"detected-sdk");string sdk=Path.Combine(folder,"sdk","iPhoneOS.sdk");FixtureSdk(sdk);
    var selection=new AppleSelection();new AppleToolsAutomation(folder,new CountingRunner(),_=>{}).Detect(selection);Assert(selection.SdkFolder==sdk);
}));
await Test("Metal ZIP preparation remains blocked until applicable terms are confirmed", async () =>
{
    string folder=Path.Combine(workspace,"metal-license");Directory.CreateDirectory(folder);string xip=Path.Combine(folder,"Xcode.xip");File.WriteAllText(xip,"fixture");
    string zip=Zip("metal-license-content",("Metal/bin/metal.exe","fixture"),("Metal/bin/metallib.exe","fixture"));File.Copy(zip,Path.Combine(folder,"Metal_Developer_Tools_for_Windows.zip"));
    var runner=new CountingRunner();var automation=new AppleToolsAutomation(Path.Combine(folder,"install"),runner,_=>{});
    await Throws<InvalidOperationException>(()=>automation.PrepareMetalAsync(new(){XcodeXip=xip},default));Assert(runner.Calls==0);
});
await Test("Authorized Metal ZIP is extracted, signature checked and configured automatically", async () =>
{
    string folder=Path.Combine(workspace,"metal-auto");Directory.CreateDirectory(folder);string xip=Path.Combine(folder,"Xcode.xip");File.WriteAllText(xip,"fixture");
    string zip=Zip("metal-auto-content",("Metal/bin/metal.exe","fixture"),("Metal/bin/metallib.exe","fixture"));File.Copy(zip,Path.Combine(folder,"Metal_Developer_Tools_for_Windows.zip"));
    var runner=new CountingRunner();var automation=new AppleToolsAutomation(Path.Combine(folder,"install"),runner,_=>{});var selection=new AppleSelection(){XcodeXip=xip,MetalLicenseAccepted=true};
    Assert(await automation.PrepareMetalAsync(selection,default));Assert(AppleToolsAutomation.ValidMetalRoot(selection.MetalFolder));Assert(runner.Calls==2);
    Assert(await automation.PrepareMetalAsync(selection,default));Assert(runner.Calls==2);
});
await Test("A Metal ZIP with rejected signatures is never activated or rediscovered", async () =>
{
    string folder=Path.Combine(workspace,"metal-rejected");Directory.CreateDirectory(folder);string xip=Path.Combine(folder,"Xcode.xip");File.WriteAllText(xip,"fixture");
    string zip=Zip("metal-rejected-content",("Metal/bin/metal.exe","fixture"),("Metal/bin/metallib.exe","fixture"));File.Copy(zip,Path.Combine(folder,"Metal_Developer_Tools_for_Windows.zip"));
    var automation=new AppleToolsAutomation(Path.Combine(folder,"install"),new RejectedSignatureRunner(),_=>{});var selection=new AppleSelection(){XcodeXip=xip,MetalLicenseAccepted=true};
    await Throws<IOException>(()=>automation.PrepareMetalAsync(selection,default));automation.Detect(selection);
    Assert(selection.MetalFolder.Length==0);Assert(!Directory.Exists(Path.Combine(folder,"install","apple","metal"))||!Directory.EnumerateFiles(Path.Combine(folder,"install","apple","metal"),"apple-signature-verified.json",SearchOption.AllDirectories).Any());
});
await Test("A repository or other existing folder is not accepted as an iOS SDK",()=>Sync(()=>
{string folder=Path.Combine(workspace,"repository-is-not-sdk");Directory.CreateDirectory(folder);Assert(!AppleToolsAutomation.ValidSdkRoot(folder));}));
await Test("SDK validation requires Foundation and UIKit headers",()=>Sync(()=>
{string folder=Path.Combine(workspace,"incomplete-sdk");FixtureSdk(folder);File.Delete(Path.Combine(folder,"System","Library","Frameworks","Foundation.framework","Headers","Foundation.h"));Assert(!AppleToolsAutomation.ValidSdkRoot(folder));}));
await Test("SDK validation requires iOS 26 or later",()=>Sync(()=>
{string folder=Path.Combine(workspace,"old-sdk");FixtureSdk(folder,"25.4");Assert(!AppleToolsAutomation.ValidSdkRoot(folder));}));
await Test("SDK validation rejects malformed SDK metadata",()=>Sync(()=>
{string folder=Path.Combine(workspace,"bad-sdk-metadata");FixtureSdk(folder);File.WriteAllText(Path.Combine(folder,"SDKSettings.json"),"not JSON");Assert(!AppleToolsAutomation.ValidSdkRoot(folder));}));
await Test("A mistaken repository-root SDK selection is repaired automatically",()=>Sync(()=>
{string folder=Path.Combine(workspace,"sdk-repo-parent");string actual=Path.Combine(folder,"win","sdk","out","iPhoneOS.sdk");FixtureSdk(actual);var selection=new AppleSelection(){SdkFolder=folder};new AppleToolsAutomation(Path.Combine(workspace,"install-parent"),new CountingRunner(),_=>{}).Detect(selection);Assert(selection.SdkFolder==actual);}));
await Test("An interrupted legacy SDK extraction is never reused",()=>Sync(()=>
{
    string folder=Path.Combine(workspace,"legacy-partial-sdk"),sdk=Path.Combine(folder,"sdk","extract-previous","iPhoneOS.sdk");FixtureSdk(sdk);
    var selection=new AppleSelection(){SdkFolder=sdk};new AppleToolsAutomation(folder,new CountingRunner(),_=>{}).Detect(selection);
    Assert(selection.SdkFolder==""&&!SdkExtractor.UsableSdk(sdk,folder));
}));
await Test("New SDK staging directories are never detected as an installed SDK",()=>Sync(()=>
{
    string folder=Path.Combine(workspace,"new-partial-sdk"),sdk=Path.Combine(folder,"sdk",".extract-12345678","iPhoneOS.sdk");FixtureSdk(sdk);
    var selection=new AppleSelection();new AppleToolsAutomation(folder,new CountingRunner(),_=>{}).Detect(selection);Assert(selection.SdkFolder=="");
}));
await Test("SDK extraction is blocked without applicable Apple authorization",async()=>
{
    string folder=Path.Combine(workspace,"sdk-unlicensed");var runner=new CountingRunner();
    await Throws<InvalidOperationException>(()=>new SdkExtractor(folder,runner,_=>{}).ExtractAsync("python",workspace,new(),default));
    Assert(runner.Calls==0&&!Directory.Exists(folder));
});
foreach(var scenario in new[]{"success","child-failure","incomplete","cancel"})
await Test("SDK activation transaction: "+scenario,async()=>
{
    string folder=Path.Combine(workspace,"SDK transaction with spaces "+scenario),target=Path.Combine(folder,"sdk","iPhoneOS.sdk"),xip=Path.Combine(folder,"Xcode fixture.xip");FixtureSdk(target);
    File.WriteAllText(Path.Combine(target,"previous.txt"),"retained");File.WriteAllText(xip,"fixture only");
    using var cancel=new CancellationTokenSource();CommandSpec? captured=null;
    var runner=new CallbackRunner((command,ct)=>
    {
        captured=command;string stage=command.Args[^1];string stagedSdk=Path.Combine(stage,"iPhoneOS.sdk");FixtureSdk(stagedSdk);
        File.WriteAllText(Path.Combine(stagedSdk,"new.txt"),"new fixture");
        if(scenario=="incomplete")File.Delete(Path.Combine(stagedSdk,"SDKSettings.json"));
        if(scenario=="cancel"){cancel.Cancel();ct.ThrowIfCancellationRequested();}
        return Task.FromResult(new CommandResult(scenario=="child-failure"?1:0,"fixture"));
    });
    var selection=new AppleSelection(){XcodeXip=xip,HasWindowsSdkAuthorization=true,AuthorizationAttestedAt=DateTimeOffset.UtcNow};
    var service=new SdkExtractor(folder,runner,_=>{});
    if(scenario=="success")
    {
        string path=await service.ExtractAsync("python with spaces",workspace,selection,cancel.Token);
        Assert(path==target&&File.Exists(Path.Combine(target,"new.txt"))&&File.Exists(Path.Combine(target,SdkExtractor.CompletionFile)));
        Assert(Directory.GetDirectories(Path.Combine(folder,"sdk"),"*.backup-*").Length==1);
    }
    else
    {
        if(scenario=="cancel")await Throws<OperationCanceledException>(()=>service.ExtractAsync("python",workspace,selection,cancel.Token));
        else await Throws<IOException>(()=>service.ExtractAsync("python",workspace,selection,cancel.Token));
        Assert(File.ReadAllText(Path.Combine(target,"previous.txt"))=="retained"&&!File.Exists(Path.Combine(target,"new.txt")));
    }
    Assert(Directory.GetDirectories(Path.Combine(folder,"sdk"),".extract-*").Length==0,"Failed or completed staging was not removed");
    Assert(captured!=null&&captured.Args[2]==xip&&captured.Environment!["PYTHONHOME"]==""&&captured.Timeout==TimeSpan.FromHours(2));
    Assert(File.ReadAllText(captured!.Args[0]).Contains("def windows_long_path"),"Bundled long-path adapter was not passed to Python");
});
await Test("Managed browser accepts quoted paths with spaces",()=>Sync(()=>
{
    string folder=Path.Combine(workspace,"folder with spaces");Directory.CreateDirectory(folder);
    Assert(PathBrowser.FullPath("\""+folder+"\"")==folder);
    Assert(PathBrowser.InitialDirectory(Path.Combine(folder,"new installation"))==folder);
}));
await Test("Managed browser lists folders and completed XIP files only",async()=>
{
    string folder=Path.Combine(workspace,"browse-xip");Directory.CreateDirectory(Path.Combine(folder,"Subfolder"));
    foreach(string f in new[]{"Xcode.xip","Xcode.xip.crdownload","note.txt"})File.WriteAllText(Path.Combine(folder,f),"fixture");
    var files=await PathBrowser.ReadAsync(folder,true,default);Assert(files.Count==2&&files[0].IsDirectory&&files[1].Name=="Xcode.xip");
    var folders=await PathBrowser.ReadAsync(folder,false,default);Assert(folders.Count==1&&folders[0].IsDirectory);
});
await Test("Managed browser reports missing paths without unsafe shell commands",async()=>
{await Throws<DirectoryNotFoundException>(()=>PathBrowser.ReadAsync(Path.Combine(workspace,"missing-folder"),false,default));});
await Test("Managed browser cancellation is honored",async()=>
{using var cancel=new CancellationTokenSource();cancel.Cancel();await Throws<OperationCanceledException>(()=>PathBrowser.ReadAsync(workspace,false,cancel.Token));});
await Test("Managed browser rejects blank and network paths",async()=>
{await Throws<ArgumentException>(()=>Sync(()=>PathBrowser.FullPath(" ")));await Throws<ArgumentException>(()=>Sync(()=>PathBrowser.FullPath(@"\\server\share")));});
int sdkFixtureIndex=Array.IndexOf(args,"--sdk-fixture");
if(sdkFixtureIndex>=0&&sdkFixtureIndex+1<args.Length)
await Test("Real child-process SDK extraction with the embedded adapter and synthetic XIP",async()=>
{
    string project=Path.GetFullPath(args[sdkFixtureIndex+1]);int pyIndex=Array.IndexOf(args,"--python");
    string python=pyIndex>=0&&pyIndex+1<args.Length?args[pyIndex+1]:(OperatingSystem.IsWindows()?"python":"python3");
    string folder=Path.Combine(workspace,"SDK integration "+new string('x',60)),repo=Path.Combine(folder,"repository"),xip=Path.Combine(folder,"Xcode synthetic.xip");
    Directory.CreateDirectory(Path.Combine(repo,"win","sdk"));
    File.Copy(Path.Combine(project,"tests","Fixtures","upstream_extract_xip.py"),Path.Combine(repo,"win","sdk","extract_xip.py"));
    var runner=new ProcessRunner();var made=await runner.RunAsync(new(python,[Path.Combine(project,"tests","test_sdk_extract.py"),"--write-fixture",xip],project,Timeout:TimeSpan.FromMinutes(1)),Console.WriteLine,default);
    Assert(made.ExitCode==0);
    var selection=new AppleSelection(){XcodeXip=xip,HasWindowsSdkAuthorization=true,AuthorizationAttestedAt=DateTimeOffset.UtcNow};
    string sdk=await new SdkExtractor(folder,runner,Console.WriteLine).ExtractAsync(python,repo,selection,default);
    string header=Path.Combine(sdk,"System","Cryptexes","OS","System","Library","Frameworks","AuthenticationServices.framework","Headers","ASAccountAuthenticationModificationReplacePasswordWithSignInWithAppleRequest.h");
    Assert(File.ReadAllText(header).StartsWith("/* SYNTHETIC TEST HEADER")&&File.Exists(Path.Combine(sdk,SdkExtractor.CompletionFile))&&AppleToolsAutomation.ValidSdkRoot(sdk));
    Assert(Directory.GetDirectories(Path.Combine(folder,"sdk"),".extract-*").Length==0);
});
if (args.Contains("--live"))
{
    await Test("LIVE GitHub latest-commit lookup and real pinned archive extraction", async () =>
    {
        using var d = new Downloads(); var repo = new RepositoryInstaller(d); var version = await repo.CheckLatestAsync(default);
        var tree = await repo.FetchAsync(Path.Combine(workspace, "live"), version, Console.WriteLine, _ => { }, default);
        Assert(File.Exists(Path.Combine(tree.Directory, "win/doctor.py")) && tree.Commit == version.Commit);
        var cache=Path.Combine(workspace,"live","downloads",version.Commit+".zip");var stamp=File.GetLastWriteTimeUtc(cache);
        var reuseLog=new List<string>();var reused=await repo.FetchAsync(Path.Combine(workspace,"live"),version,reuseLog.Add,_=>{},default);
        Assert(reused.Directory==tree.Directory&&File.GetLastWriteTimeUtc(cache)==stamp&&reuseLog.Any(l=>l.Contains("reusing it without download")));
        Console.WriteLine("LIVE COMMIT " + version.Commit);
    });
}
var reportIndex = Array.IndexOf(args, "--report");
if (reportIndex >= 0 && reportIndex + 1 < args.Length)
    StateStore.AtomicJson(Path.GetFullPath(args[reportIndex + 1]), new { Date = DateTimeOffset.UtcNow, Host = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.OSArchitecture.ToString(), Failures = failures, Results = results });
Console.WriteLine($"{results.Count - failures}/{results.Count} passed. {failures} failed.");
Directory.Delete(workspace, true);
return failures == 0 ? 0 : 1;

static HttpResponseMessage Json(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
static string Commit(string sha) => JsonSerializer.Serialize(new { sha, commit = new { message = "latest commit\nmore", committer = new { date = "2026-11-05T10:00:00Z" } } });
sealed class CountingRunner : ICommandRunner
{ public int Calls; public Task<CommandResult> RunAsync(CommandSpec command, Action<string>? log, CancellationToken ct) { Calls++; return Task.FromResult(new CommandResult(0, "")); } }
sealed class CallbackRunner(Func<CommandSpec,CancellationToken,Task<CommandResult>> callback):ICommandRunner
{public Task<CommandResult> RunAsync(CommandSpec command,Action<string>? log,CancellationToken ct)=>callback(command,ct);}
sealed class RejectedSignatureRunner : ICommandRunner
{ public Task<CommandResult> RunAsync(CommandSpec command,Action<string>? log,CancellationToken ct)=>Task.FromResult(new CommandResult(1,"signature rejected")); }
sealed class FakeHttp(Func<Uri, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    { var response = respond(request.RequestUri!); response.RequestMessage = request; return Task.FromResult(response); }
}
sealed class AsyncHttp(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> respond):HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>respond(request,ct);
}
sealed class DelayedStream(byte[] data,TimeSpan delay,int chunk):Stream
{
    int position;
    public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
    public override long Length=>throw new NotSupportedException();public override long Position{get=>position;set=>throw new NotSupportedException();}
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken ct=default)
    {await Task.Delay(delay,ct);int count=Math.Min(Math.Min(chunk,buffer.Length),data.Length-position);data.AsMemory(position,count).CopyTo(buffer);position+=count;return count;}
    public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    public override void Flush(){}public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
    public override void SetLength(long value)=>throw new NotSupportedException();public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
}
