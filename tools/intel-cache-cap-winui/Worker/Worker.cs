using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using ArcCap;

class Request {
    public bool RiskConfirmed {get;set;}
    public bool ReadOnly {get;set;}
    public uint SteamAppId {get;set;}public string InstallDirectory {get;set;}
    public string Action {get;set;}public string Executable {get;set;}public string ReceiptPath {get;set;}public string SourceReceipt {get;set;}public string DriverFolder {get;set;}public int MiB {get;set;}public bool Confirmed {get;set;}
}
class Worker {
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]static extern ulong GetTickCount64();
    [System.Runtime.InteropServices.DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
    [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]static extern bool QueryFullProcessImageName(IntPtr process,uint flags,System.Text.StringBuilder path,ref uint length);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
    static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
    static GameSafetyReport Safety(Request r) {
        var report=GameSafety.Assess(r.SteamAppId,r.InstallDirectory);
        if(!report.ScanComplete||report.Marker!=null)return report;
        try {
            System.Net.ServicePointManager.SecurityProtocol=System.Net.SecurityProtocolType.Tls12;
            var request=(System.Net.HttpWebRequest)System.Net.WebRequest.Create("https://store.steampowered.com/api/appdetails?appids="+r.SteamAppId.ToString(System.Globalization.CultureInfo.InvariantCulture)+"&l=english&filters=basic,categories");
            request.Timeout=5000;request.ReadWriteTimeout=5000;request.UserAgent="ArcCache-Safety/1.0";
            string raw;using(var response=request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream())){char[] buffer=new char[262145];int count=0,n;while(count<buffer.Length&&(n=reader.Read(buffer,count,buffer.Length-count))>0)count+=n;if(count>262144)throw new InvalidDataException("Steam response too large.");raw=new string(buffer,0,count);}
            var data=Json.Deserialize<Dictionary<string,object>>(raw);var app=(Dictionary<string,object>)data[r.SteamAppId.ToString(System.Globalization.CultureInfo.InvariantCulture)];
            if(!Convert.ToBoolean(app["success"]))throw new InvalidDataException("Steam metadata unavailable.");
            var details=(Dictionary<string,object>)app["data"];if(Convert.ToUInt32(details["steam_appid"])!=r.SteamAppId)throw new InvalidDataException("Steam app identity mismatch.");
            bool single=false,multi=false,vac=false;var categories=(System.Collections.IEnumerable)details["categories"];
            foreach(Dictionary<string,object> category in categories) {
                int id=Convert.ToInt32(category["id"]);string label=(string)category["description"];single|=id==2;vac|=id==8;
                multi|=new[]{1,9,20,27,36,37,38,39,44,49}.Contains(id)||System.Text.RegularExpressions.Regex.IsMatch(label,"multi.?player|co.op|pvp|mmo",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            report.SteamStatus="Live Steam categories checked at "+DateTime.UtcNow.ToString("o")+". Store declarations are not a complete anti-cheat inventory.";
            report.Multiplayer=multi?"Steam reports multiplayer/co-op":single?"Steam reports single-player":"Unknown";
            if(vac){report.Allowed=false;report.Message="Steam declares Valve Anti-Cheat; explicit risk acknowledgment is required.";}
            else if(multi){report.Allowed=false;report.Message="Steam declares multiplayer/co-op; explicit risk acknowledgment is required.";}
            else if(!single){report.Allowed=false;report.Message="Steam game mode is unknown; explicit risk acknowledgment is required.";}
            else {report.Allowed=true;report.Message="Steam reports single-player; no listed anti-cheat indicators found. Detection is incomplete and does not guarantee safety.";}
        }catch(Exception){report.Allowed=false;report.SteamStatus="Steam metadata unavailable or unreadable; explicit risk acknowledgment is required.";if(report.Marker==null)report.Message=report.SteamStatus;}
        return report;
    }
    static void RequirePatchSafety(string executable,bool riskConfirmed) {
        var report=Safety(new Request{SteamAppId=MatchedSet.Profile(executable).AppId,InstallDirectory=MatchedSet.GameRoot(executable)});
        if(!report.Allowed&&!riskConfirmed)throw new InvalidOperationException("Risk acknowledgment required: "+report.Message+" Anti-cheat, multiplayer and incomplete checks may cause launch failures or account penalties. No changes made.");
    }
    static void Consent(Request r){if(r.ReadOnly)throw new InvalidOperationException("Read-only requests cannot change game or Windows files.");if(!r.Confirmed)throw new InvalidOperationException("Explicit confirmation is required before this action.");}
    static void RequireOriginalActivePackage() {
        string reason;if(!DriverCatalog.CohortQualified(DriverCatalog.Selected(),out reason))throw new InvalidDataException("Driver compatibility review required: "+reason);
    }
    static void RequireNoKnownPrivateSets() {
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"IntelArcCacheCap","games");
        if(!Directory.Exists(root))return;
        foreach(string file in Directory.GetFiles(root,"steam-*.json")) {
            var receipt=MatchedSet.Read(file);if(Directory.Exists(receipt.Folder))throw new InvalidOperationException("Remove configured private game sets before restoring developer routing. Existing set: "+receipt.Folder);
        }
    }
    static object GameState(Request r) {
        string full=Path.GetFullPath(r.Executable),folder=full+".local";bool exists=File.Exists(r.ReceiptPath),present=Directory.Exists(folder)||File.Exists(folder),recordValid=false,contentsValid=false,compatibilityRepairRequired=false;
        MatchedSetReceipt receipt=null;string error=null;
        try {
            if(exists) {
                receipt=MatchedSet.Read(r.ReceiptPath);
                if(!Path.GetFullPath(receipt.Executable).Equals(full,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Receipt belongs to another game installation.");
                recordValid=true;
                if(receipt.Status=="removed"&&!present)contentsValid=true;
                else {MatchedSet.ValidateContents(receipt);contentsValid=true;compatibilityRepairRequired=receipt.Schema!=4&&MatchedSet.NeedsDebugRoutingFix(receipt.Executable);}
            }else if(present)error="A private folder exists without this app's recovery record. Import the original matched-set receipt; do not create another setup.";
        }catch(Exception e){error=e.Message;}
        if(!exists&&present) {
            try {using(var input=File.OpenRead(r.ReceiptPath)){} }
            catch(Exception e){error+=" Receipt read: "+e.GetType().Name+": "+e.Message;}
        }
        return new {ReceiptExists=exists,FolderExists=present,RecordValid=recordValid,ContentsValid=contentsValid,Receipt=recordValid?receipt:null,Error=error,CompatibilityRepairRequired=compatibilityRepairRequired};
    }
    static void RequireAmd64Exe(string path) {
        Engine.CheckPath(path);using(var input=new BinaryReader(File.OpenRead(path))){if(input.BaseStream.Length<256||input.ReadUInt16()!=0x5A4D)throw new InvalidDataException("Not a PE executable.");input.BaseStream.Position=0x3c;uint header=input.ReadUInt32();if(header>input.BaseStream.Length-26)throw new InvalidDataException("Invalid PE header.");input.BaseStream.Position=header;if(input.ReadUInt32()!=0x4550||input.ReadUInt16()!=0x8664)throw new InvalidDataException("Only AMD64 game executables are supported.");input.BaseStream.Position=header+24;if(input.ReadUInt16()!=0x20b)throw new InvalidDataException("Expected PE32+ game.");}
    }
    static object DiscoverGame(Request r) {
        if(r.SteamAppId==0)throw new InvalidDataException("Steam app identity required.");
        string root=Path.GetFullPath(r.InstallDirectory).TrimEnd(Path.DirectorySeparatorChar);Engine.CheckPath(root);
        var previous=MatchedSet.ReadProfile(r.SteamAppId);string previousExe=null;
        if(previous==null&&r.ReceiptPath!=null&&File.Exists(r.ReceiptPath)) {
            var recovered=MatchedSet.Read(r.ReceiptPath);
            if(recovered.Game!=null&&recovered.Game.AppId==r.SteamAppId&&Path.GetFullPath(recovered.Game.Root).Equals(root,StringComparison.OrdinalIgnoreCase)){if(!r.ReadOnly)MatchedSet.SaveProfile(recovered.Game);previous=recovered.Game;}
        }
        if(previous==null&&r.SteamAppId==292030) {
            string legacy=Path.Combine(root,@"bin\x64_dx12\witcher3.exe");
            if(File.Exists(legacy)&&IsolationControl.FileHash(legacy)==MatchedSet.GameHash)return new{Validated=true,Executable=legacy,Message="Existing DX12 profile recognized; executable verified."};
            if(Directory.Exists(legacy+".local"))return new{Validated=false,Executable=legacy,Message="The game updated. Use the system driver again, then launch DX12 for automatic detection."};
        }
        if(previous!=null) {
            previousExe=MatchedSet.ProfileExecutable(previous);
            if(!Path.GetFullPath(previous.Root).Equals(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Game installation moved; previous profile must be recovered before rediscovery.");
            if(File.Exists(previousExe)&&IsolationControl.FileHash(previousExe)==previous.Hash)return new{Validated=true,Executable=previousExe,Message="Automatically observed DX12 executable; current hash matches the saved profile. Private-driver compatibility still requires an original control test."};
            if(Directory.Exists(previousExe+".local"))return new{Validated=false,Executable=previousExe,Message="Game executable changed. Return its private set to stock, then launch DX12 to rediscover it. Recovery stays available."};
        }
        var candidates=new Dictionary<string,MatchedSet.ProfileInfo>(StringComparer.OrdinalIgnoreCase);int unreadable=0;
        string activeXe2=Path.Combine(ActivePackage(),"igd12um64xe2.dll");
        foreach(var process in System.Diagnostics.Process.GetProcesses())using(process) {
            IntPtr handle=OpenProcess(0x1000,false,(uint)process.Id);
            try {
                if(handle==IntPtr.Zero)continue;var path=new System.Text.StringBuilder(4096);uint length=4096;
                if(!QueryFullProcessImageName(handle,0,path,ref length))continue;string exe=path.ToString();
                if(!exe.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))continue;
                var modules=WindowsPlatform.ModulePaths(process.Id).ToArray();
                if(!modules.Any(m=>Path.GetFileName(m).Equals("d3d12.dll",StringComparison.OrdinalIgnoreCase)))continue;
                if(!modules.Any(m=>m.Equals(activeXe2,StringComparison.OrdinalIgnoreCase)))continue;
                RequireAmd64Exe(exe);Isolation.RequireNoLocalD3D12(exe);
                if(Directory.Exists(exe+".local")||File.Exists(exe+".local"))continue;
                RequireOriginalActivePackage();
                candidates[exe]=new MatchedSet.ProfileInfo{Schema=1,AppId=r.SteamAppId,Root=root,Name=process.ProcessName,RelativeExecutable=exe.Substring(root.Length+1),Hash=IsolationControl.FileHash(exe),ObservedDriverHash=Engine.Hash(Engine.Read(activeXe2)),ObservedUtc=DateTime.UtcNow.ToString("o")};
            }catch(Exception e){if(!(e is IOException)&&!(e is UnauthorizedAccessException)&&!(e is InvalidOperationException)&&!(e is System.ComponentModel.Win32Exception))throw;unreadable++;}finally{if(handle!=IntPtr.Zero)CloseHandle(handle);}
        }
        if(candidates.Count==1){var entry=candidates.Single();if(!r.ReadOnly)MatchedSet.SaveProfile(entry.Value);return new{Validated=true,Executable=entry.Key,Message="DX12 game executable and original Intel driver observed automatically. No game files changed. Private-driver loading is still untested."};}
        return new{Validated=false,Executable=previousExe,Message=candidates.Count>1?"Multiple rendering executables found; automatic setup needs an unambiguous game process. No profile guessed.":"Launch this game in DX12 with the stock driver and leave it running. Detection will retry automatically. Disable a local D3D12 recorder/proxy first. Unreadable matching processes: "+unreadable};
    }
    static string ObserveGame(string executable) {
        string full=Path.GetFullPath(executable),name=Path.GetFileNameWithoutExtension(full);var processes=System.Diagnostics.Process.GetProcessesByName(name);
        if(processes.Length==0)return "This game is not running. Launch it through Steam, leave it at the menu, and check again.";
        var lines=new List<string>();int inaccessible=0,matched=0;
        foreach(var process in processes)using(process) {
            IntPtr handle=OpenProcess(0x1000,false,(uint)process.Id);
            try {
                if(handle==IntPtr.Zero){inaccessible++;continue;}
                var path=new System.Text.StringBuilder(4096);uint length=4096;
                if(!QueryFullProcessImageName(handle,0,path,ref length)){inaccessible++;continue;}
                if(!path.ToString().Equals(full,StringComparison.OrdinalIgnoreCase))continue;matched++;
                foreach(string module in WindowsPlatform.ModulePaths(process.Id))if(Path.GetFileName(module).Equals("igd12um64xe2.dll",StringComparison.OrdinalIgnoreCase))
                    lines.Add(name+" (PID "+process.Id+"): "+module+"\r\nDisk SHA-256: "+Engine.Hash(Engine.Read(module)));
            }catch{inaccessible++;}finally{if(handle!=IntPtr.Zero)CloseHandle(handle);}
        }
        if(lines.Count==0)return inaccessible>0?"The game process could not be fully inspected. No driver-loading result is available.":matched==0?"The selected game installation is not running. Launch it through Steam and check again.":"This game is running, but no Xe2 driver path was observed. This does not confirm use of the private driver.";
        return String.Join("\r\n",lines)+"\r\nLoaded path and disk hash observed. In-memory bytes and cache growth were not measured.";
    }
    static object VerifyControl(Request r) {
        var receipt=MatchedSet.Read(r.ReceiptPath);MatchedSet.RequireDebugCompatibility(receipt);if(receipt.MiB!=512)throw new InvalidDataException("Original 512 MiB control required.");
        MatchedSet.ValidateGame(receipt.Executable);string expected=Path.Combine(receipt.Folder,"igd12um64xe2.dll");
        foreach(var process in System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(receipt.Executable)))using(process) {
            IntPtr handle=OpenProcess(0x1000,false,(uint)process.Id);
            try {
                if(handle==IntPtr.Zero)continue;var path=new System.Text.StringBuilder(4096);uint length=4096;
                if(!QueryFullProcessImageName(handle,0,path,ref length)||!path.ToString().Equals(receipt.Executable,StringComparison.OrdinalIgnoreCase))continue;
                if(WindowsPlatform.ModulePaths(process.Id).Any(m=>m.Equals(expected,StringComparison.OrdinalIgnoreCase))&&Engine.Hash(Engine.Read(expected))==Engine.Original) {
                    if(!r.ReadOnly)MatchedSet.RecordOriginalLoading(r.ReceiptPath);return new{Verified=true,Message="Original private Intel driver path and disk hash observed automatically. Game files unchanged. Gameplay and cache behavior remain unmeasured."};
                }
            }catch(System.ComponentModel.Win32Exception){}catch(UnauthorizedAccessException){}finally{if(handle!=IntPtr.Zero)CloseHandle(handle);}
        }
        return new{Verified=false,Message="Waiting for this game's original private driver to load in DX12."};
    }
    static string ActivePackage(){var p=DriverCatalog.Selected();if(p==null)throw new InvalidOperationException("No active Intel display package was found.");return p.Folder;}
    static object DriverIdentity(){var p=DriverCatalog.Selected();return new{IdentityTag=DriverCatalog.Identity(p),Adapter=p==null?null:p.Adapter,Version=p==null?null:p.Version,DriverFolder=p==null?null:p.Folder};}
    static object Status(bool signatures=false) {
        var package=DriverCatalog.Selected();if(package==null)throw new InvalidOperationException("No active Intel display package was found.");
        string identityBefore=DriverCatalog.Identity(package);string folder=package.Folder;var files=DriverCatalog.Names.Select(n=>DriverCatalog.Review(Path.Combine(folder,n),signatures)).ToArray();
        string reason;bool qualified=DriverCatalog.CohortQualified(package,out reason);string currentIdentity=DriverCatalog.Identity(DriverCatalog.Selected());if(identityBefore!=currentIdentity){qualified=false;reason="The active package changed during review. Check again after the driver installer finishes.";}foreach(var reviewed in files)reviewed.PatchEnabled=reviewed.PatchEnabled&&qualified;var xe2=files.First(f=>f.Name=="igd12um64xe2.dll");object setting;
        using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options"))setting=key==null?null:key.GetValue("DevOverrideEnable",null);
        bool reboot=false;
        if(File.Exists(IsolationControl.ReceiptPath)) {
            var receipt=Json.Deserialize<OverrideReceipt>(File.ReadAllText(IsolationControl.ReceiptPath));DateTime created;
            if(receipt!=null&&receipt.Status=="enabled-reboot-required"&&DateTime.TryParse(receipt.CreatedUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out created))reboot=created.ToUniversalTime()>DateTime.UtcNow.AddMilliseconds(-(double)GetTickCount64());
        }
        return new {DriverFolder=folder,Version=package.Version,Adapter=package.Adapter,Hash=xe2.Hash,Original=xe2.State=="QualifiedOriginal",Compatible=qualified,CompatibilityReason=reason,Files=files,IdentityTag=currentIdentity,DeveloperOverride=setting!=null&&Convert.ToInt32(setting)==1,RebootRequired=reboot,Administrator=IsolationControl.Administrator};
    }
    [STAThread]static int Main(string[] args) {
        try {
            if(args.Length==2&&args[0]=="--cache-monitor") {
                MatchedSet.ValidateGame(args[1]);System.Windows.Forms.Application.EnableVisualStyles();
                System.Windows.Forms.Application.Run(new CacheMonitor(args[1],delegate(string path){var inspected=Engine.Inspect(Engine.Read(path));return (inspected.MiB/1024.0).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+" GiB disk image";}));return 0;
            }
            string input=Console.In.ReadToEnd();if(input.Length>65536)throw new InvalidDataException("Request is too large.");
            var r=Json.Deserialize<Request>(input);if(r==null)throw new InvalidDataException("Request is empty.");
            object data=null;string message;
            switch(r.Action) {
                case "Safety":data=Safety(r);message="Fresh install and Steam safety checks completed.";break;
                case "DiscoverGame":data=DiscoverGame(r);message="Automatic game discovery completed; game and driver files unchanged.";break;
                case "VerifyControl":data=VerifyControl(r);message="Original-control observation completed; game files unchanged.";break;
                case "Status":data=Status();message="Installed driver compatibility inspected.";break;
                case "DriverIdentity":data=DriverIdentity();message="Driver metadata checked; no DLL hashing or writes.";break;
                case "CheckDriverCompatibility":data=Status(true);message="Read-only DLL, signature and full-package compatibility review completed. Signature candidates do not enable patches.";break;
                case "Observe":message=WindowsPlatform.Observe(true).ToString();break;
                case "ObserveGame":message=ObserveGame(r.Executable);break;
                case "GameState":data=GameState(r);message="Game setup inspected by the safety engine.";break;
                case "ValidateRecord":data=MatchedSet.Read(r.ReceiptPath);message="Recovery record matches the pinned profile.";break;
                case "Validate":var receipt=MatchedSet.Read(r.ReceiptPath);if(receipt.Status!="removed"||Directory.Exists(receipt.Folder))MatchedSet.ValidateContents(receipt);data=receipt;message=receipt.Status=="removed"?"Previous private set is removed.":"Private set matches the pinned profile.";break;
                case "Prepare":Consent(r);RequirePatchSafety(r.Executable,r.RiskConfirmed);RequireOriginalActivePackage();if(r.MiB!=512)throw new InvalidDataException("New setups start with unchanged originals.");
                    if(File.Exists(r.ReceiptPath)) {var previous=MatchedSet.Read(r.ReceiptPath);if(previous.Status!="removed"||Directory.Exists(previous.Folder)||File.Exists(previous.Folder)||!previous.Executable.Equals(Path.GetFullPath(r.Executable),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Existing setup must be verified and removed first.");File.Move(r.ReceiptPath,r.ReceiptPath+".removed-"+Guid.NewGuid().ToString("N"));}
                    message=MatchedSet.Prepare(r.Executable,ActivePackage(),r.ReceiptPath,512);break;
                case "Import":Consent(r);var imported=MatchedSet.Read(r.SourceReceipt);MatchedSet.ValidateContents(imported);
                    var importedGame=imported.Game??MatchedSet.Profile(imported.Executable);
                    string importedRoot=imported.Game!=null?Path.GetFullPath(imported.Game.Root):MatchedSet.GameRoot(imported.Executable);
                    if(r.SteamAppId!=0&&(importedGame.AppId!=r.SteamAppId||!importedRoot.Equals(Path.GetFullPath(r.InstallDirectory),StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("Receipt belongs to another Steam game installation.");
                    if(r.Executable!=null&&!Path.GetFullPath(imported.Executable).Equals(Path.GetFullPath(r.Executable),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Receipt belongs to another installation.");
                    Engine.CheckPath(Path.GetDirectoryName(Path.GetFullPath(r.ReceiptPath)));byte[] importedBytes=System.Text.Encoding.UTF8.GetBytes(Json.Serialize(imported));
                    using(var stream=new FileStream(r.ReceiptPath,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(importedBytes,0,importedBytes.Length);stream.Flush(true);}if(imported.Game!=null)MatchedSet.SaveProfile(imported.Game);message="Existing setup imported; game files unchanged.";break;
                case "Change":Consent(r);var change=MatchedSet.Read(r.ReceiptPath);RequirePatchSafety(change.Executable,r.RiskConfirmed);RequireOriginalActivePackage();
                    if(change.MiB==512&&r.MiB!=512&&change.Status!="original-control-loading-verified")throw new InvalidOperationException("Launch the unchanged original private control in DX12. Its loaded driver will be checked automatically before patching.");
                    message=MatchedSet.ChangeCap(r.ReceiptPath,r.MiB);break;
                case "Remove":Consent(r);message=MatchedSet.Remove(r.ReceiptPath);break;
                case "EnableOverride":Consent(r);message=IsolationControl.Enable();break;
                case "RestoreOverride":Consent(r);RequireNoKnownPrivateSets();message=IsolationControl.Restore();break;
                default:throw new InvalidOperationException("Unknown action. No changes made.");
            }
            Console.Write(Json.Serialize(new {Ok=true,Message=message,Data=data}));return 0;
        }catch(Exception e){Console.Write(Json.Serialize(new {Ok=false,Message=e.Message,Data=(object)null}));return 1;}
    }
}
