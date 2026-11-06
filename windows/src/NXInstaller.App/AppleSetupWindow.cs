using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Threading;
using NXInstaller.Core;

namespace NXInstaller.App;

public sealed class AppleSetupWindow : Window
{
    public readonly TextBlock XipPath = Ui.Label("No XIP selected yet", 12, Ui.Muted);
    public readonly TextBlock MetalStatus = Ui.Label("Searching installed Metal tools…", 12, Ui.Cyan);
    public readonly CheckBox SdkAuthorization = new() { Content = Ui.Label("I have separate applicable authorization from Apple that permits this iOS SDK on this Windows host.",12,Ui.Warn) };
    public readonly CheckBox MetalLicense = new() { Content = Ui.Label("For a Metal ZIP: I downloaded it from Apple and accepted its applicable license terms.",12,Ui.Muted) };
    public readonly Button ContinueButton = Ui.Button("Continue Automatic Setup", true);
    public readonly Button ChooseXip = Ui.Button("Choose Xcode .xip");
    private readonly AppleSelection selection;
    private readonly AppleToolsAutomation automatic;
    private readonly string root;
    private readonly bool preview;
    private readonly DispatcherTimer poll;
    private bool working;
    public AppleSetupWindow(string root, AppleSelection existing, Action<string> log, bool preview = false)
    {
        this.root = root; this.preview = preview;
        selection = new() { SdkFolder=existing.SdkFolder, XcodeXip=existing.XcodeXip, MetalFolder=existing.MetalFolder, HasWindowsSdkAuthorization=existing.HasWindowsSdkAuthorization,
            AuthorizationAttestedAt=existing.AuthorizationAttestedAt, MetalLicenseAccepted=existing.MetalLicenseAccepted };
        automatic = new(root,new ProcessRunner(),log);
        Title = "Apple Setup · NXNativeWindows v"+Ui.Version; Width=720; Height=720; MinWidth=440; MinHeight=440; Background=Ui.Bg; Foreground=Ui.Text; WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing=16, Margin=new Thickness(22) };
        layout.Children.Add(Ui.Stack(Ui.Label("Apple setup, simplified.",22,bold:true),Ui.Label("Choose the Xcode XIP once. SDK extraction, Metal detection and tool paths are handled automatically.",12,Ui.Muted)));
        var cards = new StackPanel { Spacing=14 };
        var choose = ChooseXip;
        choose.Click += async (_,_) =>
        {
            try
            {
                var path = await PathPickerWindow.PickAsync(this, selection.XcodeXip.Length > 0 ? selection.XcodeXip : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), xipOnly: true);
                if(path != null) { selection.XcodeXip=path; selection.SdkFolder=""; XipPath.Text=selection.XcodeXip; Refresh(); }
            }
            catch(Exception e) { await Ui.Alert(this, "Xcode selection", e.Message); }
        };
        var xcode = Ui.Button("Xcode Downloads"); xcode.Click+=(_,_)=>Ui.Open("https://developer.apple.com/download/all/?q=Xcode");
        var actions = new Grid { ColumnDefinitions=new ColumnDefinitions("*,Auto"),ColumnSpacing=8 }; actions.Children.Add(choose); Grid.SetColumn(xcode,1);actions.Children.Add(xcode);
        cards.Children.Add(Ui.Box(Ui.Stack(Ui.Label("1  ·  Xcode .xip",14,bold:true),Ui.Label("Only this file needs to be selected. You do not need to choose an SDK output folder.",12,Ui.Muted),XipPath,actions)));
        var download = Ui.Button("Get Metal from Apple"); download.Click+=(_,_)=>Ui.Open(AppleToolsAutomation.OfficialMetalUrl);
        cards.Children.Add(Ui.Box(Ui.Stack(Ui.Label("2  ·  Metal tools — automatic",14,bold:true),MetalStatus,
            Ui.Label("Installed tools are detected automatically. Otherwise, download the official Metal Developer Tools for Windows package into Downloads or beside your XIP. The app finds and installs/extracts it; no Metal folder picker is needed.",12,Ui.Muted),download,MetalLicense)));
        SdkAuthorization.IsChecked=selection.CanUseSdk; MetalLicense.IsChecked=selection.MetalLicenseAccepted;
        var terms = Ui.Button("Read Apple Agreement"); terms.Click+=(_,_)=>Ui.Open(AppleSelection.AgreementUrl);
        cards.Children.Add(Ui.Box(Ui.Stack(Ui.Label("3  ·  SDK authorization",14,Ui.Warn,true),
            Ui.Label("The standard Xcode agreement restricts execution to Apple hardware running macOS. Accepting those standard terms alone does not authorize SDK use on Windows. Leave this step pending unless you hold a separate applicable permission.",12,Ui.Muted),SdkAuthorization,terms)));
        var scroll = new ScrollViewer { Content=cards,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled }; Grid.SetRow(scroll,1); layout.Children.Add(scroll);
        var footer = new Grid { ColumnDefinitions=new ColumnDefinitions("Auto,*"),ColumnSpacing=12 };var later=Ui.Button("Keep Pending");later.Click+=(_,_)=>Close((AppleSelection?)null);footer.Children.Add(later);Grid.SetColumn(ContinueButton,1);ContinueButton.HorizontalAlignment=HorizontalAlignment.Stretch;footer.Children.Add(ContinueButton);Grid.SetRow(footer,2);layout.Children.Add(footer);
        ContinueButton.Click+=async(_,_)=>await ContinueAsync();Content=layout;
        poll=new DispatcherTimer {Interval=TimeSpan.FromSeconds(3)};poll.Tick+=(_,_)=>{if(!working)Refresh();};
        Opened+=(_,_)=>
        {
            if(!preview&&(Screens.ScreenFromWindow(this)??Screens.Primary)is{}screen)
            {double scale=RenderScaling;MinWidth=Math.Min(440,screen.WorkingArea.Width/scale-24);MinHeight=Math.Min(440,screen.WorkingArea.Height/scale-32);Width=Math.Min(720,screen.WorkingArea.Width/scale-24);Height=Math.Min(720,screen.WorkingArea.Height/scale-32);}
            Refresh();poll.Start();
        };Closed+=(_,_)=>poll.Stop();
    }
    private void Refresh()
    {
        if(preview) {XipPath.Text=@"C:\Downloads\Xcode_27.3_beta.xip";MetalStatus.Text="Automatic detection · official Metal package found in Downloads";return;}
        automatic.Detect(selection);
        XipPath.Text=selection.XcodeXip.Length>0?selection.XcodeXip:Directory.Exists(selection.SdkFolder)?"Existing iOS SDK detected automatically":"Choose the official Xcode .xip";
        if(AppleToolsAutomation.ValidMetalRoot(selection.MetalFolder)) {MetalStatus.Text="Metal tools detected automatically · ready";MetalStatus.Foreground=Ui.Good;}
        else if(AppleToolsAutomation.FindPackage(AppleToolsAutomation.SearchDirectories(selection.XcodeXip)) is {} file) {MetalStatus.Text="Downloaded Metal package found: "+Path.GetFileName(file)+" · press Continue";MetalStatus.Foreground=Ui.Cyan;}
        else {MetalStatus.Text="Metal not installed yet · download its official Windows package, then press Continue.";MetalStatus.Foreground=Ui.Warn;}
    }
    private async Task ContinueAsync()
    {
        if(working||preview)return;
        if(SdkAuthorization.IsChecked!=true) {await Ui.Alert(this,"Authorization required","Keep this step pending unless your applicable Apple authorization permits this SDK on Windows.");return;}
        if(!File.Exists(selection.XcodeXip)&&!AppleToolsAutomation.ValidSdkRoot(selection.SdkFolder)) {await Ui.Alert(this,"Choose Xcode XIP","Choose the officially obtained Xcode .xip. The SDK output path is automatic.");return;}
        working=true;ContinueButton.IsEnabled=false;
        try
        {
            selection.HasWindowsSdkAuthorization=true;selection.AuthorizationAttestedAt=DateTimeOffset.UtcNow;selection.MetalLicenseAccepted=MetalLicense.IsChecked==true;
            MetalStatus.Text="Detecting / preparing Metal tools. Apple's installer may open with its own license screen.";
            if(!await automatic.PrepareMetalAsync(selection,default)) {MetalStatus.Text="Download Metal Developer Tools for Windows from Apple into Downloads, then press Continue. No folder selection is required.";Ui.Open(AppleToolsAutomation.OfficialMetalUrl);return;}
            Close(selection);
        }
        catch(Exception e) {MetalStatus.Text=e.Message;await Ui.Alert(this,"Metal setup",e.Message);}
        finally {working=false;ContinueButton.IsEnabled=true;}
    }
}
