using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NXNativeEasyBuilder;

var temp = Path.Combine(Path.GetTempPath(), "nxmac-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    var sha = new string('a', 40);
    var fixture = Path.Combine(temp, "fixture");
    var source = Path.Combine(fixture, "nxnative-" + sha);
    var tools = Path.Combine(source, "dm/gfx/tools");
    Directory.CreateDirectory(Path.Combine(tools, "dmsh_xlate_bin"));
    Directory.CreateDirectory(Path.Combine(tools, "spirv_cross_bin"));
    Directory.CreateDirectory(Path.Combine(source, "scripts"));
    var script = Path.Combine(source, "scripts/build_ipa_from_nsp.py");
    File.WriteAllText(script, "# Test fixture");
    File.WriteAllText(Path.Combine(tools, "own_compilers.py"), "XCODE_VERSION = '27.2'\n");
    var locks = new List<string>();
    foreach (var name in new[] { "spirv-cross", "dmsh_xlate.dll", "Ryujinx.Graphics.Shader.dll", "Ryujinx.Common.dll" })
    {
        var file = Path.Combine(tools, name == "spirv-cross" ? "spirv_cross_bin" : "dmsh_xlate_bin", name);
        File.WriteAllText(file, name);
        locks.Add(name + "\t" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))[..16].ToLowerInvariant());
    }
    File.WriteAllLines(Path.Combine(tools, "dmsh_chain.lock"), locks);
    var archive = Path.Combine(temp, "repo.tar.gz");
    await using (var file = File.Create(archive))
    await using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
        TarFile.CreateFromDirectory(fixture, gzip, includeBaseDirectory: false);

    var mock = new ArchiveServer(sha, archive);
    using var client = new HttpClient(mock);
    var repo = new Repository(Path.Combine(temp, "app data"), client);
    var latest = await repo.LatestAsync(default);
    Assert(latest == new CommitInfo(sha, "Fixture commit"), "Latest commit and first subject line");
    var release = await repo.StageAsync(latest, _ => { }, _ => { }, default);
    Assert(repo.Active() == null && Repository.FingerprintsMatch(release), "Stage without activation");
    repo.Activate(release);
    Assert(repo.Active() == release, "Active commit round trip");
    Assert(await repo.StageAsync(latest, _ => { }, _ => { }, default) == release && mock.Downloads == 1, "Reuse verified checkout");
    File.AppendAllText(release.SpirvCross, "bad");
    Assert(!Repository.FingerprintsMatch(release), "Detect wrong SPIRV-Cross bytes");
    Assert(await repo.StageAsync(latest, _ => { }, _ => { }, default) == release && mock.Downloads == 2 && Repository.FingerprintsMatch(release),
        "Repair a corrupted tool");
    await Throws<IOException>(() => repo.StageAsync(new CommitInfo(new string('b', 40), "bad archive"), _ => { }, _ => { }, default));
    Assert(repo.Active() == release && !Directory.EnumerateDirectories(Path.GetDirectoryName(release.Folder)!, ".download-*").Any(),
        "Failed candidate leaves active commit and no staging files");

    var game = Path.Combine(temp, "Game with spaces é");
    var key = Path.Combine(temp, "Test Keys", "prod.keys");
    var exports = Path.Combine(temp, "Exports");
    Directory.CreateDirectory(game);
    Directory.CreateDirectory(Path.GetDirectoryName(key)!);
    Directory.CreateDirectory(exports);
    File.WriteAllText(Path.Combine(game, "placeholder.nsp"), "test file");
    File.WriteAllText(key, "test fixture, no keys");
    var ipaRequest = new BuildRequest(game, key, exports, true, false);
    var arguments = ipaRequest.Arguments(release);
    Assert(arguments.Contains(game) && arguments.Contains(key) && arguments.Contains("ios") && arguments.Contains("none") &&
        arguments.Contains("--keep-extracted") && !arguments.Contains("--work-dir"), "IPA arguments preserve paths and upstream cache location");
    File.WriteAllText(Path.Combine(exports, "Game with spaces é.ipa"), "existing output");
    Assert(ipaRequest.Arguments(release).Last().EndsWith("-2.ipa"), "Do not overwrite a file in the custom output directory");
    await Throws<IOException>(() => Task.FromResult((ipaRequest with { Keys = null }).Arguments(release)));
    Assert(true, "Missing keys caught before build");
    var nup = Path.Combine(game, "test.nup");
    File.WriteAllText(nup, "test fixture");
    var macRequest = new BuildRequest(nup, null, null, false, true);
    var macArgs = macRequest.Arguments(release);
    Assert(macArgs.Contains("mac-app") && !macArgs.Contains("-k") && !macArgs.Contains("--gpu"), "NUP Mac app options");

    string? label = null;
    double? fraction = null;
    var events = new BuildProgress(_ => { }, (text, value) => (label, fraction) = (text, value));
    events.Receive("""{"ev":"stage","k":2,"n":4,"name":"shaders"}""");
    events.Receive("""{"ev":"progress","k":2,"name":"shaders","done":3200000000,"total":6400000000,"unit":"B","frac":0.5}""");
    Assert(fraction == .375 && label?.Contains(3_200_000_000L.ToString("N0")) == true, "Upstream progress and 64-bit byte counts");
    events.Receive("""{"ev":"say","msg":"[RED] Synthetic shader error"}""");
    Assert(events.Error == "Synthetic shader error", "Structured upstream error");

    var python = Environment.GetEnvironmentVariable("NXMAC_TEST_PYTHON") ?? "/usr/bin/python3";
    var macTools = new MacTools("/Fake Xcode/Contents/Developer", python, "/usr/bin/dotnet", "/fake/dotnet");
    File.WriteAllText(release.Script, """
        import json, os, pathlib, sys
        out = pathlib.Path(sys.argv[sys.argv.index('-o') + 1])
        assert pathlib.Path(sys.argv[1]).is_dir()
        assert 'prod.keys' in sys.argv[sys.argv.index('-k') + 1]
        assert pathlib.Path(os.environ['SPIRV_CROSS']).is_file()
        assert os.environ['DEVELOPER_DIR'] == '/Fake Xcode/Contents/Developer'
        assert not os.environ.get('DM_ALLOW_OTHER_METAL')
        out.write_bytes(b'test artifact')
        print(json.dumps({'ev':'say','msg':f'done: {out} (0.0 GB)'}))
        """);
    var made = await GameBuild.RunAsync(ipaRequest, release, macTools, ipaRequest.Arguments(release),
        new BuildProgress(_ => { }, (_, _) => { }), default);
    Assert(File.Exists(made) && made.EndsWith("-2.ipa"), "Real child process argument, environment and output handoff");
    var appRequest = macRequest with { OutputFolder = exports };
    File.WriteAllText(release.Script, """
        import json, pathlib, sys
        assert '-k' not in sys.argv and sys.argv[sys.argv.index('--platform') + 1] == 'mac-app'
        out = pathlib.Path(sys.argv[sys.argv.index('-o') + 1])
        (out / 'Contents').mkdir(parents=True)
        (out / 'Contents/Info.plist').write_text('test app fixture')
        print(json.dumps({'ev':'say','msg':f'done: {out} (0.0 GB); open it from Finder.'}))
        """);
    var app = await GameBuild.RunAsync(appRequest, release, macTools, appRequest.Arguments(release),
        new BuildProgress(_ => { }, (_, _) => { }), default);
    Assert(app.EndsWith("test.app") && Directory.Exists(app), "Mac app result and NUP arguments through a child process");
    File.WriteAllText(release.Script, "print('{\"ev\":\"say\",\"msg\":\"[RED] Build failed\"}'); raise SystemExit(1)\n");
    await Throws<IOException>(() => GameBuild.RunAsync(ipaRequest, release, macTools, ipaRequest.Arguments(release),
        new BuildProgress(_ => { }, (_, _) => { }), default));
    Assert(true, "Nonzero upstream exit fails the build");
    File.WriteAllText(release.Script, "raise SystemExit(0)\n");
    await Throws<IOException>(() => GameBuild.RunAsync(ipaRequest, release, macTools, ipaRequest.Arguments(release),
        new BuildProgress(_ => { }, (_, _) => { }), default));
    Assert(true, "Missing artifact fails even with zero exit");

    using (var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(250)))
    {
        await Throws<OperationCanceledException>(() => ProcessRunner.RunAsync("/bin/sh", ["-c", "sleep 30 & wait"], stop.Token));
        Assert(true, "Cancel external process with child");
    }

    if (args.Contains("--live"))
    {
        using var liveClient = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        liveClient.DefaultRequestHeaders.UserAgent.ParseAdd("NXNativeEasyBuilder-Test/1.0.0");
        var live = new Repository(Path.Combine(temp, "live"), liveClient);
        var head = await live.LatestAsync(default);
        var checkout = await live.StageAsync(head, Console.WriteLine, _ => { }, default);
        Assert(Repository.FingerprintsMatch(checkout) && checkout.XcodeVersion.Length > 0, $"Live repository {head.Sha}");
        if (!OperatingSystem.IsWindows())
            Assert((File.GetUnixFileMode(checkout.SpirvCross) & UnixFileMode.UserExecute) != 0, "GitHub tar preserved executable mode");
    }

    if (args.Contains("--ui")) RunUi(Path.Combine(temp, "ui"), args.Last());
    return 0;
}
finally { Directory.Delete(temp, recursive: true); }

static void RunUi(string dataRoot, string evidence)
{
    Directory.CreateDirectory(evidence);
    AppBuilder.Configure<BuilderApp>().WithInterFont().UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
    var window = new MainWindow(preview: true, dataRoot: dataRoot);
    window.Show();
    window.Preview();
    void Flush()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
    Rect Bounds(Visual visual) => new(visual.TranslatePoint(default, window)!.Value, visual.Bounds.Size);
    foreach (var (width, height) in new[] { (1040, 800), (900, 670), (640, 540), (1280, 900) })
    {
        window.Width = width;
        window.Height = height;
        Flush();
        var log = window.GetVisualDescendants().OfType<TextBox>().Single(x => x.Name == "ActivityLog");
        var forms = window.GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert(Bounds(log).Height >= 75 && !Bounds(log).Intersects(Bounds(forms)), $"Resize {width}×{height} log and form separation");
        var labels = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible &&
            !string.IsNullOrEmpty(t.Text) && t.Bounds.Width > 1 && t.Bounds.Height > 1);
        Assert(labels.All(t => t.TextLayout.Height <= t.Bounds.Height + 1), $"Resize {width}×{height} text fits");
        using var frame = window.CaptureRenderedFrame() ?? throw new Exception("No GUI render");
        frame.Save(Path.Combine(evidence, $"ui-{width}x{height}.png"));
    }
    var logBox = window.GetVisualDescendants().OfType<TextBox>().Single(x => x.Name == "ActivityLog");
    var splitter = window.GetVisualDescendants().OfType<GridSplitter>().Single();
    var start = Bounds(splitter).Center;
    var old = logBox.Bounds.Height;
    window.MouseMove(start);
    window.MouseDown(start, MouseButton.Left);
    window.MouseMove(new Point(start.X, start.Y - 70));
    window.MouseUp(new Point(start.X, start.Y - 70), MouseButton.Left);
    Flush();
    Assert(logBox.Bounds.Height >= old + 60, "Dragging divider enlarges the log");
    window.Close();
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException("FAIL " + message);
    Console.WriteLine("PASS " + message);
}

static async Task Throws<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}");
}

sealed class ArchiveServer(string sha, string archive) : HttpMessageHandler
{
    public int Downloads { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
    {
        var answer = new HttpResponseMessage(HttpStatusCode.OK);
        if (request.RequestUri!.Host == "api.github.com")
            answer.Content = new StringContent(JsonSerializer.Serialize(new { sha, commit = new { message = "Fixture commit\nDetails" } }));
        else
        {
            Downloads++;
            answer.Content = new StreamContent(File.OpenRead(archive));
        }
        return Task.FromResult(answer);
    }
}
