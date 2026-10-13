using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace ArcCap {
public class ModuleObservation {
    public List<string> Modules=new List<string>(); public int Inaccessible;
    public override string ToString() {return (Modules.Count==0?"No Xe2 module path observed.":String.Join(Environment.NewLine,Modules))+Environment.NewLine+
        "Inaccessible process scans: "+Inaccessible+". Path observation and disk hashes do not prove in-memory bytes or cache behavior.";}
}
public static class WindowsPlatform {
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct ModuleEntry {
        public uint Size,ModuleId,ProcessId,GlobalUsage,ProcessUsage;public IntPtr BaseAddress;public uint BaseSize;public IntPtr Handle;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string Path;
    }
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr CreateToolhelp32Snapshot(uint flags,uint pid);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool Module32FirstW(IntPtr snapshot,ref ModuleEntry entry);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool Module32NextW(IntPtr snapshot,ref ModuleEntry entry);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    public static List<string> ModulePaths(int pid) {
        IntPtr snapshot=new IntPtr(-1);
        for(int attempt=0;attempt<3;attempt++){snapshot=CreateToolhelp32Snapshot(0x18,(uint)pid);if(snapshot!=new IntPtr(-1)||Marshal.GetLastWin32Error()!=24)break;}
        if(snapshot==new IntPtr(-1))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        try {
            var paths=new List<string>();var entry=new ModuleEntry{Size=(uint)Marshal.SizeOf(typeof(ModuleEntry))};
            if(!Module32FirstW(snapshot,ref entry)){int error=Marshal.GetLastWin32Error();if(error==18)return paths;throw new System.ComponentModel.Win32Exception(error);}
            do{paths.Add(entry.Path);entry.Size=(uint)Marshal.SizeOf(typeof(ModuleEntry));}while(Module32NextW(snapshot,ref entry));
            if(Marshal.GetLastWin32Error()!=18)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            return paths;
        }finally{CloseHandle(snapshot);}
    }
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int n);
    [DllImport("wintrust.dll",ExactSpelling=true,PreserveSig=true)] static extern int WinVerifyTrust(IntPtr window,ref Guid action,ref TrustData data);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct TrustFile {
        public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path; public IntPtr File,Subject;
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct TrustData {
        public uint Size; public IntPtr Policy,Sip; public uint UI,Revocation,Choice;public IntPtr Info;
        public uint StateAction;public IntPtr State;public IntPtr Url;public uint Flags,Context;
    }
    public static string EmbeddedSignature(string path) {
        var file=new TrustFile{Size=(uint)Marshal.SizeOf(typeof(TrustFile)),Path=Path.GetFullPath(path)};
        IntPtr ptr=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TrustFile)));
        try {
            Marshal.StructureToPtr(file,ptr,false);
            var data=new TrustData{Size=(uint)Marshal.SizeOf(typeof(TrustData)),UI=2,Choice=1,Info=ptr,Flags=0x1000};
            Guid action=new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            int result=WinVerifyTrust(new IntPtr(-1),ref action,ref data);
            string status=result==0?"Valid":unchecked((uint)result)==0x80096010?"Hash mismatch":unchecked((uint)result)==0x800B0100?"No embedded signature":"Not verified (0x"+unchecked((uint)result).ToString("X8")+")";
            return status+" — embedded Authenticode only; catalog trust not checked, network retrieval disabled.";
        } finally {Marshal.DestroyStructure(ptr,typeof(TrustFile));Marshal.FreeHGlobal(ptr);}
    }
    public static List<string> Discover() {
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","DriverStore","FileRepository");
        var found=new List<string>();
        foreach(string d in Directory.EnumerateDirectories(root,"iigd*")) {
            string file=Path.Combine(d,"igd12um64xe2.dll");if(File.Exists(file)) found.Add(file);
        }
        found.Sort(StringComparer.OrdinalIgnoreCase);return found;
    }
    public static ModuleObservation Observe(bool hashes) {
        var result=new ModuleObservation();
        foreach(var p in Process.GetProcesses()) {
            using(p) {
                try {
                    foreach(string modulePath in ModulePaths(p.Id)) {
                        if(!String.Equals(Path.GetFileName(modulePath),"igd12um64xe2.dll",StringComparison.OrdinalIgnoreCase))continue;
                        string line=p.ProcessName+" (PID "+p.Id+"): "+modulePath;
                        if(hashes) {try{line+="\r\nDisk SHA-256: "+Engine.Hash(Engine.Read(modulePath));}catch(Exception e){line+="\r\nHash unavailable: "+e.Message;}}
                        result.Modules.Add(line);
                    }
                } catch {result.Inaccessible++;}
            }
        }
        return result;
    }
    public static void RequireInstallEnvironment(string target) {
        using(var id=WindowsIdentity.GetCurrent()) {
            if(!new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator)) throw new InvalidOperationException("Manually launch the app as administrator in Safe Mode. The app never elevates itself.");
        }
        if(GetSystemMetrics(67)==0)throw new InvalidOperationException("Replacement is restricted to Windows Safe Mode. Open the Safe Mode guide. Boot settings are never changed by this app.");
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","DriverStore","FileRepository")+Path.DirectorySeparatorChar;
        string full=Path.GetFullPath(target);Engine.CheckPath(full);
        if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!String.Equals(Path.GetFileName(full),"igd12um64xe2.dll",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only this Windows installation's DriverStore Xe2 DLL is supported. Offline installation is not implemented.");
        var scan=Observe(false);
        if(scan.Modules.Count>0)throw new InvalidOperationException("Intel DLL observed in a process. Close the process before replacement.\r\n"+scan);
    }
}
}
