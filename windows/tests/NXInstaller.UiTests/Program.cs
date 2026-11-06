using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NXInstaller.App;
using NXInstaller.Core;

AppBuilder.Configure<InstallerApplication>().WithInterFont().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing=false }).SetupWithoutStarting();
string output=Path.GetFullPath(args.Length>0?args[0]:"artifacts/ui-render-tests");Directory.CreateDirectory(output);
var results=new List<object>();int failures=0;
void Flush(){Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();Dispatcher.UIThread.RunJobs();}
void Pump(Task task)
{
 var watch=System.Diagnostics.Stopwatch.StartNew();
 while(!task.IsCompleted&&watch.Elapsed<TimeSpan.FromSeconds(8)){Flush();Thread.Sleep(5);}
 if(!task.IsCompleted)throw new TimeoutException("Managed browser task did not complete");
 task.GetAwaiter().GetResult();Flush();
}
void Click(Button button){button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Flush();}
void Check(bool ok,string message,List<string> issues){if(!ok)issues.Add(message);}
Rect Bounds(Visual visual,Visual ancestor){var origin=visual.TranslatePoint(default,ancestor)??default;return new Rect(origin,visual.Bounds.Size);}
List<string> Inspect(Window window)
{
 var issues=new List<string>();
 foreach(var t in window.GetVisualDescendants().OfType<TextBlock>().Where(t=>t.IsEffectivelyVisible&&!string.IsNullOrEmpty(t.Text)))
 {
  if(t.Bounds.Width<1||t.Bounds.Height<1)continue;
  string sample=(t.Text??"")[..Math.Min(45,t.Text?.Length??0)];
  Check(t.TextLayout.Height<=t.Bounds.Height+1,$"Text clipped vertically: {sample} ({t.TextLayout.Height}>{t.Bounds.Height})",issues);
  Check(t.TextLayout.Width<=t.Bounds.Width+2,$"Text clipped horizontally: {sample} ({t.TextLayout.Width}>{t.Bounds.Width})",issues);
 }
 return issues;
}
using var dummy=new MemoryStream();
var main=new MainWindow(@"C:\NXNativeWindows",preview:true);main.Show();main.Preview();Flush();
foreach(var (w,h) in new[]{(1180,900),(960,720),(760,600),(600,520),(520,500),(1180,650)})
{
 main.Width=w;main.Height=h;main.Preview(longText:true);Flush();
 var issues=Inspect(main);var log=Bounds(main.LogPanel,main);var components=Bounds(main.ComponentsPanel,main);
 Check(!log.Intersects(components),"Log panel overlaps component panel",issues);
 Check(log.Height>=110,"Log panel is too short",issues);
 foreach(var row in main.Components.Values)
 {
  var title=Bounds(row.TitleText,row);var badge=Bounds(row.BadgeText,row);var desc=Bounds(row.DetailText,row);
  Check(!title.Intersects(badge),"Component title overlaps its status badge",issues);
  Check(!title.Intersects(desc),"Component title overlaps description",issues);
 }
 using(var frame=main.CaptureRenderedFrame()){if(frame==null)issues.Add("No rendered frame");else frame.Save(Path.Combine(output,$"main-{w}x{h}.png"));}
 failures+=issues.Count;results.Add(new{Case=$"main-{w}x{h}",LogHeight=log.Height,Issues=issues});Console.WriteLine($"main {w}x{h}: {issues.Count} issue(s)");foreach(var x in issues)Console.WriteLine(x);
}
foreach(double scale in new[]{1.25,1.5,2.0})
{
 main.MinWidth=0;main.MinHeight=0;main.Width=1100/scale;main.Height=900/scale;Flush();var issues=Inspect(main);
 Check(!Bounds(main.LogPanel,main).Intersects(Bounds(main.ComponentsPanel,main)),"Panels overlap at equivalent DPI viewport",issues);
 var pixels=new PixelSize((int)Math.Round(main.ClientSize.Width*scale),(int)Math.Round(main.ClientSize.Height*scale));
 using(var frame=new RenderTargetBitmap(pixels,new Vector(96*scale,96*scale))){frame.Render(main);frame.Save(Path.Combine(output,$"main-density-{scale}.png"));}
 failures+=issues.Count;results.Add(new{Case=$"density-{scale}",LogicalSize=main.ClientSize.ToString(),Issues=issues});Console.WriteLine($"density {scale}: {issues.Count} issue(s)");foreach(var x in issues)Console.WriteLine(x);
}
// Exercise the actual splitter with headless pointer events, not a parallel layout model.
main.Width=1100;main.Height=900;Flush();double before=main.LogPanel.Bounds.Height;
var splitter=Bounds(main.LogSplitter,main);var grab=splitter.Center;var moved=new Point(grab.X,grab.Y-85);
main.MouseMove(grab);main.MouseDown(grab,MouseButton.Left);main.MouseMove(moved);main.MouseUp(moved,MouseButton.Left);Flush();
var resizeIssues=new List<string>();Check(main.LogPanel.Bounds.Height>before+50,"Increasing log allocation did not enlarge the log",resizeIssues);failures+=resizeIssues.Count;results.Add(new{Case="expand-log",Before=before,After=main.LogPanel.Bounds.Height,Issues=resizeIssues});
using(var frame=main.CaptureRenderedFrame())frame?.Save(Path.Combine(output,"main-expanded-log.png"));
var apple=new AppleSetupWindow(Path.GetTempPath(),new(),_=>{},preview:true);apple.Show();
foreach(var(w,h)in new[]{(720,720),(580,560),(480,480)})
{
 apple.Width=w;apple.Height=h;Flush();var issues=Inspect(apple);failures+=issues.Count;results.Add(new{Case=$"apple-{w}x{h}",Issues=issues});Console.WriteLine($"apple {w}x{h}: {issues.Count} issue(s)");foreach(var x in issues)Console.WriteLine(x);
 using(var frame=apple.CaptureRenderedFrame())frame?.Save(Path.Combine(output,$"apple-{w}x{h}.png"));
}
apple.Close();
string fixture=Path.Combine(Path.GetTempPath(),"nxw-ui-picker-"+Guid.NewGuid().ToString("N"));
string picked=Path.Combine(fixture,"new installation with spaces");Directory.CreateDirectory(picked);
string xip=Path.Combine(fixture,"Xcode official fixture.xip");File.WriteAllText(xip,"TEST FIXTURE ONLY");File.WriteAllText(Path.Combine(fixture,"Xcode.xip.crdownload"),"partial");
main.RootPath.Text=fixture;
// Invoke the same Browse click handler as the shipped application, including the modal result.
Click(main.Browse);var picker=main.OwnedWindows.OfType<PathPickerWindow>().Single();Pump(picker.Loading);
foreach(var(w,h)in new[]{(760,600),(520,480),(440,440)})
{
 picker.Width=w;picker.Height=h;Flush();var issues=Inspect(picker);
 Check(!Bounds(picker.Entries,picker).Intersects(Bounds(picker.SelectionPath,picker)),"Browser list overlaps selected path",issues);
 var regions=picker.Layout.Children.ToArray();
 for(int i=0;i<regions.Length;i++)for(int j=i+1;j<regions.Length;j++)
  Check(!Bounds(regions[i],picker).Intersects(Bounds(regions[j],picker)),"Browser layout regions overlap: rows "+Grid.GetRow(regions[i])+" and "+Grid.GetRow(regions[j]),issues);
 failures+=issues.Count;results.Add(new{Case=$"browse-layout-{w}x{h}",Issues=issues});Console.WriteLine($"browse layout {w}x{h}: {issues.Count} issue(s)");foreach(var x in issues)Console.WriteLine(x);
 using(var frame=picker.CaptureRenderedFrame())frame?.Save(Path.Combine(output,$"browse-{w}x{h}.png"));
}
{
 var issues=new List<string>();Pump(picker.NavigateAsync(Path.Combine(fixture,"does-not-exist")));
 Check(picker.Message.Text!.StartsWith("Cannot open this path:"),"Missing path did not show an inline error",issues);
 Check(main.IsVisible&&picker.IsVisible,"Invalid navigation closed the application",issues);
 picker.SelectionPath.Text=Path.GetPathRoot(fixture);Click(picker.Confirm);
 Check(picker.SelectedPath==null&&picker.IsVisible,"Drive-root install path was accepted or closed the picker",issues);
 Pump(picker.NavigateAsync(picked));Click(picker.Confirm);Flush();
 Check(main.RootPath.Text==picked,"Selected folder with spaces did not reach the installation form",issues);
 Check(main.IsVisible&&main.Browse.IsEnabled,"Main window closed or Browse stayed disabled",issues);
 failures+=issues.Count;results.Add(new{Case="browse-change-path-and-recover-errors",Issues=issues});Console.WriteLine($"browse change/error recovery: {issues.Count} issue(s)");foreach(var x in issues)Console.WriteLine(x);
}
{
 var issues=new List<string>();string beforePath=main.RootPath.Text!;
 for(int attempt=0;attempt<3;attempt++)
 {
  Click(main.Browse);var cancelPicker=main.OwnedWindows.OfType<PathPickerWindow>().Single();Pump(cancelPicker.Loading);Click(cancelPicker.CancelButton);
  Check(main.RootPath.Text==beforePath&&main.IsVisible&&main.Browse.IsEnabled,"Cancel changed the path or closed the installer",issues);
 }
 failures+=issues.Count;results.Add(new{Case="browse-cancel-reopen-three-times",Issues=issues});Console.WriteLine($"browse cancel/reopen: {issues.Count} issue(s)");
}
{
 var issues=new List<string>();var appleLive=new AppleSetupWindow(fixture,new(){XcodeXip=xip},_=>{});appleLive.Show();Flush();Click(appleLive.ChooseXip);
 var filePicker=appleLive.OwnedWindows.OfType<PathPickerWindow>().Single();Pump(filePicker.Loading);Pump(filePicker.NavigateAsync(xip));
 var items=filePicker.Entries.ItemsSource!.Cast<BrowserEntry>().ToArray();
 Check(items.All(i=>i.IsDirectory||i.Name.EndsWith(".xip",StringComparison.OrdinalIgnoreCase)),"XIP filter included incomplete or unrelated files",issues);
 Check(!filePicker.SelectionPath.IsVisible&&filePicker.Confirm.Content?.ToString()=="Open","XIP picker still asks for a folder or output path",issues);
 filePicker.Entries.SelectedItem=items.First(i=>i.IsDirectory);Flush();
 Check(!filePicker.Confirm.IsEnabled,"XIP Open button accepted a folder",issues);filePicker.ConfirmSelection();
 Check(filePicker.IsVisible&&filePicker.SelectedPath==null,"A folder was accepted instead of a file",issues);
 filePicker.Entries.SelectedItem=items.First(i=>i.Path==xip);Flush();
 Check(filePicker.Confirm.IsEnabled&&filePicker.FileName.Text==Path.GetFileName(xip),"Selecting a file did not prepare Open",issues);
 Check(filePicker.Message.Text!.StartsWith("Ready to open"),"Previous selection error was not cleared",issues);
 foreach(var(w,h)in new[]{(760,600),(520,480),(440,440)})
 {
  filePicker.Width=w;filePicker.Height=h;Flush();var layoutIssues=Inspect(filePicker);var regions=filePicker.Layout.Children.ToArray();
  for(int i=0;i<regions.Length;i++)for(int j=i+1;j<regions.Length;j++)
   Check(!Bounds(regions[i],filePicker).Intersects(Bounds(regions[j],filePicker)),"XIP file-picker layout regions overlap",layoutIssues);
  Check(!filePicker.SelectionPath.IsEffectivelyVisible,"XIP picker shows an output-folder field",layoutIssues);
  Check(Bounds(filePicker.Confirm,filePicker).Bottom<=filePicker.ClientSize.Height,"XIP Open button fell below the viewport",layoutIssues);
  failures+=layoutIssues.Count;results.Add(new{Case=$"xip-file-layout-{w}x{h}",Issues=layoutIssues});Console.WriteLine($"XIP file layout {w}x{h}: {layoutIssues.Count} issue(s)");foreach(var x in layoutIssues)Console.WriteLine(x);
  using(var frame=filePicker.CaptureRenderedFrame())frame?.Save(Path.Combine(output,$"xip-file-{w}x{h}.png"));
 }
 filePicker.Width=760;filePicker.Height=600;Flush();
 using(var frame=filePicker.CaptureRenderedFrame())frame?.Save(Path.Combine(output,"xip-file-selection.png"));
 Click(filePicker.Confirm);Flush();
 Check(appleLive.XipPath.Text==xip&&appleLive.IsVisible,"XIP selection failed or closed Apple Setup",issues);
 failures+=issues.Count;results.Add(new{Case="apple-xip-managed-picker",Issues=issues});Console.WriteLine($"XIP picker: {issues.Count} issue(s)");foreach(var x in issues)Console.WriteLine(x);appleLive.Close();
}
main.Close();Directory.Delete(fixture,true);StateStore.AtomicJson(Path.Combine(output,"ui-results.json"),new{Engine="Actual Avalonia + Skia production controls, headless platform",GeneratedAt=DateTimeOffset.UtcNow,Failures=failures,Results=results});
return failures==0?0:1;
