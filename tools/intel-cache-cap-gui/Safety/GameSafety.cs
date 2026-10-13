using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
namespace ArcCap {
public class GameSafetyReport {
    public bool ScanComplete {get;set;} public bool Allowed {get;set;} public string Message {get;set;}
    public string Marker {get;set;} public string MarkerPath {get;set;} public int Entries {get;set;}
    public string Multiplayer {get;set;} public string SteamStatus {get;set;}
}
public static class GameSafety {
    // Snapshot of SCSKiller.Core/Games/GameFiles.cs MarkerNames at repository HEAD.
    // Presence is an indicator; absence never proves all anti-cheat is absent.
    public static readonly string[] Markers = new string[] {
            "EasyAntiCheat",
            "EasyAntiCheat_EOS",
            "start_protected_game.exe",
            "EasyAntiCheat_EOS_Setup.exe",
            "EasyAntiCheat_Setup.exe",
            "BattlEye",
            "BEService.exe",
            "BEService_x64.exe",
            "BELauncher.exe",
            "BEClient_x64.dll",
            "BEClient.dll",
            "EAAntiCheat.Installer.exe",
            "GameGuard",
            "XIGNCODE",
            "nProtect",
            "randgrid.sys",
            "NCGuardSDK",
            "NCGuard",
            "AntiCheatExpert",
            "AceAntibotClient",
            "TP3Helper.exe",
            "SGuard",
            "SGuard64.exe",
            "SGuardSvc64.exe",
            "ACE-Base64.dll",
            "ACE-Base.dat",
            "ACE-Setup64.exe",
            "ACE-Service64.exe",
            "ACE-ATS64.dll",
            "ACE-CSI64.dll",
            "ACE-DFS64.dll",
            "TenProtect",
            "TesSafe.sys",
            "HoYoKProtect.sys",
            "mhypbase.dll",
            "mhyprot.sys",
            "mhyprot2.sys",
            "mhyprot3.sys",
            "ACE-BASE.sys",
            "GenshinImpact.exe",
            "YuanShen.exe",
            "StarRail.exe",
            "ZenlessZoneZero.exe",
            "BH3.exe",
            "HYP.exe",
            "HYPHelper.exe",
            "HYPWorker.exe",
            "EAAntiCheat.GameServiceLauncher.exe",
            "EAAntiCheat.GameServiceLauncher.dll",
            "vgk.sys",
            "vgc.exe",
            "VALORANT.exe",
            "VALORANT-Win64-Shipping.exe",
            "League of Legends.exe",
            "LeagueClient.exe",
            "LeagueClientUx.exe",
            "LeagueClientUxRender.exe",
            "LoR.exe",
            "Lion-Win64-Shipping.exe",
            "RiotClientServices.exe",
            "RiotClientUx.exe",
            "RiotClientUxRender.exe",
            "NeacClient.exe",
            "NeacSafe64.sys",
            "NeacSafe64_ex.sys",
            "NEP2.dll",
            "NEPCleaner.exe",
            "BlackCall.aes",
            "BlackCall64.aes",
            "BlackCat64.sys",
            "HShield",
            "PunkBuster",
            "PnkBstrA.exe",
            "pbsvc.exe",
            "pbsv.dll",
            "equ8_conf.json",
            ".build.info",
            ".product.db",
            "Warframe.x64.exe",
            "arbiter.dll",
            "gameguard.des",
            "DenuvoAC",
            "denuvo-anti-cheat.sys",
            "denuvo-anti-cheat-runtime.dll",
            "denuvo-anti-cheat-update-service.exe",
            "Denuvo Anti-Cheat Installer.exe",
            "*.xem",
            "*_BE.exe",
    };
    public static GameSafetyReport Scan(string root,int maxEntries=2000000,int milliseconds=10000) {
        var result=new GameSafetyReport{Multiplayer="Unknown",SteamStatus="Not checked",Message="Safety scan incomplete; explicit risk acknowledgment is required."};
        try {
            root=Path.GetFullPath(root);if(!Directory.Exists(root))return result;
            var pending=new Stack<string>();pending.Push(root);var clock=Stopwatch.StartNew();
            while(pending.Count>0) {
                string dir=pending.Pop();if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0){result.Message="A linked folder prevents a complete safety scan; explicit risk acknowledgment is required.";return result;}
                foreach(string path in Directory.EnumerateFileSystemEntries(dir)) {
                    if(++result.Entries>maxEntries||clock.ElapsedMilliseconds>milliseconds){result.Message="Safety scan exceeded its limit; explicit risk acknowledgment is required.";return result;}
                    string name=Path.GetFileName(path);
                    foreach(string marker in Markers)if(marker[0]=='*'?name.EndsWith(marker.Substring(1),StringComparison.OrdinalIgnoreCase):name.Equals(marker,StringComparison.OrdinalIgnoreCase)) {
                        result.Marker=marker;result.MarkerPath=path;result.Message="Anti-cheat indicator found: "+marker+". Explicit risk acknowledgment is required.";return result;
                    }
                    var attributes=File.GetAttributes(path);
                    if((attributes&FileAttributes.ReparsePoint)!=0){result.Message="A linked entry prevents a complete safety scan; explicit risk acknowledgment is required.";return result;}
                    if((attributes&FileAttributes.Directory)!=0)pending.Push(path);
                }
                if(clock.ElapsedMilliseconds>milliseconds)return result;
            }
            result.ScanComplete=true;result.Message="No listed anti-cheat indicators found. Unknown game profiles remain blocked.";
        }catch(Exception e){if(!(e is IOException)&&!(e is UnauthorizedAccessException)&&!(e is System.Security.SecurityException)&&!(e is ArgumentException))throw;result.Message="Safety scan could not read the complete install; explicit risk acknowledgment is required.";}
        return result;
    }
    public static GameSafetyReport Assess(uint appId,string root) {
        var report=Scan(root);
        if(appId==292030){report.Multiplayer="Reviewed single-player profile";report.Allowed=report.ScanComplete&&report.Marker==null; if(report.Allowed)report.Message="Reviewed Witcher single-player profile; no listed anti-cheat indicators found. Exact executable and driver checks still apply.";}
        return report;
    }
    public static void RequireWitcher(string executable) {
        string dir=Path.GetDirectoryName(Path.GetFullPath(executable));
        if(!Path.GetFileName(executable).Equals("witcher3.exe",StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(dir).Equals("x64_dx12",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("No reviewed single-player safety profile for this executable.");
        string root=Path.GetDirectoryName(Path.GetDirectoryName(dir));var report=Assess(292030,root);if(!report.Allowed)throw new InvalidDataException(report.Message);
    }
}
}