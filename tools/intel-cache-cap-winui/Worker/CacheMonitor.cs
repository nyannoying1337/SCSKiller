using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// External, read-only HUD. No DLL injection, hooks, driver writes or cache deletion.
class CacheSnapshot {
    public long Bytes;public int Files,Unreadable;public Dictionary<string,long> Sizes=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
    public static CacheSnapshot Read(string root) {
        var result=new CacheSnapshot();
        try {
            if(!Directory.Exists(root)){result.Unreadable++;return result;}
            if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)throw new IOException("Cache root is a reparse point.");
            foreach(string path in Directory.GetFiles(root))try {
                var f=new FileInfo(path);if((f.Attributes&FileAttributes.ReparsePoint)!=0){result.Unreadable++;continue;}
                long size=f.Length;result.Bytes=checked(result.Bytes+size);result.Files++;result.Sizes[f.Name]=size;
            }catch(IOException){result.Unreadable++;}catch(UnauthorizedAccessException){result.Unreadable++;}
        }catch(IOException){result.Unreadable++;}catch(UnauthorizedAccessException){result.Unreadable++;}
        return result;
    }
    public int ChangedSince(CacheSnapshot before) {
        int count=0;foreach(var pair in Sizes){long old;if(!before.Sizes.TryGetValue(pair.Key,out old)||old!=pair.Value)count++;}
        foreach(string name in before.Sizes.Keys)if(!Sizes.ContainsKey(name))count++;return count;
    }
}
class CacheMonitor : Form {
    readonly string executable,root,log;readonly CacheSnapshot initial;readonly Label text;readonly Timer timer=new Timer();int gamePid;DateTime started;
    struct Rect {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out Rect rectangle);
    [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool QueryFullProcessImageName(IntPtr process,uint flags,System.Text.StringBuilder path,ref uint length);
    [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
    protected override bool ShowWithoutActivation {get{return true;}}
    protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x20|0x08000000|0x80;return p;}}
    static string Number(long value){return (value/(1024.0*1024)).ToString("0.00",CultureInfo.InvariantCulture)+" MiB";}
    public CacheMonitor(string exe) {
        executable=Path.GetFullPath(exe);root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AppData","LocalLow","Intel","ShaderCache");
        foreach(var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)))using(p){IntPtr h=OpenProcess(0x1000,false,(uint)p.Id);try{if(h==IntPtr.Zero)continue;var path=new System.Text.StringBuilder(4096);uint n=4096;if(QueryFullProcessImageName(h,0,path,ref n)&&path.ToString().Equals(executable,StringComparison.OrdinalIgnoreCase)){gamePid=p.Id;started=p.StartTime;break;}}finally{if(h!=IntPtr.Zero)CloseHandle(h);}}
        if(gamePid==0)throw new InvalidOperationException("Launch the selected game before starting the monitor.");
        initial=CacheSnapshot.Read(root);string logs=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"IntelArcCacheCap","logs");Directory.CreateDirectory(logs);log=Path.Combine(logs,"cache-monitor-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+".csv");
        File.WriteAllText(log,"utc,pid,shared_intel_bytes,net_growth_bytes,changed_files,unreadable_files\r\n");
        FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;BackColor=Color.FromArgb(25,28,35);Opacity=0.88;ClientSize=new Size(380,124);
        text=new Label{Dock=DockStyle.Fill,Padding=new Padding(12),ForeColor=Color.White,Font=new Font("Segoe UI",11),BackColor=BackColor};Controls.Add(text);
        timer.Interval=2000;timer.Tick+=delegate{Sample();};Shown+=delegate{Sample();timer.Start();};FormClosed+=delegate{timer.Dispose();};
    }
    void Sample() {
        try{using(var p=Process.GetProcessById(gamePid))if(p.HasExited||p.StartTime!=started){Close();return;}}catch{Close();return;}
        IntPtr front=GetForegroundWindow();uint foregroundPid;GetWindowThreadProcessId(front,out foregroundPid);Rect rect;
        if(foregroundPid==gamePid&&GetWindowRect(front,out rect)){Location=new Point(Math.Max(rect.Left,rect.Right-Width-18),rect.Top+42);if(!Visible)Show();}else if(Visible)Hide();
        var now=CacheSnapshot.Read(root);long growth=now.Bytes-initial.Bytes;int changed=now.ChangedSince(initial);
        text.Text="Intel cache folder · SHARED / unattributed\r\nSize: "+Number(now.Bytes)+"   Growth: "+(growth>=0?"+":"")+Number(growth)+"\r\nChanged files: "+changed+"   Read failures: "+now.Unreadable+"\r\nDisk updates every 2s · not shader compile count";
        try{File.AppendAllText(log,DateTime.UtcNow.ToString("o")+","+gamePid+","+now.Bytes+","+growth+","+changed+","+now.Unreadable+"\r\n");}catch(IOException){text.Text+="\r\nLogging unavailable";}catch(UnauthorizedAccessException){text.Text+="\r\nLogging unavailable";}
    }
}
