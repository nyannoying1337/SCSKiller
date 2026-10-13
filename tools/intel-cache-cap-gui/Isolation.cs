using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ArcCap {
public static class Isolation {
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr FindResource(IntPtr module,IntPtr name,IntPtr type);
    [DllImport("kernel32.dll")] static extern IntPtr LoadResource(IntPtr module,IntPtr resource);
    [DllImport("kernel32.dll")] static extern IntPtr LockResource(IntPtr resource);
    [DllImport("kernel32.dll")] static extern uint SizeofResource(IntPtr module,IntPtr resource);
    [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
    public static string Manifest(string path) {
        Engine.CheckPath(path);
        // Resource-only mapping: never executes the game's entry point or DLL initialization.
        IntPtr m=LoadLibraryEx(Path.GetFullPath(path),IntPtr.Zero,0x22);
        if(m==IntPtr.Zero)throw new IOException("Cannot inspect game resources; Windows error "+Marshal.GetLastWin32Error());
        try {
            IntPtr r=FindResource(m,new IntPtr(1),new IntPtr(24));
            if(r==IntPtr.Zero) {
                int error=Marshal.GetLastWin32Error();
                if(error==1813||error==1814||error==1815)return null;
                throw new IOException("Manifest lookup failed; Windows error "+error);
            }
            uint size=SizeofResource(m,r);if(size>1024*1024)throw new InvalidDataException("Unexpectedly large manifest resource.");
            IntPtr address=LockResource(LoadResource(m,r));if(address==IntPtr.Zero)throw new IOException("Cannot map manifest resource.");
            byte[] bytes=new byte[size];Marshal.Copy(address,bytes,0,bytes.Length);return Encoding.UTF8.GetString(bytes);
        } finally {FreeLibrary(m);}
    }
    public static bool DotLocalCandidate(bool hasManifest,bool developerOverride) {return !hasManifest||developerOverride;}
    public static string LocalD3D12(string executable) {
        string candidate=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(executable)),"d3d12.dll");
        return File.Exists(candidate)?candidate:null;
    }
    public static void RequireNoLocalD3D12(string executable) {
        string local=LocalD3D12(executable);
        if(local!=null)throw new InvalidDataException("Per-game .local setup is blocked because this game already has a local d3d12.dll. Redirection can send a forwarding proxy's system-D3D12 load back to itself, causing recursion. Preserve the existing DLL; do not combine these mechanisms until compatibility is verified.\r\n"+local);
    }
    public static string FindWitcher() {
        var roots=new List<string>();
        using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) {
            if(key!=null) {string value=key.GetValue("SteamPath") as string;if(value!=null)roots.Add(value.Replace('/',Path.DirectorySeparatorChar));}
        }
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"));
        var libraries=new List<string>(roots);
        foreach(string root in roots) {
            string vdf=Path.Combine(root,"steamapps","libraryfolders.vdf");if(!File.Exists(vdf))continue;
            foreach(Match match in Regex.Matches(File.ReadAllText(vdf),"\"path\"\\s*\"([^\"]+)\""))libraries.Add(match.Groups[1].Value.Replace("\\\\","\\"));
        }
        foreach(string root in libraries) {
            string acf=Path.Combine(root,"steamapps","appmanifest_292030.acf");if(!File.Exists(acf))continue;
            var match=Regex.Match(File.ReadAllText(acf),"\"installdir\"\\s*\"([^\"]+)\"");if(!match.Success)continue;
            string exe=Path.Combine(root,"steamapps","common",match.Groups[1].Value,"bin","x64_dx12","witcher3.exe");if(File.Exists(exe))return exe;
        }
        return null;
    }
    public static string Inspect(string executable) {
        string full=Path.GetFullPath(executable);string manifest=Manifest(full);
        bool external=File.Exists(full+".manifest"),hasManifest=manifest!=null||external;
        bool dev=false;
        using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options")) {
            if(key!=null)dev=Convert.ToInt32(key.GetValue("DevOverrideEnable",0))==1;
        }
        bool candidate=DotLocalCandidate(hasManifest,dev);
        string localD3D12=LocalD3D12(full);
        string hash;using(var stream=File.OpenRead(full))using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
        var text=new StringBuilder();
        text.AppendLine("Per-game isolation preflight — READ ONLY");text.AppendLine("Executable: "+full);text.AppendLine("SHA-256: "+hash);
        text.AppendLine("Embedded application manifest: "+(manifest!=null?"present":"not found at resource ID 1"));
        text.AppendLine("External application manifest: "+(external?"present":"absent"));
        text.AppendLine("Machine-wide DevOverrideEnable: "+(dev?"enabled":"not enabled"));
        text.AppendLine(localD3D12!=null?"Status: .local isolation BLOCKED by an existing game-local D3D12 DLL; proxy compatibility is unverified.":candidate?"Status: .local mechanism is a candidate ONLY. Intel driver routing is still unverified.":"Status: simple .local isolation BLOCKED by application manifest + disabled developer override.");
        if(localD3D12!=null)text.AppendLine("Local D3D12 DLL: "+localD3D12+" — "+System.Diagnostics.FileVersionInfo.GetVersionInfo(localD3D12).FileDescription);
        text.AppendLine("Existing .local path: "+(File.Exists(full+".local")||Directory.Exists(full+".local")?"present; not modified":"absent"));
        text.AppendLine("No game files, registry entries, driver files or enforcement settings were changed.");
        text.AppendLine("The separate Enable button requires manual administrator launch and explicit confirmation. Preflight never changes settings or executable files.");
        text.AppendLine("For multiplayer separation, the original must be installed globally; a local copy alone does not accomplish that.");
        foreach(string p in WindowsPlatform.Discover()) {
            try {var info=Engine.Inspect(Engine.Read(p));text.AppendLine("Package candidate: "+p);text.AppendLine("  "+info.State+"; "+info.MiB+" MiB; "+info.Hash);}
            catch(Exception e){text.AppendLine("Package candidate could not be verified: "+p+" — "+e.Message);}
        }
        text.AppendLine("A package candidate is not proof of active use. Module observation is still required.");
        text.AppendLine("Microsoft reference: https://learn.microsoft.com/en-us/windows/win32/dlls/dynamic-link-library-redirection");
        return text.ToString();
    }
}
}
