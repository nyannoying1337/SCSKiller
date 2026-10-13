using ArcCache.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using System.Text.Json;
using Windows.Storage.Pickers;
using Windows.Graphics.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
namespace ArcCache;
public partial class MainWindow : Window {
    readonly AppStore store=new();
    DiagnosticLog? diagnostics;
    bool initializingDiagnostics;
    readonly SafetyClient safety=new(Path.Combine(AppContext.BaseDirectory,"ArcCache.SafetyEngine.exe"));
    IReadOnlyList<SteamGame> games=[];
    SteamGame? selected;
    ReceiptState? selectedReceipt;
    GameSetupState? setupState;
    bool busy,driverOriginal,routingReady,receiptValid,recordValid;
    string? steamRoot;
    Process? cacheMonitor;
    bool safetyAllowed;
    ScsKillerBridge? scs;
    CancellationTokenSource? scsCancellation;
    readonly DispatcherTimer discoveryTimer=new(){Interval=TimeSpan.FromSeconds(4)};
    string discoveryMessage="";
    bool preview;
    readonly Dictionary<uint,int> desiredLimits=new();
    bool updatingChoice, synchronizingSelection, backgroundCheck;
    string lastBackgroundError="", displayedArtwork="";
    int foregroundRuns, backgroundPolls, previewWatchSeconds;
    public MainWindow() {
        InitializeComponent();ExtendsContentIntoTitleBar=true;SetTitleBar(TitleArea);
        diagnostics=new DiagnosticLog(store.Root,Environment.GetCommandLineArgs().Contains("--debug"));
        safety.Diagnostic=diagnostics.Write;initializingDiagnostics=true;DiagnosticToggle.IsOn=diagnostics.Enabled;initializingDiagnostics=false;
        diagnostics.Write("session-start",new{Version=typeof(MainWindow).Assembly.GetName().Version?.ToString(),OS=Environment.OSVersion.ToString(),Runtime=Environment.Version.ToString(),Variant=typeof(MainWindow).Assembly.GetName().Name});
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1320,940));Nav.SelectedItem=LibraryNav;
        AppWindow.Closing+=(_,e)=>{if(busy)e.Cancel=true;else{discoveryTimer.Stop();StopCacheMonitor();}};
        Root.Loaded+=async (_,_)=>await Run("Reading your Steam library…",LoadLibrary);
        discoveryTimer.Tick+=async (_,_)=>await PollGame();
        try{scs=new ScsKillerBridge(Path.Combine(store.Root,"scskiller-bridge.json")).Load();string bundled=Path.Combine(AppContext.BaseDirectory,"SCSKiller","cli","scskiller.exe");if(scs.CliPath==null&&File.Exists(bundled))scs.SelectCli(bundled);ScsPath.Text=scs.CliPath??"Choose your installed SCSKiller CLI.";}catch(Exception e){Log("SCSKiller configuration could not be read: "+e.Message);scs=new ScsKillerBridge(Path.Combine(store.Root,"scskiller-bridge.json"));}
    }
    // Read-only visual verification; off-screen so a running game never loses focus.
    public void StartPreview(string path,uint appId=0,int watchSeconds=0) {
        previewWatchSeconds=Math.Clamp(watchSeconds,0,20);
        preview=true;
        AppWindow.IsShownInSwitchers=false;AppWindow.Move(new Windows.Graphics.PointInt32(-20000,-20000));AppWindow.Show(false);
        Root.Loaded+=async (_,_)=>{
            try {
                await Task.Delay(1500);while(busy)await Task.Delay(200);
                if(appId!=0){selected=games.FirstOrDefault(g=>g.AppId==appId);await Run("Showing selected game for read-only preview…",ShowGame);Filter();}
                await Task.Delay(700);while(busy)await Task.Delay(200);Root.Background=new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255,32,32,32));Root.UpdateLayout();
                if(Environment.GetCommandLineArgs().Contains("--preview-settings")){Nav.SelectedItem=Nav.FooterMenuItems.OfType<NavigationViewItem>().First(i=>Equals(i.Tag,"Settings"));Root.UpdateLayout();}
                int runsBefore=foregroundRuns,pollsBefore=backgroundPolls;
                if(previewWatchSeconds>0)await Task.Delay(previewWatchSeconds*1000);
                if(previewWatchSeconds>0)File.WriteAllText(path+".checks.json",JsonSerializer.Serialize(new{ForegroundRunsBefore=runsBefore,ForegroundRunsAfter=foregroundRuns,BackgroundPolls=backgroundPolls-pollsBefore,NavigationEnabled=Nav.IsEnabled}));
                var bitmap=new RenderTargetBitmap();await bitmap.RenderAsync(Root);
                using var file=File.Create(Path.GetFullPath(path));var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,file.AsRandomAccessStream());
                encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,96,96,(await bitmap.GetPixelsAsync()).ToArray());await encoder.FlushAsync();
            }catch(Exception e){Log("Preview failed: "+e);File.WriteAllText(path+".error.txt",e.ToString());}
            Close();
        };
    }
    void DiagnosticToggled(object sender,RoutedEventArgs e) {
        if(initializingDiagnostics||diagnostics==null)return;
        try{diagnostics.SetEnabled(DiagnosticToggle.IsOn);Log(diagnostics.Enabled?"Detailed diagnostics enabled. Files stay local; review them before sharing.":"Detailed diagnostics disabled. Existing logs are retained.");}
        catch(Exception error){Log("Diagnostic setting could not be saved: "+error.Message);initializingDiagnostics=true;DiagnosticToggle.IsOn=diagnostics.Enabled;initializingDiagnostics=false;}
    }
    void OpenLogs(object sender,RoutedEventArgs e) {
        try{Process.Start(new ProcessStartInfo("explorer.exe"){UseShellExecute=true,ArgumentList={System.IO.Path.GetDirectoryName(store.LogPath)!}});}catch(Exception error){Log("Could not open logs: "+error.Message);}
    }
    void Log(string text){store.Log(text);ActivityLog.Text+=DateTime.Now.ToString("HH:mm:ss ")+text+Environment.NewLine;}
    async Task Run(string label,Func<Task> action) {
        if(busy)return;foregroundRuns++;busy=true;Nav.IsEnabled=false;GlobalNotice.IsOpen=false;BusyRing.Visibility=Visibility.Visible;BusyRing.IsActive=true;FooterStatus.Text=label;Log(label);
        UpdateButtons();
        try{await action();FooterStatus.Text="Finished · result recorded in Activity";}
        catch(Exception e){diagnostics?.Write("action-error",new{Action=label,Error=e.ToString()});Log("Stopped: "+e.Message);FooterStatus.Text="Stopped · no further changes attempted";GlobalNotice.Severity=InfoBarSeverity.Warning;GlobalNotice.Title="Action stopped";GlobalNotice.Message=e.Message;GlobalNotice.IsOpen=true;}
        finally{busy=false;Nav.IsEnabled=true;BusyRing.IsActive=false;BusyRing.Visibility=Visibility.Collapsed;UpdateButtons();}
    }
    async Task<WorkerReply> Engine(WorkerRequest request) {
        WorkerReply reply=await safety.RunAsync(request);Log(reply.Message);if(!reply.Ok)throw new InvalidOperationException(reply.Message);return reply;
    }
    async Task LoadLibrary() {
        uint? previousId=selected?.AppId;var previousGames=games.ToDictionary(g=>g.AppId);
        LibraryResult result=await Task.Run(()=>SteamLibrary.Discover(steamRoot));games=result.Games.Select(g=>previousGames.TryGetValue(g.AppId,out var old)?g with{Detected=old.Detected,DetectedExecutable=old.DetectedExecutable}:g).ToArray();steamRoot=result.SteamRoot;
        LibraryCount.Text=$"{games.Count} installed Steam titles · local discovery";
        LibraryNotice.IsOpen=result.Warnings.Count>0;LibraryNotice.Title="Steam discovery";LibraryNotice.Message=string.Join(Environment.NewLine,result.Warnings.Take(3));
        Filter();await LoadDriver();
        GamesList.SelectedItem=games.FirstOrDefault(g=>g.AppId==previousId)??games.FirstOrDefault(g=>g.AppId==292030)??games.FirstOrDefault();selected=GamesList.SelectedItem as SteamGame;await ShowGame();
    }
    void Filter(){synchronizingSelection=true;try{uint? id=selected?.AppId;GamesList.ItemsSource=games.Where(g=>g.Name.Contains(Search.Text??"",StringComparison.OrdinalIgnoreCase)).ToArray();if(id!=null)GamesList.SelectedItem=games.FirstOrDefault(g=>g.AppId==id&&g.Name.Contains(Search.Text??"",StringComparison.OrdinalIgnoreCase));}finally{synchronizingSelection=false;}}
    async Task LoadDriver() {
        driverOriginal=false;routingReady=false;
        try {
            var reply=await Engine(new("Status"));var d=reply.Data;
            driverOriginal=d.GetProperty("Original").GetBoolean();routingReady=d.GetProperty("DeveloperOverride").GetBoolean()&&!d.GetProperty("RebootRequired").GetBoolean();
            DriverTitle.Text=driverOriginal?"Original Intel driver installed":"System driver has been modified";
            DriverSummary.Text=$"Intel Arc B580 · {d.GetProperty("Version").GetString()}\n"+(driverOriginal?"The system stays on the original driver. Private copies are configured per game.":"Restore the original with Intel's official installer before applying a game profile.");
            DriverDetails.Text=$"Package: {d.GetProperty("DriverFolder").GetString()}\nSHA-256: {d.GetProperty("Hash").GetString()}\nOriginal: {driverOriginal}";
            OverrideStatus.Text=RoutingPresentation.Status(d.GetProperty("DeveloperOverride").GetBoolean(),d.GetProperty("RebootRequired").GetBoolean());
        }catch(Exception e){DriverTitle.Text="Driver needs attention";DriverSummary.Text=e.Message;Log("Driver inspection stopped: "+e.Message);}
    }
    async Task PollGame() {
        if(busy||backgroundCheck||selected==null)return;
        var game=selected;uint id=game.AppId;backgroundCheck=true;backgroundPolls++;
        try {
            if(!game.Supported) {
                if(await DiscoverSelected(true)&&selected?.AppId==id&&!busy)await ShowGame();
            }else if(NeedsControlObservation()) {
                // Polling never enters global busy mode, clears artwork, or rechecks Steam safety.
                var result=await safety.RunAsync(new("VerifyControl",ReceiptPath:store.Receipt(id),ReadOnly:preview));
                if(!result.Ok)throw new InvalidOperationException(result.Message);
                if(selected?.AppId!=id||busy)return;
                if(result.Data.GetProperty("Verified").GetBoolean()) {
                    if(!await ReadSetup(game))return;
                    RuntimeStatus.Text="Original private driver loaded. Close the game before applying your limit.";
                    RenderSetup();Log("Original private-driver loading checked automatically.");
                    discoveryTimer.Stop();
                }
            }
            lastBackgroundError="";
        }catch(Exception e){if(lastBackgroundError!=e.Message){Log("Automatic check paused: "+e.Message);lastBackgroundError=e.Message;}}
        finally{backgroundCheck=false;}
    }
    async Task<bool> ReadSetup(SteamGame game) {
        GameSetupState? state=null;
        if(game.SupportedExecutable!=null)try {
            var reply=await safety.RunAsync(new("GameState",game.SupportedExecutable,store.Receipt(game.AppId)));
            if(!reply.Ok)throw new InvalidOperationException(reply.Message);state=GameSetupState.FromReply(reply);
        }catch(Exception e){state=new(false,false,false,false,null,e.Message);}
        if(selected?.AppId!=game.AppId)return false;
        setupState=state;selectedReceipt=state?.Receipt;recordValid=state?.RecordValid==true;receiptValid=state?.ContentsValid==true;
        return true;
    }
    void RenderSetup() {
        if(selected==null)return;
        var presentation=SetupPresentation.From(setupState,selected.Supported,routingReady);
        GameStatus.Text=presentation.Installed;CurrentCap.Text=presentation.Installed;
        GameNotice.Title=presentation.Title;GameNotice.Message=presentation.Next;
        GameNotice.Severity=setupState?.Error!=null||setupState?.CompatibilityRepairRequired==true?InfoBarSeverity.Warning:InfoBarSeverity.Informational;
        GameNotice.IsOpen=true;
        updatingChoice=true;
        int desired=desiredLimits.GetValueOrDefault(selected.AppId,selectedReceipt?.MiB is >512?selectedReceipt.MiB:1024);
        CapChoice.SelectedIndex=desired==4096?3:desired==2048?2:desired==1024?1:0;
        updatingChoice=false;UpdateButtons();
    }
    async Task ShowGame() {
        discoveryTimer.Stop();var game=selected;if(game==null){GameTitle.Text="Choose a game";GameStatus.Text="Select an installed Steam title.";UpdateButtons();return;}
        await DiscoverSelected();if(selected?.AppId!=game.AppId)return;game=selected;
        GameTitle.Text=game.Name;
        string artKey=string.Join("|",game.ArtworkSources);
        if(displayedArtwork!=artKey){displayedArtwork=artKey;GamePlaceholder.Visibility=Visibility.Visible;GameArtwork.Source=ArtworkImages.Get(game.ArtworkSources,240);}
        GameDetails.Text=$"Steam app: {game.AppId}\nInstall folder: {game.InstallDirectory}\nRecovery record: {store.Receipt(game.AppId)}";
        RuntimeStatus.Text="Loaded driver: not checked yet.";
        safetyAllowed=false;
        try {
            var assessment=(await safety.RunAsync(new("Safety",SteamAppId:game.AppId,InstallDirectory:game.InstallDirectory))).Data;
            if(selected?.AppId!=game.AppId)return;
            var notice=SafetyPresentation.From(assessment);safetyAllowed=!notice.Warn;
            SafetyPanel.Visibility=notice.Warn?Visibility.Visible:Visibility.Collapsed;
            SafetyNotice.IsOpen=notice.Warn;SafetyNotice.Title=notice.Title;SafetyNotice.Message=notice.Message;SafetyNotice.Severity=InfoBarSeverity.Warning;
            SafetyDetails.Text=assessment.GetProperty("Message").GetString()+"\n"+assessment.GetProperty("Multiplayer").GetString()+"\n"+assessment.GetProperty("SteamStatus").GetString();
            Log("Safety: "+SafetyDetails.Text);
        }catch(Exception e){if(selected?.AppId!=game.AppId)return;SafetyPanel.Visibility=Visibility.Visible;SafetyNotice.IsOpen=true;SafetyNotice.Title="Game safety could not be confirmed";SafetyNotice.Message="A modified driver may cause launch failures or account penalties. Continue only if you accept that risk.";SafetyNotice.Severity=InfoBarSeverity.Warning;SafetyDetails.Text=e.Message;Log("Safety check: "+e.Message);}
        if(!await ReadSetup(game))return;
        if(setupState?.Configured==true&&selectedReceipt?.MiB>512)desiredLimits[game.AppId]=selectedReceipt.MiB;
        RenderSetup();
        if((!preview||previewWatchSeconds>0)&&(!game.Supported||NeedsControlObservation()))discoveryTimer.Start();
    }
    void ArtworkOpened(object sender,RoutedEventArgs e)=>GamePlaceholder.Visibility=Visibility.Collapsed;
    void ArtworkFailed(object sender,ExceptionRoutedEventArgs e)=>GamePlaceholder.Visibility=Visibility.Visible;
    bool NeedsControlObservation()=>setupState?.Configured==true&&selectedReceipt?.MiB==512&&selectedReceipt.Status!="original-control-loading-verified";
    async Task<bool> DiscoverSelected(bool quiet=false) {
        if(selected==null)return false;var game=selected;
        try {
            var request=new WorkerRequest("DiscoverGame",ReceiptPath:store.Receipt(game.AppId),SteamAppId:game.AppId,InstallDirectory:game.InstallDirectory,ReadOnly:preview);
            var reply=quiet?await safety.RunAsync(request):await Engine(request);if(!reply.Ok)throw new InvalidOperationException(reply.Message);
            if(selected?.AppId!=game.AppId)return false;
            bool found=reply.Data.GetProperty("Validated").GetBoolean();string? exe=reply.Data.GetProperty("Executable").GetString();
            discoveryMessage=reply.Data.GetProperty("Message").GetString()??"";bool changed=game.Detected!=found||game.DetectedExecutable!=exe;
            var updated=game with{Detected=found,DetectedExecutable=exe};games=games.Select(g=>g.AppId==game.AppId?updated:g).ToArray();selected=updated;
            if(changed)Filter();
            if(found)discoveryTimer.Stop();return found;
        }catch(Exception e){if(quiet)throw;discoveryMessage="Automatic detection unavailable: "+e.Message;Log(discoveryMessage);if(selected?.AppId==game.AppId)selected=game with{Detected=false};return false;}
    }
    void CapChanged(object sender,SelectionChangedEventArgs e) {
        if(updatingChoice||selected==null||CapChoice.SelectedItem is not ComboBoxItem item)return;
        desiredLimits[selected.AppId]=int.Parse(item.Tag.ToString()!);UpdateButtons();
    }
    void UpdateButtons() {
        if(ApplyButton==null)return;
        bool supported=selected?.Supported==true;bool configured=setupState?.Configured==true;
        ApplyButton.IsEnabled=!busy&&supported&&driverOriginal&&routingReady&&(configured||setupState?.CanPrepare==true)&&!NeedsControlObservation();
        RestoreButton.IsEnabled=!busy&&selected?.SupportedExecutable!=null&&recordValid&&selectedReceipt?.Status!="removed";LaunchButton.IsEnabled=!busy&&selected!=null;ImportButton.IsEnabled=!busy&&selected!=null;CapChoice.IsEnabled=!busy&&supported;
        RestoreButton.Content=recordValid&&selectedReceipt!=null&&setupState?.FolderExists==false&&selectedReceipt.Status!="removed"?"Clear failed setup record":"Use system driver again";
        if(setupState?.FolderExists==true&&!routingReady)LaunchButton.IsEnabled=false;
        if(setupState?.FolderExists==true&&!configured)LaunchButton.IsEnabled=false;
        ApplyButton.Content=NeedsControlObservation()?"Play in DX12 to finish step 2":configured?"Apply selected limit":"Set up original private copy";
        bool same=configured&&CapChoice.SelectedItem is ComboBoxItem choice&&int.Parse(choice.Tag.ToString()!)==selectedReceipt?.MiB;
        if(same&&!NeedsControlObservation()){ApplyButton.IsEnabled=false;ApplyButton.Content="Selected limit already installed";}
        ScsStatusButton.IsEnabled=!busy&&selected!=null&&scs?.CliPath!=null;
        ScsCompileButton.IsEnabled=ScsStatusButton.IsEnabled&&!(configured&&selectedReceipt?.MiB>512);
        ScsSummary.Text=configured&&selectedReceipt?.MiB>512
            ? $"Compilation with this game's {SetupPresentation.Limit(selectedReceipt.MiB)} driver is not connected yet. Stock compilation is disabled here to avoid using a different limit. Your installed patch is unaffected."
            : "This compiler currently uses the system Intel driver (512 MiB). Community recordings depend on your existing SCSKiller account and settings.";
    }
    async Task<bool> Confirm(string title,string message,string primary,bool requireOriginalTest=false,bool requireRisk=false) {
        var panel=new StackPanel{Spacing=12};panel.Children.Add(new TextBlock{Text=message,TextWrapping=TextWrapping.Wrap});
        var dialog=new ContentDialog{XamlRoot=Root.XamlRoot,Title=title,Content=panel,PrimaryButtonText=primary,CloseButtonText="Cancel",DefaultButton=ContentDialogButton.Close,RequestedTheme=Root.RequestedTheme};
        CheckBox? original=null,risk=null;
        void Update()=>dialog.IsPrimaryButtonEnabled=(!requireOriginalTest||original?.IsChecked==true)&&(!requireRisk||risk?.IsChecked==true);
        if(requireOriginalTest){original=new CheckBox{Content="I tested this original private set in gameplay."};panel.Children.Add(original);original.Checked+=(_,_)=>Update();original.Unchecked+=(_,_)=>Update();}
        if(requireRisk){risk=new CheckBox{Content=new TextBlock{Text="I understand this may stop the game from starting or cause account penalties.",TextWrapping=TextWrapping.Wrap}};panel.Children.Add(risk);risk.Checked+=(_,_)=>Update();risk.Unchecked+=(_,_)=>Update();}
        Update();
        return await dialog.ShowAsync()==ContentDialogResult.Primary;
    }
    void Navigate(NavigationView sender,NavigationViewSelectionChangedEventArgs args){string? tag=(args.SelectedItem as NavigationViewItem)?.Tag as string;if(LibraryPanel==null)return;LibraryPanel.Visibility=tag=="Library"?Visibility.Visible:Visibility.Collapsed;DriverPanel.Visibility=tag=="Driver"?Visibility.Visible:Visibility.Collapsed;ActivityPanel.Visibility=tag=="Activity"?Visibility.Visible:Visibility.Collapsed;SettingsPanel.Visibility=tag=="Settings"?Visibility.Visible:Visibility.Collapsed;}
    async void RefreshGame(object sender,RoutedEventArgs e)=>await Run("Refreshing installed setup…",ShowGame);
    async void RefreshLibrary(object sender,RoutedEventArgs e)=>await Run("Refreshing Steam…",LoadLibrary);
    void SearchChanged(AutoSuggestBox sender,AutoSuggestBoxTextChangedEventArgs args){if(!busy)Filter();}
    async void GameSelected(object sender,SelectionChangedEventArgs e){if(busy||synchronizingSelection)return;var next=GamesList.SelectedItem as SteamGame;if(next?.AppId==selected?.AppId)return;selected=next;await Run("Checking this game's setup…",ShowGame);}
    async void InspectDriver(object sender,RoutedEventArgs e)=>await Run("Inspecting the original system driver…",LoadDriver);
    async void ApplyGame(object sender,RoutedEventArgs e) {
        SteamGame? game=selected;if(busy||game?.Supported!=true)return;
        await Run("Checking safety and confirming this private change…",async()=>{
        string receipt=store.Receipt(game.AppId);int mib=int.Parse(((ComboBoxItem)CapChoice.SelectedItem).Tag.ToString()!);
        bool configured=setupState?.Configured==true;
        string message=configured?$"Change only {game.Name}'s private Xe2 copy to {mib} MiB? Close the game first. The installed driver stays original.":$"Create a verified ORIGINAL private driver set for {game.Name}? It needs about 608 MiB. Close the game and disable its D3D12 proxy first. Test this original set before applying the patch. A known bundled 32-bit debug-helper conflict is handled with two verified 64-bit Windows helpers inside the private folder, when needed. Bundled game DLLs, system files and Windows settings stay untouched.";
        // Recheck before showing consent; the worker independently repeats this check.
        WorkerReply assessment;try{assessment=await safety.RunAsync(new("Safety",SteamAppId:game.AppId,InstallDirectory:game.InstallDirectory));}catch(Exception ex){assessment=new(false,ex.Message,default);}
        bool riskRequired=!assessment.Ok||!assessment.Data.GetProperty("Allowed").GetBoolean();
        if(riskRequired)message="This game may have multiplayer or anti-cheat. A modified driver can cause account penalties or stop it from starting.\n\n"+message;
        if(mib>=2048)message+="\n\nExperimental larger limit: Ready or Not reached the menu with 4 GiB on driver 32.0.101.9034 after the helper fix. Cache capacity and eviction behavior remain unmeasured. Keep the recovery receipt.";
        if(!await Confirm(riskRequired?"Apply anyway?":configured?"Apply private limit":"Prepare original control",message,configured?"Apply":"Prepare",requireRisk:riskRequired))return;
            if(configured)await Engine(new("Change",ReceiptPath:receipt,MiB:mib,Confirmed:true,RiskConfirmed:riskRequired));
            else await Engine(new("Prepare",game.SupportedExecutable,receipt,MiB:512,Confirmed:true,RiskConfirmed:riskRequired));
            await ShowGame();
            GlobalNotice.Severity=InfoBarSeverity.Success;GlobalNotice.Title=configured?"Limit installed":"Original copy prepared";
            GlobalNotice.Message=configured?$"{SetupPresentation.Limit(mib)} installed for {game.Name}. Private files verified. Launch the game to check loading; cache capacity is a separate test.":"Next: play through Steam in DX12 for the automatic loading check. Your selected limit is saved.";
            GlobalNotice.IsOpen=true;
        });
    }
    async void RestoreGame(object sender,RoutedEventArgs e) {
        if(selected?.SupportedExecutable==null)return;string receipt=store.Receipt(selected.AppId);
        if(!await Confirm("Return this game to stock","Close the selected game first. Remove only the hash-checked private files recorded for this setup? Changed or extra files stop removal. The original system driver and developer setting are untouched.","Remove private set"))return;
        await Run("Removing this game's verified private set…",async()=>{await Engine(new("Remove",ReceiptPath:receipt,Confirmed:true));await ShowGame();});
    }
    async void ObserveGame(object sender,RoutedEventArgs e) {
        string? executable=selected?.SupportedExecutable;
        if(executable==null){GlobalNotice.Title="No verified launch profile";GlobalNotice.Message="Select a supported game to inspect its loaded driver.";GlobalNotice.IsOpen=true;return;}
        await Run("Checking this game's loaded Intel driver…",async()=>{
            var result=await Engine(new("ObserveGame",executable));RuntimeStatus.Text="Loaded-driver observation:\n"+result.Message;
        });
    }
    void StopCacheMonitor() {
        if(cacheMonitor!=null){if(!cacheMonitor.HasExited)cacheMonitor.Kill();cacheMonitor.Dispose();cacheMonitor=null;}
        MonitorButton.Content="Show cache monitor";
    }
    async void ToggleCacheMonitor(object sender,RoutedEventArgs e) {
        if(cacheMonitor!=null&&!cacheMonitor.HasExited){StopCacheMonitor();Log("Cache monitor stopped.");return;}
        if(selected?.Supported!=true){GlobalNotice.Title="Select a pinned DX12 profile";GlobalNotice.Message="Launch this game in DX12 first so its executable can be detected.";GlobalNotice.IsOpen=true;return;}
        string exe=selected.SupportedExecutable!;
        await Run("Starting the read-only cache overlay…",async()=>{
            StopCacheMonitor();var info=new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"ArcCache.SafetyEngine.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            info.ArgumentList.Add("--cache-monitor");info.ArgumentList.Add(exe);cacheMonitor=Process.Start(info)??throw new IOException("The cache monitor could not start.");
            await Task.Delay(1000);
            if(cacheMonitor.HasExited){string result=await cacheMonitor.StandardOutput.ReadToEndAsync();StopCacheMonitor();throw new IOException("Monitor could not start: "+result);}
            MonitorButton.Content="Stop cache monitor";Log("Overlay started: SHARED Intel folder, attribution unverified. Net disk growth since monitor start; no shader compilation count. Borderless/windowed display required; CSV saved under Activity logs.");
        });
    }
    async void LaunchGame(object sender,RoutedEventArgs e) {
        if(selected==null)return;SteamGame game=selected;uint id=game.AppId;
        await Run("Checking this game before opening Steam…",async()=>{
            if(game.SupportedExecutable!=null) {
                var state=GameSetupState.FromReply(await Engine(new("GameState",game.SupportedExecutable,store.Receipt(id))));
                if(state.FolderExists) {
                    if(!routingReady)throw new InvalidOperationException("Enable Use private drivers in Settings and restart Windows before launching this private setup. To play with the system driver, use Use system driver again first.");
                    if(!game.Supported)throw new InvalidOperationException("Game executable changed. Return its private set to stock before launching for rediscovery.");
                    if(state.CompatibilityRepairRequired)throw new InvalidOperationException("This private setup needs a compatibility update. Use system driver again, then prepare a fresh original 512 MiB copy before launching.");
                    if(!state.Configured)throw new InvalidOperationException("Existing private setup is unverified. Import its recovery record or recover it before launching through this app.");
                    var assessment=(await Engine(new("Safety",SteamAppId:id,InstallDirectory:game.InstallDirectory))).Data;
                    if(!assessment.GetProperty("Allowed").GetBoolean()&&!await Confirm("High-risk private-driver launch","This game may have multiplayer or anti-cheat. Playing with a modified driver can cause account penalties or launch failures. Use the original driver for online play.","Launch anyway",requireRisk:true))return;
                }
            }
            Process.Start(new ProcessStartInfo("steam://rungameid/"+id){UseShellExecute=true});Log("Steam launch requested. Launching is not proof of driver loading.");
        });
    }
    async void ImportSetup(object sender,RoutedEventArgs e) {
        if(selected==null)return;uint id=selected.AppId;
        var picker=new FileOpenPicker();picker.FileTypeFilter.Add(".json");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));var file=await picker.PickSingleFileAsync();if(file==null)return;
        await Run("Verifying an existing recovery receipt…",async()=>{
            await Engine(new("Import",selected!.SupportedExecutable,store.Receipt(id),Confirmed:true,SourceReceipt:file.Path,SteamAppId:id,InstallDirectory:selected.InstallDirectory));await ShowGame();
        });
    }
    async void ChooseSteamFolder(object sender,RoutedEventArgs e) {
        var picker=new FolderPicker();picker.FileTypeFilter.Add("*");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));var folder=await picker.PickSingleFolderAsync();if(folder==null)return;steamRoot=folder.Path;
        await Run("Reading the selected Steam folder…",LoadLibrary);
    }
    async void EnableRouting(object sender,RoutedEventArgs e) {
        if(!await Confirm("Enable private drivers","Required for per-game cache limits. This one-time Windows setting lets games use the private copies beside their executable. It also affects other apps with .local setups. Your installed Intel driver stays unchanged; Windows integrity enforcement stays enabled. Run this app as administrator yourself, confirm here, then restart Windows. The previous setting is saved for recovery.","Enable private drivers"))return;
        await Run("Enabling developer routing with explicit consent…",async()=>{await Engine(new("EnableOverride",Confirmed:true));await LoadDriver();});
    }
    async void RestoreRouting(object sender,RoutedEventArgs e) {
        if(!await Confirm("Restore the previous Windows setting","For one game, use Use system driver again on its game page instead. To restore the Windows setting for all apps, remove your private game setups first. Run this app as administrator yourself and restart Windows afterward. This restores the saved previous value, which may already have been enabled. It does not delete shader caches.","Restore previous value"))return;
        await Run("Restoring the previous developer setting…",async()=>{await Engine(new("RestoreOverride",Confirmed:true));await LoadDriver();});
    }
    async void ChooseScsCli(object sender,RoutedEventArgs e) {
        var picker=new FileOpenPicker();picker.FileTypeFilter.Add(".exe");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));var file=await picker.PickSingleFileAsync();if(file==null)return;
        await Run("Connecting the selected SCSKiller CLI…",()=>{scs!.SelectCli(file.Path);ScsPath.Text=scs.CliPath;Log("SCSKiller CLI connected; no commands run.");return Task.CompletedTask;});
    }
    async void ScsStatus(object sender,RoutedEventArgs e)=>await RunScs("status");
    async void ScsCompile(object sender,RoutedEventArgs e)=>await RunScs("compile");
    void CancelScs(object sender,RoutedEventArgs e)=>scsCancellation?.Cancel();
    async Task RunScs(string action) {
        if(selected==null||scs==null||busy)return;SteamGame game=selected;
        if(action=="compile"&&setupState?.Configured==true&&selectedReceipt?.MiB>512){
            GlobalNotice.Severity=InfoBarSeverity.Informational;GlobalNotice.Title="Custom-driver compilation is not connected";
            GlobalNotice.Message="Your private patch remains installed. This compiler uses the 512 MiB system driver, so compilation is disabled for this setup.";GlobalNotice.IsOpen=true;return;
        }
        string message=action=="compile"?$"Compile {game.Name} through your selected SCSKiller CLI? Close the game first. This uses the ORIGINAL installed Intel driver and its 512 MiB default, not this game's private limit. Warming may evict existing cache entries. Community recordings are downloaded only when enabled and your existing SCSKiller account has access; no recording is guaranteed for this game.":"Read this game's SCSKiller status? The CLI scans the library and may sync community recordings according to your existing SCSKiller settings.";
        message+="\n\nIntel support is experimental and enabled only in this child process. SCSKiller's own compatibility and anti-cheat checks still apply. No recorder or DLL is installed by this bridge. This connection does not upload recordings or sign you in.";
        if(!await Confirm(action=="compile"?"Compile shaders · system driver (512 MiB)":"Check SCSKiller status",message,action=="compile"?"Compile":"Check"))return;
        await Run("SCSKiller · "+action+" in progress…",async()=>{
            if(action=="compile"&&game.SupportedExecutable!=null) {
                foreach(var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(game.SupportedExecutable)))using(process)throw new InvalidOperationException("Close the selected game before compiling.");
            }
            scsCancellation=new CancellationTokenSource();ScsCancelButton.Visibility=Visibility.Visible;
            try {
                int code=await scs.RunAsync(action,game.AppId,line=>DispatcherQueue.TryEnqueue(()=>Log(line)),scsCancellation.Token);
                if(code!=0)throw new InvalidOperationException($"SCSKiller exited with code {code}. See Activity for its reason; no private-driver files were changed.");
                Log("SCSKiller completed. Stock-driver warming is not verification of the private cap. Cache contents are not backed up by this app.");
            }finally{ScsCancelButton.Visibility=Visibility.Collapsed;scsCancellation.Dispose();scsCancellation=null;}
        });
    }
}
