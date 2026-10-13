using ArcCache.Core;
using System.Text.Json;

int passed=0;
void Check(bool yes,string message){if(!yes)throw new Exception(message);}
void Test(string name,Action body){body();passed++;Console.WriteLine("PASS "+name);}
void Reject(Action body){try{body();}catch(InvalidDataException){return;}throw new Exception("Expected rejection.");}
Test("Opt-in diagnostics are local, persist preference, rotate and stop when disabled",()=>{
    string root=Path.Combine(Path.GetTempPath(),"ArcDiagnostics-"+Guid.NewGuid().ToString("N"));
    try {
        var log=new DiagnosticLog(root);log.Write("off",new{Value=1});Check(!File.Exists(log.Path),"Disabled logging wrote data.");
        log.SetEnabled(true);log.Write("fixture",new{Action="Status"});
        Check(File.ReadLines(log.Path).All(line=>JsonDocument.Parse(line).RootElement.TryGetProperty("utc",out _)),"Invalid JSON lines.");
        Check(new DiagnosticLog(root).Enabled,"Preference was not retained.");
        File.WriteAllText(log.Path,new string('x',2*1024*1024));log.Write("rotate",new{Value=1});
        Check(File.Exists(log.Path+".previous")&&new FileInfo(log.Path).Length<1024,"Diagnostics failed to rotate.");
        log.SetEnabled(false);long size=new FileInfo(log.Path).Length;log.Write("off",new{Value=2});Check(new FileInfo(log.Path).Length==size&&!new DiagnosticLog(root).Enabled,"Disabled log kept recording.");
        Check(new DiagnosticLog(root,true).Enabled,"--debug session mode ignored.");
        Check(!new DiagnosticLog(root).Enabled,"Session mode persisted itself.");
    }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
});

Test("Inactive routing preserves the saved limit and gives an enable action",()=>{
    var receipt=new ReceiptState(3,"game.exe","game.exe.local",4096,"changed-runtime-unverified",new());
    var ui=SetupPresentation.From(new GameSetupState(true,true,true,true,receipt,null),true,false);
    Check(ui.Installed.StartsWith("4 GiB saved")&&ui.Title=="Enable private drivers"&&ui.Next.Contains("restart Windows"),"Inactive routing lost saved cap or reported ready.");
});
Test("Routing statuses distinguish off, on and pending restart",()=>{
    Check(RoutingPresentation.Status(false,false).StartsWith("Off"),"Disabled routing shown as ready.");
    Check(RoutingPresentation.Status(true,false).StartsWith("On"),"Enabled routing shown as disabled.");
    Check(RoutingPresentation.Status(true,true).StartsWith("Restart required"),"Pending enable shown as ready.");
    Check(RoutingPresentation.Status(false,true).StartsWith("Restart required"),"Pending restore shown as completed.");
});

Test("Installed wide cap is distinct from unmeasured runtime behavior",()=>{
    var receipt=new ReceiptState(3,"game.exe","game.exe.local",4096,"changed-runtime-unverified",new());
    var state=new GameSetupState(true,true,true,true,receipt,null);
    var ui=SetupPresentation.From(state,true);
    Check(ui.Installed.StartsWith("4 GiB installed")&&ui.Title=="Ready to play"&&!ui.WaitingForControl,"Applied patch presented as missing or waiting for original control.");
    Check(ui.Next.Contains("Play through Steam")&&!ui.WaitingForControl,"Installed patch still shown as needing setup.");
    Check(SetupPresentation.From(state,false).Installed.StartsWith("4 GiB installed"),"Stale executable erased verified installed limit.");
    Check(SetupPresentation.From(state with{ContentsValid=false,Error="Hash mismatch"},true).Title=="Recover existing setup","Invalid disk files shown as applied.");
});
Test("Larger setup distinguishes an experimental test from inactive routing",()=>{
    var receipt=new ReceiptState(4,"game.exe","game.exe.local",8192,"changed-runtime-unverified",new());
    var state=new GameSetupState(true,true,true,true,receipt,null);
    Check(SetupPresentation.From(state,true).Title=="Ready for experimental test","Untested larger patch was presented as proven gameplay.");
    var inactive=SetupPresentation.From(state,true,false);
    Check(inactive.Installed.StartsWith("8 GiB saved")&&inactive.Installed.Contains("inactive")&&inactive.Next.Contains("restart"),"Disabled routing was presented as an active installed patch.");
});
Test("Updated driver preserves saved cap but requires review",()=>{
    var receipt=new ReceiptState(4,"game.exe","game.exe.local",8192,"changed-runtime-unverified",new());
    var state=new GameSetupState(true,true,true,true,receipt,null);
    var ui=SetupPresentation.From(state,true,true,false);
    Check(ui.Installed.Contains("8 GiB saved")&&ui.Title=="Review the updated driver"&&ui.Next.Contains("system driver"),"Driver update erased setup or presented outdated private driver as ready.");
});
Test("Old private setup needing compatibility migration blocks patching without erasing disk state",()=>{
    var receipt=new ReceiptState(3,"game.exe","game.exe.local",4096,"changed-runtime-unverified",new());
    var state=new GameSetupState(true,true,true,true,receipt,null,true);
    Check(!state.Configured&&!state.CanPrepare&&state.RecordValid,"Unsafe existing setup allowed patching or duplicate preparation.");
    var ui=SetupPresentation.From(state,true);Check(ui.Installed.StartsWith("4 GiB installed")&&ui.Next.Contains("fresh original 512 MiB"),"Migration erased installation status or failed to give recovery steps.");
});
Test("Setup workflow advances from detection through control to patch",()=>{
    Check(SetupPresentation.From(null,false).Title.Contains("Detect"),"Missing detection step.");
    var empty=new GameSetupState(false,false,false,false,null,null);
    Check(SetupPresentation.From(empty,true).Title.Contains("Prepare"),"Missing prepare step.");
    var receipt=new ReceiptState(3,"game.exe","game.exe.local",512,"prepared-runtime-unverified",new());
    var state=new GameSetupState(true,true,true,true,receipt,null);
    Check(SetupPresentation.From(state,true).WaitingForControl,"Original control check bypassed.");
    Check(SetupPresentation.From(state with{Receipt=receipt with{Status="original-control-loading-verified"}},true).Title.Contains("Step 3"),"Verified control still waiting.");
    Check(SetupPresentation.From(state with{FolderExists=false,Receipt=receipt with{Status="removed"}},true).Title.Contains("Prepare"),"Removed setup shown as installed.");
});
Test("Steam artwork resolves flat, direct app-ID and hashed folder assets with icon fallbacks",()=>{
    string root=Path.Combine(Path.GetTempPath(),"ArcArtwork-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
    byte[] png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jN9sAAAAASUVORK5CYII=");
    string cache=Path.Combine(root,"appcache","librarycache");Directory.CreateDirectory(cache);
    void Image(string relative){string path=Path.Combine(cache,relative);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path,png);}
    try {
        Image("1_library_600x900.png");Image("1_icon.png");var flat=SteamArtwork.Find(root,1);Check(flat.Portraits.Count==1&&flat.Icons.Count==1,"Legacy layout missed.");
        Image(Path.Combine("2","library_600x900.jpg"));Image(Path.Combine("2",new string('a',40)+".jpg"));var direct=SteamArtwork.Find(root,2);Check(direct.Portraits.Count==1&&direct.Icons.Count==1,"Direct modern layout or hashed icon missed.");
        Image(Path.Combine("3",new string('b',40),"library_capsule.jpg"));Image(Path.Combine("3",new string('c',40),"library_header.jpg"));var hashed=SteamArtwork.Find(root,3);Check(hashed.Portraits.Count==2&&hashed.Portraits[0].EndsWith("library_capsule.jpg"),"Portrait priority or header fallback wrong.");
        File.WriteAllText(Path.Combine(cache,"2","library_600x900.jpg"),"broken artwork");Image(Path.Combine("2","header.png"));Check(SteamArtwork.Find(root,2).Portraits.Single().EndsWith("header.png"),"Corrupt leading file blocked fallback.");
        Check(SteamArtwork.Find(root,999).Portraits.Count==0,"Missing artwork guessed.");
        var game=new SteamGame(3,"Fixture",root,hashed.Portraits[0],ArtworkFallbacks:hashed.Portraits.Skip(1).ToArray());Check(game.IconSources.SequenceEqual(game.ArtworkSources)&&game.ArtworkSources.Count==2,"Icon has no portrait fallback.");
    }finally{Directory.Delete(root,true);}
});
Test("Safety warnings stay brief while retaining multiplayer, anti-cheat and unknown risk",()=>{
    JsonElement Assessment(bool allowed,string? marker,string reason,string multi)=>JsonSerializer.SerializeToElement(new{Allowed=allowed,Marker=marker,Message=reason,Multiplayer=multi});
    Check(!SafetyPresentation.From(Assessment(true,null,"Clear","Steam reports single-player")).Warn,"Positive scan creates warning clutter.");
    foreach(var data in new[]{Assessment(false,"EasyAntiCheat","Indicator found","Unknown"),Assessment(false,null,"Steam declares multiplayer/co-op","Steam reports multiplayer/co-op"),Assessment(false,null,"Unavailable","Unknown")}){
        var notice=SafetyPresentation.From(data);Check(notice.Warn&&notice.Message.Contains("account penalties")&&notice.Message.Contains("original driver")&&notice.Message.Length<200,"Risk or next action lost in simplified message.");
    }
    Check(SafetyPresentation.From(Assessment(false,null,"Steam declares Valve Anti-Cheat","Unknown")).Title=="Anti-cheat detected","VAC declaration treated as generic multiplayer.");
});
Test("SCSKiller bridge uses selected Steam ID, child-only Intel opt-in and no shell",()=>{
    var command=ScsKillerBridge.Command(Path.Combine(Path.GetTempPath(),"scskiller.exe"),"compile",292030);
    Check(command.ArgumentList.SequenceEqual(new[]{"compile","steam:292030","--community"})&&!command.UseShellExecute&&command.RedirectStandardInput&&command.Verb=="","Unexpected bridge command or escalation.");
    Check(command.Environment["SCSKILLER_EXPERIMENTAL_INTEL"]=="1","Missing explicit child opt-in.");
    bool rejected=false;try{ScsKillerBridge.Command(command.FileName,"record",292030);}catch(ArgumentException){rejected=true;}Check(rejected,"Recorder installation must not be exposed through bridge.");
});
Test("KeyValues comments, escaped paths and nested objects",()=>{
    string text="// comment\n\"libraryfolders\" { \"0\" { \"path\" \"D:\\\\SteamLibrary\" } \"1\" { \"path\" \"E:\\\\Steam Games\" } }";
    Check(KeyValues.Values(text,"path").SequenceEqual(new[]{@"D:\SteamLibrary",@"E:\Steam Games"}),"Paths parsed incorrectly.");
    Reject(()=>KeyValues.Values("\"x\" { \"y\" \"unterminated","x"));
    Reject(()=>KeyValues.Values("\"x\" {","x"));
    Check(KeyValues.Values("\"name\" \"}\"","name").Single()=="}","Quoted brace treated as structure.");
});
Test("Metadata nesting and size bounds",()=>{
    Reject(()=>KeyValues.Values(new string('a',4*1024*1024+1),"x"));
    string deep=string.Concat(Enumerable.Repeat("\"x\" { ",66))+string.Concat(Enumerable.Repeat("} ",66));Reject(()=>KeyValues.Values(deep,"x"));
});
Test("Steam multiple libraries, install flags, duplicate IDs and traversal",()=>{
    string root=Path.Combine(Path.GetTempPath(),"ArcSteamTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
    try {
        string library=Path.Combine(root,"external");Directory.CreateDirectory(Path.Combine(root,"steamapps"));Directory.CreateDirectory(Path.Combine(library,"steamapps","common","The Witcher 3"));
        File.WriteAllText(Path.Combine(root,"steamapps","libraryfolders.vdf"),"\"libraryfolders\" { \"0\" { \"path\" \""+library.Replace("\\","\\\\")+"\" } }");
        string Manifest(uint id,string folder,string flags="4")=>$"\"AppState\" {{ \"appid\" \"{id}\" \"name\" \"Fixture {id}\" \"installdir\" \"{folder}\" \"StateFlags\" \"{flags}\" }}";
        File.WriteAllText(Path.Combine(library,"steamapps","appmanifest_292030.acf"),Manifest(292030,"The Witcher 3"));
        File.WriteAllText(Path.Combine(library,"steamapps","appmanifest_2.acf"),Manifest(2,"The Witcher 3","2"));
        File.WriteAllText(Path.Combine(library,"steamapps","appmanifest_3.acf"),Manifest(3,"../.."));
        File.WriteAllText(Path.Combine(library,"steamapps","appmanifest_4.acf"),"broken {");
        var result=SteamLibrary.Discover(root);Check(result.Games.Count==1&&!result.Games[0].Supported,"Installed games need runtime detection, not a title whitelist.");Check(result.Warnings.Count==1,"Malformed manifest should produce a warning.");
        Check(!new SteamGame(123,"Other",root,null).Supported,"Unsupported game enabled.");
        Check(new SteamGame(123,"Other",root,null,Path.Combine(root,"game.exe"),true).Supported,"Observed games should not require an app-ID whitelist.");
    }finally{Directory.Delete(root,true);}
});
if(args.Length>0) {
    var safety=new SafetyClient(Path.GetFullPath(args[0]));
    Test("Worker refuses unknown operations without changes",()=>{var reply=safety.RunAsync(new("UnknownOperation")).GetAwaiter().GetResult();Check(!reply.Ok&&reply.Message.Contains("Unknown action"),"Unknown operation accepted.");});
    Test("Worker refuses all mutation kinds without explicit confirmation",()=>{foreach(string action in new[]{"Prepare","Change","Remove","Import","EnableOverride","RestoreOverride"}){var reply=safety.RunAsync(new(action)).GetAwaiter().GetResult();Check(!reply.Ok&&reply.Message.Contains("Explicit confirmation"),"Mutation accepted without confirmation: "+action);}});
    Test("Automatic discovery never guesses an executable for a stopped unknown title",()=>{
        string root=Path.Combine(Path.GetTempPath(),"ArcDiscovery-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try{File.Copy(Environment.ProcessPath!,Path.Combine(root,"possible-game.exe"));var result=safety.RunAsync(new("DiscoverGame",SteamAppId:987654,InstallDirectory:root)).GetAwaiter().GetResult();Check(result.Ok&&!result.Data.GetProperty("Validated").GetBoolean()&&result.Data.GetProperty("Executable").ValueKind==JsonValueKind.Null,"A static executable filename was mistaken for a rendering process.");Check(!Directory.Exists(Path.Combine(root,"possible-game.exe.local")),"Discovery modified the game.");}finally{Directory.Delete(root,true);}
    });
    Test("Fresh worker safety scan blocks nested anti-cheat even for reviewed single-player",()=>{
        string root=Path.Combine(Path.GetTempPath(),"ArcSafetyWorker-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"bin","x64_dx12"));Directory.CreateDirectory(Path.Combine(root,"nested","EasyAntiCheat"));
        try {
            var reply=safety.RunAsync(new("Safety",SteamAppId:292030,InstallDirectory:root)).GetAwaiter().GetResult();Check(reply.Ok&&!reply.Data.GetProperty("Allowed").GetBoolean()&&reply.Data.GetProperty("Marker").GetString()=="EasyAntiCheat","Anti-cheat did not override profile eligibility.");
            string exe=Path.Combine(root,"bin","x64_dx12","witcher3.exe"),receipt=Path.Combine(root,"receipt.json");File.WriteAllText(exe,"temporary fixture");
            reply=safety.RunAsync(new("Prepare",exe,receipt,Confirmed:true)).GetAwaiter().GetResult();Check(!reply.Ok&&reply.Message.Contains("Anti-cheat indicator")&&!Directory.Exists(exe+".local")&&!File.Exists(receipt),"Direct worker request bypassed safety or wrote setup files.");
            reply=safety.RunAsync(new("Prepare",exe,receipt,Confirmed:true,RiskConfirmed:true)).GetAwaiter().GetResult();Check(!reply.Ok&&reply.Message.Contains("Unsupported Witcher executable")&&!Directory.Exists(exe+".local")&&!File.Exists(receipt),"Risk consent should pass the warning but never bypass executable validation.");
            if(args.Length>=2) {
                var live=JsonSerializer.Deserialize<ReceiptState>(File.ReadAllText(args[1]))!;File.WriteAllText(receipt,JsonSerializer.Serialize(live with{Executable=exe,Folder=exe+".local"}));
                reply=safety.RunAsync(new("Change",ReceiptPath:receipt,MiB:1024,Confirmed:true)).GetAwaiter().GetResult();Check(!reply.Ok&&reply.Message.Contains("Anti-cheat indicator"),"Change bypassed the fresh safety scan.");
            }
        }finally{Directory.Delete(root,true);}
    });
    Test("Worker safety blocks an unavailable install",()=>{var reply=safety.RunAsync(new("Safety",SteamAppId:292030,InstallDirectory:Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")))).GetAwaiter().GetResult();Check(reply.Ok&&!reply.Data.GetProperty("Allowed").GetBoolean()&&!reply.Data.GetProperty("ScanComplete").GetBoolean(),"Missing install classified as safe.");});
    Test("Worker validates existing private set READ ONLY",()=>{
        if(args.Length<2)return;var reply=safety.RunAsync(new("Validate",ReceiptPath:Path.GetFullPath(args[1]))).GetAwaiter().GetResult();Check(reply.Ok,"Live receipt validation failed: "+reply.Message);
        var expected=JsonSerializer.Deserialize<ReceiptState>(File.ReadAllText(args[1]))!;Check(reply.Data.GetProperty("MiB").GetInt32()==expected.MiB,"Worker cap differs from validated receipt.");
    });
    Test("Worker requires automatic original-control observation even with patch consent",()=>{
        if(args.Length<2)return;string temporary=Path.Combine(Path.GetTempPath(),"ArcControlGate-"+Guid.NewGuid()+".json");
        try {
            var source=JsonSerializer.Deserialize<ReceiptState>(File.ReadAllText(args[1]))!;var hashes=new Dictionary<string,string>(source.Files);hashes["igd12um64xe2.dll"]="1A552E471AE986A52D045C4112E1980FB7171AABEBFF93C027A4BBA10A3C6229";
            File.WriteAllText(temporary,JsonSerializer.Serialize(source with{MiB=512,Files=hashes,Status="prepared-runtime-unverified"}));
            var result=safety.RunAsync(new("Change",ReceiptPath:temporary,MiB:2048,Confirmed:true,RiskConfirmed:true)).GetAwaiter().GetResult();
            Check(!result.Ok&&result.Message.Contains("checked automatically"),"User consent bypassed the original-loading gate.");
        }finally{File.Delete(temporary);}
    });
    Test("Original-control check never verifies a stopped copied executable or changes its receipt",()=>{
        if(args.Length<2)return;string root=Path.Combine(Path.GetTempPath(),"ArcControlStopped-"+Guid.NewGuid());Directory.CreateDirectory(Path.Combine(root,"bin","x64_dx12"));
        try {
            var source=JsonSerializer.Deserialize<ReceiptState>(File.ReadAllText(args[1]))!;string exe=Path.Combine(root,"bin","x64_dx12","witcher3.exe");File.Copy(source.Executable,exe);
            var hashes=new Dictionary<string,string>(source.Files);hashes["igd12um64xe2.dll"]="1A552E471AE986A52D045C4112E1980FB7171AABEBFF93C027A4BBA10A3C6229";
            string receipt=Path.Combine(root,"receipt.json"),before=JsonSerializer.Serialize(source with{Executable=exe,Folder=exe+".local",MiB=512,Files=hashes});File.WriteAllText(receipt,before);
            var result=safety.RunAsync(new("VerifyControl",ReceiptPath:receipt)).GetAwaiter().GetResult();Check((!result.Ok||!result.Data.GetProperty("Verified").GetBoolean())&&File.ReadAllText(receipt)==before,"Stopped/unrelated process falsely verified the original control.");
        }finally{Directory.Delete(root,true);}
    });
    Test("Worker inspects original system driver READ ONLY",()=>{var reply=safety.RunAsync(new("Status")).GetAwaiter().GetResult();Check(reply.Ok&&reply.Data.GetProperty("Original").GetBoolean(),"Original system driver was not verified.");});
    Test("Worker refuses a missing recovery receipt",()=>{var reply=safety.RunAsync(new("Validate",ReceiptPath:Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json"))).GetAwaiter().GetResult();Check(!reply.Ok,"Missing receipt accepted.");});
    Test("Game state recognizes the validated live cap and blocks duplicate preparation",()=>{
        if(args.Length<2)return;
        var validated=safety.RunAsync(new("Validate",ReceiptPath:Path.GetFullPath(args[1]))).GetAwaiter().GetResult();string exe=validated.Data.GetProperty("Executable").GetString()!;
        var reply=safety.RunAsync(new("GameState",exe,Path.GetFullPath(args[1]))).GetAwaiter().GetResult();var state=GameSetupState.FromReply(reply);
        Check(state.Configured&&state.Receipt?.MiB==validated.Data.GetProperty("MiB").GetInt32()&&!state.CanPrepare,"Existing setup misclassified as stock.");
        var missing=safety.RunAsync(new("GameState",exe,Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json"))).GetAwaiter().GetResult();var orphan=GameSetupState.FromReply(missing);
        Check(!orphan.Configured&&!orphan.CanPrepare&&orphan.Error!=null,"Untracked existing folder incorrectly allowed setup.");
    });
    Test("Selected-game observation reports a stopped game instead of unrelated processes",()=>{
        string missing=Path.Combine(Path.GetTempPath(),"ArcCacheMissingGame-"+Guid.NewGuid()+".exe");
        var reply=safety.RunAsync(new("ObserveGame",missing)).GetAwaiter().GetResult();Check(reply.Ok&&reply.Message.Contains("not running")&&!reply.Message.Contains("msedge"),"Stopped game observation is misleading.");
    });
}
Console.WriteLine($"Passed {passed} checks. Steam fixtures were temporary; live checks were read-only.");
if(args.Length>=3) {
    Test("SCSKiller bridge persists selection, streams both channels, propagates failure and cancels owned process",()=>{
        string root=Path.Combine(Path.GetTempPath(),"ArcBridgeTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            string cli=Path.Combine(root,"scskiller.exe");File.Copy(args[2],cli);
            var bridge=new ScsKillerBridge(Path.Combine(root,"bridge.json"));bridge.SelectCli(cli);bridge=new ScsKillerBridge(Path.Combine(root,"bridge.json")).Load();
            var lines=new System.Collections.Concurrent.ConcurrentBag<string>();
            int code=bridge.RunAsync("status",292030,lines.Add,CancellationToken.None).GetAwaiter().GetResult();
            Check(code==0&&lines.Any(l=>l.Contains("fixture:status|steam:292030"))&&lines.Any(l=>l.Contains("intel=1")),"Wrong command or lost output.");
            Check(bridge.RunAsync("compile",2,lines.Add,CancellationToken.None).GetAwaiter().GetResult()==7,"Failure code suppressed.");
            using var cancel=new CancellationTokenSource(500);bool cancelled=false;
            try{bridge.RunAsync("status",1,lines.Add,cancel.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}
            Check(cancelled,"Cancellation was not reported.");
        }finally{Directory.Delete(root,true);}
    });
    Console.WriteLine($"Passed {passed} total checks including bridge process lifecycle.");
}
