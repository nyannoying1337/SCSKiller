using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArcCap {
public class MainForm : Form {
    readonly ComboBox path=new ComboBox(); readonly TextBox bundle=new TextBox();
    readonly NumericUpDown cap=new NumericUpDown(); readonly CheckBox consent=new CheckBox();
    readonly Label current=new Label(),status=new Label(); readonly TextBox log=new TextBox();
    readonly ProgressBar progress=new ProgressBar();readonly List<Button> actions=new List<Button>();
    bool busy;readonly string logPath;
    public MainForm() {
        Text="Intel Arc Cache Cap — experimental";ClientSize=new Size(960,900);MinimumSize=new Size(820,800);
        Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=1,AutoScroll=true};Controls.Add(root);
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        Add(root,new Label{Text="Intel Arc shader-cache cap",Font=new Font("Segoe UI",21,FontStyle.Bold),AutoSize=true});
        Add(root,Label("Compiled C# Windows app • experimental • verified 32.0.101.9034 only"));
        Add(root,Label("1. Inspect a driver DLL"));
        path.Dock=DockStyle.Fill;path.DropDownStyle=ComboBoxStyle.DropDown;Add(root,path);
        var inspectRow=Row();Add(root,inspectRow);
        Button(inspectRow,"Browse DLL",Browse);Button(inspectRow,"Scan packages",Scan);Button(inspectRow,"Inspect",Inspect);Button(inspectRow,"Observe game modules",Observe);
        current.Text="Current default cap: inspect a DLL first.";current.AutoSize=true;current.Font=new Font(Font,FontStyle.Bold);Add(root,current);
        Add(root,Label("2. Choose the desired default per-cache-file cap"));
        var limitRow=Row();cap.Minimum=128;cap.Maximum=2047;cap.Value=1024;cap.Width=100;limitRow.Controls.Add(cap);
        limitRow.Controls.Add(Label("MiB  •  1024 MiB = 1 GiB  •  current method: 128–2047 MiB"));Add(root,limitRow);
        Add(root,Label("Application profiles may override this default. A larger instruction patch is not yet verified."));
        var prepareRow=Row();Add(root,prepareRow);Button(prepareRow,"Create verified copy + backup…",Prepare);
        Add(root,Label("3. Install or roll back explicitly in Safe Mode"));bundle.Dock=DockStyle.Fill;Add(root,bundle);
        var installRow=Row();Add(root,installRow);Button(installRow,"Open bundle",OpenBundle);Button(installRow,"Safe Mode guide",Guide);Button(installRow,"Install…",()=>Change(false));Button(installRow,"Roll back…",()=>Change(true));
        consent.Text="I accept experimental system-file replacement and have the official Intel installer for recovery.";consent.AutoSize=true;Add(root,consent);
        Add(root,Label("Changed bytes invalidate original signature/catalog correspondence. Safe Mode does not restore trust.\r\nThis app never disables enforcement, changes boot settings, takes ownership or elevates itself."));
        var isolationRow=Row();Add(root,isolationRow);Button(isolationRow,"Check Witcher 3 isolation…",CheckIsolation);
        isolationRow.Controls.Add(Label("Read-only: reports routing blockers; changes nothing."));
        var overrideRow=Row();Add(root,overrideRow);Button(overrideRow,"Enable developer override…",EnableOverride);Button(overrideRow,"Restore developer setting…",RestoreOverride);Button(overrideRow,"Remove Witcher test copy…",RemovePrivate);
        Button(overrideRow,"Prepare Witcher copy…",PreparePrivate);
        var matchedRow=Row();Add(root,matchedRow);Button(matchedRow,"Prepare matched original set…",PrepareMatched);
        Button(matchedRow,"Change private set cap…",ChangeMatched);Button(matchedRow,"Remove matched set…",RemoveMatched);
        Add(root,Label("Matched set: tested Witcher build + exact 9034 package hashes only. Original 512 MiB / patched 1024 MiB.\r\nPrivate loading and gameplay confirmed on the test PC; cache growth effect remains unmeasured."));
        Add(root,Label("These setting buttons require a manually launched administrator session. Reboot manually after a change."));
        status.Text="Ready. No system-driver changes have been made.";status.AutoSize=true;status.Font=new Font(Font,FontStyle.Bold);Add(root,status);
        progress.Dock=DockStyle.Fill;progress.Height=8;Add(root,progress);
        log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Both;log.WordWrap=false;log.Dock=DockStyle.Fill;log.Font=new Font("Consolas",9);
        int last=root.RowCount;Add(root,log);root.RowStyles[last]=new RowStyle(SizeType.Percent,100);log.MinimumSize=new Size(0,120);
        string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"IntelArcCacheCap","logs");Directory.CreateDirectory(dir);
        logPath=Path.Combine(dir,DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-native.log");
        FormClosing+=(s,e)=>{if(busy){e.Cancel=true;MessageBox.Show(this,"Wait for the current operation to finish.","Operation in progress");}};
    }
    static Label Label(string text) {return new Label{Text=text,AutoSize=true,Margin=new Padding(3,5,3,5)};}
    static FlowLayoutPanel Row() {return new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,WrapContents=true};}
    static void Add(TableLayoutPanel root,Control c) {int row=root.RowCount++;root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.Controls.Add(c,0,row);}
    void Button(Control parent,string text,Action action) {var b=new Button{Text=text,AutoSize=true,Padding=new Padding(7,3,7,3)};b.Click+=(s,e)=>action();parent.Controls.Add(b);actions.Add(b);}
    void Write(string text) {string line=DateTime.Now.ToString("HH:mm:ss ")+text+"\r\n";log.AppendText(line);File.AppendAllText(logPath,line);}
    async void Run(string label,Func<string> task,Action completed=null) {
        if(busy)return;busy=true;status.Text=label;progress.Style=ProgressBarStyle.Marquee;
        foreach(var b in actions)b.Enabled=false;
        try {string result=await Task.Run(task);Write(result);if(completed!=null)completed();status.Text="Completed — results below.";}
        catch(Exception e){status.Text="Stopped — see recovery details below.";Write(e.Message);MessageBox.Show(this,e.Message,"Action stopped",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        finally{busy=false;progress.Style=ProgressBarStyle.Blocks;foreach(var b in actions)b.Enabled=true;}
    }
    void Browse() {using(var d=new OpenFileDialog{Filter="Driver DLL|*.dll",Title="Choose igd12um64xe2.dll"})if(d.ShowDialog(this)==DialogResult.OK){path.Text=d.FileName;current.Text="Current default cap: inspect this DLL.";}}
    void Scan() {
        List<string> found=null;
        Run("Scanning package candidates…",()=>{found=WindowsPlatform.Discover();return found.Count==0?"No Xe2 package candidates found.":"Choose a package from the dropdown, then Inspect. Candidates can be inactive.\r\n"+String.Join("\r\n",found);},
            ()=>{path.Items.Clear();foreach(var p in found)path.Items.Add(p);if(found.Count==1)path.SelectedIndex=0;current.Text="Current default cap: inspect the selected DLL.";});
    }
    void Inspect() {
        string p=path.Text;Inspection info=null;
        Run("Verifying SHA-256, executable mapping and instruction context…",()=>{
            info=Engine.Inspect(Engine.Read(p));
            return "Current default cap: "+info.MiB+" MiB\r\nRecognition: "+info.State+"\r\nVerified driver: "+info.Version+
                "\r\nFile version resource: "+FileVersionInfo.GetVersionInfo(p).FileVersion+"\r\nSHA-256: "+info.Hash+"\r\n"+WindowsPlatform.EmbeddedSignature(p)+
                "\r\nDefault per-cache-file cap; application profiles can override it.";
        },()=>current.Text="Current default cap: "+info.MiB+" MiB • driver "+info.Version);
    }
    void Observe() {Run("Observing game module paths…",()=>WindowsPlatform.Observe(true).ToString());}
    void CheckIsolation() {
        string exe=Isolation.FindWitcher();
        using(var d=new OpenFileDialog{Filter="Game executable|*.exe",Title="Select Witcher 3 DX12 for a read-only isolation check"}) {
            if(exe!=null){d.InitialDirectory=Path.GetDirectoryName(exe);d.FileName=Path.GetFileName(exe);}
            if(d.ShowDialog(this)!=DialogResult.OK)return;exe=d.FileName;
        }
        string selected=exe;Run("Checking game manifest and Windows redirection policy (read-only)…",()=>Isolation.Inspect(selected));
    }
    void EnableOverride() {
        if(!IsolationControl.Administrator){MessageBox.Show(this,"Close this app, right-click its executable and select Run as administrator. Then choose Enable developer override. This app will not elevate itself.","Administrator session required");return;}
        if(MessageBox.Show(this,"Enable HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\DevOverrideEnable = DWORD 1?\r\n\r\nThis affects .local DLL routing machine-wide, including manifested apps. The previous value is saved before changing it. Signature enforcement and driver files remain unchanged. You must reboot manually.\r\n\r\nReceipt: "+IsolationControl.ReceiptPath,"Enable developer override",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
        Run("Saving previous setting and enabling developer DLL redirection…",()=>IsolationControl.Enable());
    }
    void RestoreOverride() {
        if(MessageBox.Show(this,"Restore the original machine-wide developer setting from the saved receipt? You must reboot manually afterward.\r\n\r\n"+IsolationControl.ReceiptPath,"Restore developer setting",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        Run("Restoring the previous developer setting…",()=>IsolationControl.Restore());
    }
    void RemovePrivate() {
        using(var d=new OpenFileDialog{Filter="Private-copy receipt|*.json",Title="Select Witcher private-copy receipt"}) {
            if(d.ShowDialog(this)!=DialogResult.OK)return;string receipt=d.FileName;
            if(MessageBox.Show(this,"Remove ONLY the Witcher .local test copy recorded in this receipt? Altered contents will block removal.\r\n\r\n"+receipt,"Remove private copy",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
            Run("Validating and removing the owned Witcher private copy…",()=>IsolationControl.RemovePrivateCopy(receipt));
        }
    }
    void PreparePrivate() {
        MessageBox.Show(this,"The single-DLL test failed. Use Prepare matched original set instead, then Change private set cap after testing originals.","Single-DLL method unsupported");
    }
    void PrepareMatched() {
        string exe=Isolation.FindWitcher();if(exe==null){MessageBox.Show(this,"Witcher DX12 was not found.");return;}
        string driver,receipt;
        using(var d=new FolderBrowserDialog{Description="Select the original 9034 Intel package folder inside DriverStore"}){if(d.ShowDialog(this)!=DialogResult.OK)return;driver=d.SelectedPath;}
        using(var d=new SaveFileDialog{Filter="Matched-set receipt|*.json",FileName="witcher-matched-set.json",Title="Save recovery receipt outside the game folder"}){if(d.ShowDialog(this)!=DialogResult.OK)return;receipt=d.FileName;}
        if(MessageBox.Show(this,"Create a 608 MiB private ORIGINAL driver set for this Witcher build?\r\n\r\n"+exe+".local\r\n\r\nAll 27 files must match pinned hashes. System driver and Windows settings stay unchanged. Test gameplay before choosing a private patch.","Prepare matched original set",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
        Run("Verifying and copying the matched original Intel set…",()=>MatchedSet.Prepare(exe,driver,receipt,512));
    }
    void ChangeMatched() {
        using(var d=new OpenFileDialog{Filter="Matched-set receipt|*.json",Title="Choose matched-set recovery receipt"}) {
            if(d.ShowDialog(this)!=DialogResult.OK)return;string receipt=d.FileName;int mib=(int)cap.Value;
            if(mib!=512&&mib!=1024){MessageBox.Show(this,"Set the cap field to 512 (original) or 1024 (tested patch) for this experiment.");return;}
            if(MessageBox.Show(this,"Change ONLY this receipt's private Xe2 DLL to "+mib+" MiB? Witcher must be closed. The installed driver stays original. Cache behavior must still be measured.","Change private driver cap",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
            Run("Validating the entire private set and replacing only Xe2…",()=>MatchedSet.ChangeCap(receipt,mib));
        }
    }
    void RemoveMatched() {
        using(var d=new OpenFileDialog{Filter="Matched-set receipt|*.json",Title="Choose matched-set recovery receipt"}) {
            if(d.ShowDialog(this)!=DialogResult.OK)return;string receipt=d.FileName;
            if(MessageBox.Show(this,"Remove only the 27 pinned private DLLs recorded in this receipt? Changed or extra files block removal. Windows developer override is restored separately.","Remove matched set",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
            Run("Verifying and removing the matched private set…",()=>MatchedSet.Remove(receipt));
        }
    }
    void Prepare() {
        string source=path.Text;int mib=(int)cap.Value;
        using(var d=new FolderBrowserDialog{Description="Choose a parent folder for a NEW backup bundle"}) {
            if(d.ShowDialog(this)!=DialogResult.OK)return;
            string folder=Path.Combine(d.SelectedPath,"arc-cap-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));bundle.Text=folder;
            Run("Creating verified copy, backup and owner/group/DACL snapshot…",()=>{
                var b=Engine.Prepare(source,folder,mib);
                return "Bundle saved: "+folder+"\r\nDefault cap: "+b.Before.MiB+" → "+b.After.MiB+" MiB\r\nOutput SHA-256: "+b.After.Hash+
                    "\r\n"+WindowsPlatform.EmbeddedSignature(Path.Combine(folder,"patched.dll"))+"\r\nNo installed driver files changed.";
            });
        }
    }
    void OpenBundle() {using(var d=new FolderBrowserDialog{Description="Choose the bundle folder containing manifest.json"})if(d.ShowDialog(this)==DialogResult.OK)bundle.Text=d.SelectedPath;}
    void Guide() {
        MessageBox.Show(this,"1. Keep the complete bundle and an official Intel driver installer on a local drive.\r\n\r\n"+
            "2. Shift + Restart → Troubleshoot → Advanced options → Startup Settings → Restart → 4 (Safe Mode).\r\n\r\n"+
            "3. Manually run this app as administrator. Open the bundle. Select the consent checkbox and confirm Install or Roll back.\r\n\r\n"+
            "4. Reboot normally. Observe module paths while your game runs. Disk hashes do not prove runtime effect.\r\n\r\n"+
            "Access denied: the app stops without taking ownership. Use the official driver installer if recovery is needed. Interrupted operation: return to Safe Mode with this bundle and Roll back. Unknown bytes or changed ACL: do not overwrite blindly; reinstall the official package.\r\n\r\n"+
            "Safe Mode does not bypass signature enforcement. Offline Windows installation is not implemented.","Safe Mode and recovery guide",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }
    void Change(bool rollback) {
        if(!consent.Checked){MessageBox.Show(this,"Read and select the experimental replacement checkbox first.","Consent required");return;}
        string folder=bundle.Text;Bundle b;
        try{b=Engine.Validate(folder);}catch(Exception e){MessageBox.Show(this,e.Message,"Invalid bundle");return;}
        string action=rollback?"Roll back":"Install";
        if(MessageBox.Show(this,action+" this exact target?\r\n\r\n"+b.Target+"\r\n\r\nExpected current SHA-256:\r\n"+(rollback?b.After.Hash:b.Before.Hash)+
            "\r\n\r\nDesired SHA-256:\r\n"+(rollback?b.Before.Hash:b.After.Hash)+"\r\n\r\nSignature enforcement remains enabled. Loading and cache behavior are unverified.",
            action,MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
        Run(action+" — verifying environment, target, backup and ACL…",()=>{
            WindowsPlatform.RequireInstallEnvironment(b.Target);
            // Revalidate after the dialog; do not trust a manifest changed while consent was pending.
            var checkedBundle=Engine.Validate(folder);
            if(checkedBundle.Target!=b.Target||checkedBundle.Before.Hash!=b.Before.Hash||checkedBundle.After.Hash!=b.After.Hash||!Engine.Equivalent(checkedBundle.Security,b.Security))
                throw new InvalidDataException("Bundle changed after confirmation. Select and confirm it again.");
            return Engine.Replace(folder,Engine.Security,Engine.RestoreSecurity,rollback,b)+"\r\nReboot normally, then observe the module used by your game.";
        });
    }
}
static class Program {
    [STAThread]static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new MainForm());}
}
}
