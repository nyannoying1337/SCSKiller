using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace ArcCap {
public class MatchedSetReceipt {
    public int Schema {get;set;} public string Executable {get;set;} public string Folder {get;set;}
    public int MiB {get;set;} public string Status {get;set;} public Dictionary<string,string> Files {get;set;}
    public MatchedSet.ProfileInfo Game {get;set;}
}
public static class MatchedSet {
    public const string GameHash="2DF765398A3D1F165DB418271BE7200178320A4466817379F2FA0DC0001FB101";
    public class ProfileInfo {
        public uint AppId;public string Name,RelativeExecutable,Hash,Launcher;
        public int Schema;public string Root,ObservedDriverHash,ObservedUtc;
    }
    static readonly ProfileInfo[] Profiles={
        new ProfileInfo{AppId=292030,Name="Witcher",RelativeExecutable=@"bin\x64_dx12\witcher3.exe",Hash=GameHash}
    };
    public static string ProfileDirectory {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"IntelArcCacheCap","profiles");}}
    public static string ProfilePath(uint appId){return Path.Combine(ProfileDirectory,"steam-"+appId+".json");}
    public static string ProfileExecutable(ProfileInfo p) {
        Require(p!=null&&p.Schema==1&&p.AppId>0&&p.ObservedDriverHash==Engine.Original&&p.Hash!=null&&System.Text.RegularExpressions.Regex.IsMatch(p.Hash,"\\A[0-9A-F]{64}\\z"),"Invalid observed game profile.");
        Require(!String.IsNullOrWhiteSpace(p.Root)&&!String.IsNullOrWhiteSpace(p.RelativeExecutable)&&!Path.IsPathRooted(p.RelativeExecutable),"Invalid profile paths.");
        string root=Path.GetFullPath(p.Root).TrimEnd(Path.DirectorySeparatorChar),exe=Path.GetFullPath(Path.Combine(root,p.RelativeExecutable));
        Require(exe.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&Path.GetExtension(exe).Equals(".exe",StringComparison.OrdinalIgnoreCase),"Observed executable escaped its game installation.");return exe;
    }
    public static ProfileInfo ReadProfile(uint appId){string path=ProfilePath(appId);if(!File.Exists(path))return null;Engine.CheckPath(path);Require(new FileInfo(path).Length<=65536,"Profile is too large.");var p=new JavaScriptSerializer().Deserialize<ProfileInfo>(File.ReadAllText(path));ProfileExecutable(p);Require(p.AppId==appId,"Profile identity mismatch.");return p;}
    public static void SaveProfile(ProfileInfo profile) {
        ProfileExecutable(profile);Directory.CreateDirectory(ProfileDirectory);Engine.CheckPath(ProfileDirectory);
        string path=ProfilePath(profile.AppId),tmp=path+"."+Guid.NewGuid().ToString("N")+".pending";
        File.WriteAllText(tmp,new JavaScriptSerializer().Serialize(profile));if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);
    }
    public static ProfileInfo Profile(string executable) {
        string full=Path.GetFullPath(executable);
        if(Directory.Exists(ProfileDirectory))foreach(string file in Directory.EnumerateFiles(ProfileDirectory,"steam-*.json").Take(10000)) {
            uint id;if(!UInt32.TryParse(Path.GetFileNameWithoutExtension(file).Substring(6),out id))continue;
            var profile=ReadProfile(id);if(full.Equals(ProfileExecutable(profile),StringComparison.OrdinalIgnoreCase))return profile;
        }
        foreach(var profile in Profiles)if(full.EndsWith("\\"+profile.RelativeExecutable,StringComparison.OrdinalIgnoreCase))return profile;
        throw new InvalidDataException("No pinned private-driver profile for this executable path.");
    }
    public static string GameRoot(string executable) {
        var profile=Profile(executable);string full=Path.GetFullPath(executable);
        return profile.Root==null?full.Substring(0,full.Length-profile.RelativeExecutable.Length-1):Path.GetFullPath(profile.Root);
    }
    const string Xe2="igd12um64xe2.dll";
    public static readonly Dictionary<string,string> Originals=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
            {"clangFEWrapper.dll","0B92E83923B4546E84E307AC940676F299253E223BC3F39B2A9A0101FCB9C6D0"},
            {"DnnlPlugin.dll","830FAE35948D3912364BB2DACE719A85D1E450D810379C816B7D98BDB9CB2E87"},
            {"iga64.dll","1504040E97F3CFB66561DC199F19AB3AD82AF8D3AE8F9C690C074CAE72F2228E"},
            {"igc-default64.dll","55559222A70929C46C7B4000E019BFE5126FA8C051037CFDF62AC6F29BA53A89"},
            {"igc-fallback64.dll","FF95E410083871A410903CA091179F256AEC6B6E96716CC9DF86A9893BF4C0F3"},
            {"igc64.dll","39911E8E314686FD29DD0FEA4BFF1262CEBF59FAA107C0625347A087AC9352CB"},
            {"igd12dxva64.dll","01F5A99859004812C108BF0C41B120789A044E480DD4FFB106C7A3909330C862"},
            {"igd12um64xe2.dll","1A552E471AE986A52D045C4112E1980FB7171AABEBFF93C027A4BBA10A3C6229"},
            {"igd12umd64.dll","BC44EA9502D717E407B47D363A9436CCE2CE3B7C62CD181EFAEC075F3E61FAA9"},
            {"igd12umt64.dll","0B59B540FDDF541404A2090CEBD108A9F6483371E81B40258EF71F220EF77FA8"},
            {"igddxvacommon64.dll","C3FF451B4E02D69EEBE91C2494E995BD06483ED8339D18512F69C5311DAD5272"},
            {"igdext64.dll","77023AF5B37A0F6C131E6F187B0343824407BC6CE343F383998784B0F1E0D838"},
            {"igdfcl64.dll","4AAFD9F797A227CDAB388853BA835A5670F384CA98395E859A4CF7810B8D68AE"},
            {"igdgmm2_64.dll","DB2EA2A6F271CC85D097DC13176E153E5EBF67775155DF162B768A75ED863A2E"},
            {"igdgmm64.dll","DB3B3D6FBC6ABA6BA156080224916C1FEFCB1080F0EA3927727F52626623C681"},
            {"igdgmmstaging_64.dll","994D22569552487DCADBC8B1AA6AC9BEF0C24DF23E67637B4E2A2F63952C1E70"},
            {"igdinfo64.dll","C15C5EB83770455AB89E298346314572B9BE8BDE3AFD6161B425AA9A1EA9BE30"},
            {"igdmd64.dll","64F2680B79D9F2E30CC3B368A5C205FD8CC8A45155F68C9CC3400E180D8280DA"},
            {"igdml64.dll","02000B55E4AF662CA3D0C3999CCB021BCA99C66940C2EE97D3BEE6099BC4F5AA"},
            {"igmlsblob64xe2.dll","FA904D151ED0C25B062C1848ACF5C454805C1A989DB5C9294741818A24C4AC94"},
            {"igxess2.dll","2C49B09841D3D370D845A65DC7D49A857FAE8D15B90341816DDD11E9961EA789"},
            {"MSVCP140.dll","8E8F8293B3FF9DA7EB1B6B68132E330A7B04A01319B04A433DA2FF2D936B40EC"},
            {"opencl-clang264.dll","173F91A4A2D985DB86FF940486F32578BCAC83F0F89F36C7D4B8597B3433F7A0"},
            {"UniversalAdapter64.dll","CBBBDF2C03BB82B6F01AE11EAFD6C424F6770510943DF0E32B7F97D4B6CC034C"},
            {"VCRUNTIME140_1.dll","800A51AE7DC7154C2DC1F9C8FC70103D2E3A3B86BB1E7F7E2178FF516688E116"},
            {"VCRUNTIME140.dll","10B7E953B52F7D47471D86CB0E8CD279DC6BC7151556036EAD1DD6A9AD9F6F59"},
            {"ze_loader.dll","B902825D0193E1C86D8CC628913C7869A87B2FB1DBF81F86E0C20422269EA0A4"},
    };
    static void Require(bool yes,string message){if(!yes)throw new InvalidDataException(message);}
    static void Closed(string executable){var profile=Profile(executable);foreach(string name in new[]{Path.GetFileNameWithoutExtension(executable),profile.Launcher}.Where(n=>n!=null))foreach(var process in Process.GetProcessesByName(name))using(process)throw new InvalidOperationException("Close "+profile.Name+" before changing its private set.");}
    public static void ValidateGame(string executable) {
        string full=Path.GetFullPath(executable);Engine.CheckPath(full);
        var profile=Profile(full);
        Require(IsolationControl.FileHash(full)==profile.Hash,"Unsupported "+profile.Name+" executable. Launch the updated game with the stock driver to rediscover its profile.");
        Isolation.RequireNoLocalD3D12(full);
    }
    // Microsoft Windows 10.0.26100.9549 AMD64 helpers: source signatures were
    // verified during support qualification. Copy locally; never ship these DLLs.
    public static readonly Dictionary<string,string> DebugHelpers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
        {"dbghelp.dll","2D2EC94891162A61D86789133623B47D64D74C15D6BE99473B25AD7A6468CF39"},
        {"dbgcore.dll","4AA8F5241B94752CAC649E61BDF57C8910E1DBF3D4E9759C658BB41A852F6CDF"}
    };
    public static bool NeedsDebugRoutingFix(string executable) {
        string helper=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(executable)),"dbghelp.dll");
        if(!File.Exists(helper))return false;Engine.CheckPath(helper);
        using(var reader=new BinaryReader(File.OpenRead(helper))) {
            Require(reader.BaseStream.Length>=64,"Truncated game debug helper.");Require(reader.ReadUInt16()==0x5A4D,"Unknown game debug helper.");
            reader.BaseStream.Position=60;uint pe=reader.ReadUInt32();Require(pe<=reader.BaseStream.Length-6,"Invalid game debug helper header.");
            reader.BaseStream.Position=pe;Require(reader.ReadUInt32()==0x4550,"Invalid game debug helper PE.");
            if(reader.ReadUInt16()!=0x14C)return false;
        }
        Require(IsolationControl.FileHash(helper)=="3DCE0A04D672F925E63CE3AFE2F9F2889CCD962C8B0F4DF2F4926E6B0F682AB3","Unqualified 32-bit dbghelp conflict. Private routing is blocked until a compatible fix is verified.");
        return true;
    }
    public static void RequireDebugCompatibility(MatchedSetReceipt receipt) {
        Require(!NeedsDebugRoutingFix(receipt.Executable)||receipt.Schema==4,"Existing private setup needs the debug-helper compatibility update. Use the system driver again, then prepare a fresh original 512 MiB copy with this app.");
    }
    public static void ValidateDebugSources() {
        foreach(var file in DebugHelpers){string source=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),file.Key);Engine.CheckPath(source);Require(IsolationControl.FileHash(source)==file.Value,"Windows debug helper version is unsupported: "+file.Key+". No compatibility files copied.");}
    }
    static string Expected(string name,int mib) {
        Require(mib==512||mib==1024||mib==2048||mib==4096,"Supported private limits: 512, 1024, 2048 or 4096 MiB.");
        return name.Equals(Xe2,StringComparison.OrdinalIgnoreCase)?mib==1024?Engine.OneGiB:mib==2048?Engine.TwoGiB:mib==4096?Engine.FourGiB:Engine.Original:Originals[name];
    }
    public static void ValidateReceipt(MatchedSetReceipt r) {
        Require(r!=null&&(r.Schema==3||r.Schema==4)&&r.Files!=null&&r.Files.Count==Originals.Count+(r.Schema==4?DebugHelpers.Count:0),"Unsupported matched-set receipt.");
        string exe=Path.GetFullPath(r.Executable),folder=Path.GetFullPath(r.Folder);
        if(r.Game!=null)Require(ProfileExecutable(r.Game).Equals(exe,StringComparison.OrdinalIgnoreCase),"Receipt game profile differs from its target.");else Profile(exe);
        Require(folder.Equals(exe+".local",StringComparison.OrdinalIgnoreCase),"Unexpected receipt target.");
        foreach(var entry in Originals)Require(r.Files.ContainsKey(entry.Key)&&r.Files[entry.Key]==Expected(entry.Key,r.MiB),"Receipt does not match the pinned package.");
        if(r.Schema==4)foreach(var entry in DebugHelpers)Require(r.Files.ContainsKey(entry.Key)&&r.Files[entry.Key]==entry.Value,"Receipt does not match pinned Windows debug helpers.");
    }
    static void Save(string path,MatchedSetReceipt r) {
        path=Path.GetFullPath(path);Engine.CheckPath(Path.GetDirectoryName(path));
        string tmp=path+".pending";Require(!File.Exists(tmp),"Receipt staging file already exists.");
        byte[] bytes=System.Text.Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(r));
        using(var stream=new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);
    }
    public static MatchedSetReceipt Read(string path) {Engine.CheckPath(path);var r=new JavaScriptSerializer().Deserialize<MatchedSetReceipt>(File.ReadAllText(path));ValidateReceipt(r);return r;}
    public static void RecordOriginalLoading(string path){var receipt=Read(path);Require(receipt.MiB==512,"Only unchanged original controls can be verified.");ValidateContents(receipt);receipt.Status="original-control-loading-verified";Save(path,receipt);}
    public static void ValidateContents(MatchedSetReceipt r) {
        ValidateReceipt(r);Engine.CheckPath(r.Folder);
        Require(Directory.EnumerateFileSystemEntries(r.Folder).Count()==r.Files.Count,"Private set contains unexpected files; nothing will be removed.");
        foreach(var entry in r.Files){string file=Path.Combine(r.Folder,entry.Key);Engine.CheckPath(file);Require(File.Exists(file)&&IsolationControl.FileHash(file)==entry.Value,"Private set changed: "+entry.Key);}
    }
    public static string Prepare(string executable,string driverFolder,string receiptPath,int mib) {
        ValidateGame(executable);Closed(executable);driverFolder=Path.GetFullPath(driverFolder);Engine.CheckPath(driverFolder);
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),@"System32\DriverStore\FileRepository")+Path.DirectorySeparatorChar;
        Require(driverFolder.StartsWith(root,StringComparison.OrdinalIgnoreCase),"Choose the installed driver package inside DriverStore.");
        using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options"))
            Require(key!=null&&Convert.ToInt32(key.GetValue("DevOverrideEnable",0))==1,"Developer override must already be explicitly enabled and Windows rebooted. No setting was changed.");
        string full=Path.GetFullPath(executable),folder=full+".local";receiptPath=Path.GetFullPath(receiptPath);
        Require(!receiptPath.StartsWith(Path.GetDirectoryName(full)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Save the receipt outside the game directory.");
        Engine.CheckPath(Path.GetDirectoryName(receiptPath));Require(!File.Exists(receiptPath)&&!Directory.Exists(folder)&&!File.Exists(folder),"Receipt or redirection path already exists.");
        foreach(var entry in Originals) {
            Require(!File.Exists(Path.Combine(Path.GetDirectoryName(full),entry.Key)),"Game root already contains Intel package DLL: "+entry.Key);
            string source=Path.Combine(driverFolder,entry.Key);Engine.CheckPath(source);Require(IsolationControl.FileHash(source)==entry.Value,"Unsupported or changed package file: "+entry.Key);
        }
        bool fixDebug=NeedsDebugRoutingFix(full);if(fixDebug)ValidateDebugSources();
        var gameProfile=Profile(full);
        var r=new MatchedSetReceipt{Schema=fixDebug?4:3,Executable=full,Folder=folder,MiB=mib,Status="prepare-started",Files=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase),Game=gameProfile.Schema==1?gameProfile:null};
        foreach(var entry in Originals)r.Files.Add(entry.Key,Expected(entry.Key,mib));if(fixDebug)foreach(var entry in DebugHelpers)r.Files.Add(entry.Key,entry.Value);Save(receiptPath,r);
        string staging=folder+".pending-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(staging);
        try {
            foreach(var entry in Originals) {
                byte[] bytes=File.ReadAllBytes(Path.Combine(driverFolder,entry.Key));Require(Engine.Hash(bytes)==entry.Value,"Source changed during copy.");
                if(entry.Key.Equals(Xe2,StringComparison.OrdinalIgnoreCase))bytes=Engine.Patch(bytes,mib);
                string dest=Path.Combine(staging,entry.Key);
                using(var stream=new FileStream(dest,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                Require(IsolationControl.FileHash(dest)==r.Files[entry.Key],"Copy failed validation.");
            }
            if(fixDebug)foreach(var entry in DebugHelpers) {
                string source=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),entry.Key);Engine.CheckPath(source);
                byte[] bytes=File.ReadAllBytes(source);Require(Engine.Hash(bytes)==entry.Value,"Windows debug source changed during copy.");
                string dest=Path.Combine(staging,entry.Key);using(var stream=new FileStream(dest,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                Require(IsolationControl.FileHash(dest)==entry.Value,"Windows debug copy failed validation.");
            }
            Closed(full);ValidateGame(full);Directory.Move(staging,folder);ValidateContents(r);r.Status="prepared-runtime-unverified";Save(receiptPath,r);
            return "Matched private set prepared: "+mib+" MiB. Runtime loading and cache effect require verification.\r\n"+folder+"\r\nRecovery receipt: "+receiptPath;
        } catch {r.Status="recovery-required";Save(receiptPath,r);throw;}
        // Failed staging is retained with pinned hashes for inspection; never recursively remove game paths.
    }
    public static string ChangeCap(string receiptPath,int mib) {
        var r=Read(receiptPath);Closed(r.Executable);ValidateGame(r.Executable);ValidateContents(r);RequireDebugCompatibility(r);Expected(Xe2,mib);
        string target=Path.Combine(r.Folder,Xe2),staging=target+".pending";Require(!File.Exists(staging),"Staged DLL already exists; recover before continuing.");
        byte[] bytes=Engine.Patch(Engine.Read(target),mib);r.Status="change-started";Save(receiptPath,r);
        using(var stream=new FileStream(staging,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        try {
            Closed(r.Executable);Require(IsolationControl.FileHash(target)==r.Files[Xe2],"Private DLL changed before replacement.");
            File.Replace(staging,target,null);r.MiB=mib;r.Files[Xe2]=Expected(Xe2,mib);r.Status="changed-runtime-unverified";Save(receiptPath,r);ValidateContents(r);
            return "Private Xe2 copy changed to "+mib+" MiB. Installed driver unchanged. Re-launch the game in DX12 and observe the loaded path.";
        } catch {r.Status="recovery-required";Save(receiptPath,r);throw;}
    }
    public static string Remove(string receiptPath) {
        var r=Read(receiptPath);Closed(r.Executable);
        if(!Directory.Exists(r.Folder)){r.Status="removed";Save(receiptPath,r);return "Private set is already absent.";}
        ValidateContents(r);r.Status="remove-started";Save(receiptPath,r);
        foreach(var entry in r.Files) {Closed(r.Executable);string file=Path.Combine(r.Folder,entry.Key);Engine.CheckPath(file);Require(IsolationControl.FileHash(file)==entry.Value,"File changed during removal.");File.Delete(file);}
        Directory.Delete(r.Folder,false);r.Status="removed";Save(receiptPath,r);return "Owned private driver set removed. Installed driver and Windows settings unchanged.";
    }
}
}
