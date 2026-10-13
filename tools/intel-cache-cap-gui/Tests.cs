using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace ArcCap {
static class Tests {
    static byte[] original;static int passed;static List<string> report=new List<string>();
    static void Check(bool value,string error){if(!value)throw new Exception(error);}
    static void Reject(Action action){try{action();}catch(InvalidDataException){return;}throw new Exception("Operation should have been blocked");}
    static void Test(string name,Action body){body();passed++;string line="PASS "+name;report.Add(line);Console.WriteLine(line);}
    static void Temporary(Action<string,string> body) {
        string dir=Path.Combine(Path.GetTempPath(),"ArcCapTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try {string target=Path.Combine(dir,"igd12um64xe2.dll"),folder=Path.Combine(dir,"bundle");File.WriteAllBytes(target,original);Engine.Prepare(target,folder,1024);body(target,folder);}
        finally { // Only this freshly created test directory is removed.
            if(!Path.GetFullPath(dir).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))throw new Exception("Unexpected cleanup path");
            Directory.Delete(dir,true);
        }
    }
    static int Main(string[] args) {
        try {
            if(args.Length<1){Console.Error.WriteLine("Pass the supported original DLL path, and optionally a report path.");return 2;}
            original=Engine.Read(args[0]);Check(Engine.Hash(original)==Engine.Original,"Original fixture hash differs");
            Test("known original and 1 GiB hash, exact changed byte",()=>{
                Check(Engine.Inspect(original).MiB==512,"Original cap");var after=Engine.Patch(original,1024);Check(Engine.Hash(after)==Engine.OneGiB,"Output hash");
                var changed=Enumerable.Range(0,original.Length).Where(i=>original[i]!=after[i]).ToArray();Check(changed.SequenceEqual(new[]{0x18BC63}),"Unexpected byte diff");
            });
            Test("bounds and cap-only normalization",()=>{
                foreach(int n in new[]{128,512,1024,2047,2048,4096})Check(Engine.Inspect(Engine.Patch(original,n)).MiB==n,"Cap recognition");
                foreach(int n in new[]{0,127,2049,4095,4097,8193,32768})Reject(()=>Engine.Patch(original,n));
            });
            Test("wide patches pin full hashes and round-trip to original / 1 GiB",()=>{
                foreach(int cap in new[]{2048,4096}) {
                    var wide=Engine.Patch(original,cap);Check(Engine.Hash(wide)==(cap==2048?Engine.TwoGiB:Engine.FourGiB),"Wide hash mismatch");
                    Check(Engine.Patch(wide,512).SequenceEqual(original),"Wide rollback differs");Check(Engine.Hash(Engine.Patch(wide,1024))==Engine.OneGiB,"Wide to 1 GiB differs");
                    wide[0x18BC70]^=1;Reject(()=>Engine.Inspect(wide));
                }
            });
            Test("unknown bytes refused",()=>{var b=(byte[])original.Clone();b[0x190000]^=1;Reject(()=>Engine.Patch(b,1024));});
            Test("malformed PE refused",()=>{var b=(byte[])original.Clone();Array.Copy(new byte[]{255,255,255,255},0,b,0x3c,4);Reject(()=>Engine.Inspect(b));});
            Test("AI equivalent, access/protection/owner changes differ",()=>{
                string s="O:SYG:SYD:(A;ID;FA;;;SY)(A;ID;0x1200a9;;;WD)";
                Check(Engine.Equivalent(Engine.Canonical(s),Engine.Canonical(s.Replace("D:","D:AI"))),"AI difference");
                Check(!Engine.Equivalent(Engine.Canonical(s),Engine.Canonical(s.Replace("0x1200a9","FA"))),"Rights difference");
                Check(!Engine.Equivalent(Engine.Canonical(s),Engine.Canonical(s.Replace("D:","D:P"))),"Protection difference");
                Check(!Engine.Equivalent(Engine.Canonical(s),Engine.Canonical(s.Replace("O:SY","O:BA"))),"Owner difference");
            });
            Test("real Windows ACL backup, install and rollback on temporary file",()=>Temporary((target,folder)=>{
                var b=Engine.Validate(folder);Check(Engine.Equivalent(Engine.Security(target),b.Security),"ACL backup");
                Engine.Replace(folder,Engine.Security,Engine.RestoreSecurity,false);Check(Engine.Hash(Engine.Read(target))==Engine.OneGiB,"Install");
                Engine.Replace(folder,Engine.Security,Engine.RestoreSecurity,true);Check(Engine.Read(target).SequenceEqual(original),"Rollback");
                Check(Engine.Equivalent(Engine.Security(target),b.Security),"ACL rollback");
            }));
            Test("target drift refused",()=>Temporary((target,folder)=>{var b=Engine.Read(target);b[100]^=1;File.WriteAllBytes(target,b);Reject(()=>Engine.Replace(folder,Engine.Security,Engine.RestoreSecurity,false));Check(Engine.Read(target).SequenceEqual(b),"Target overwritten");}));
            Test("tampered bundle refused",()=>Temporary((target,folder)=>{string file=Path.Combine(folder,"patched.dll");var b=Engine.Read(file);b[100]^=1;File.WriteAllBytes(file,b);Reject(()=>Engine.Validate(folder));}));
            Test("ACL drift refused",()=>Temporary((target,folder)=>{
                var altered=Engine.Security(target);altered.Owner="altered";Reject(()=>Engine.Replace(folder,p=>altered,Engine.RestoreSecurity,false));Check(Engine.Read(target).SequenceEqual(original),"Target changed");
            }));
            Test("ACL failure leaves target unchanged",()=>Temporary((target,folder)=>{
                bool failed=false;try{Engine.Replace(folder,Engine.Security,(p,a)=>{throw new UnauthorizedAccessException("simulated denial");},false);}catch(UnauthorizedAccessException){failed=true;}
                Check(failed&&Engine.Read(target).SequenceEqual(original),"Denial modified target");Check(Engine.Validate(folder).Status=="failed-target-unchanged","Failure journal");
            }));
            Test("locked target leaves bytes unchanged",()=>Temporary((target,folder)=>{
                bool failed=false;using(var locked=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.Read)){try{Engine.Replace(folder,Engine.Security,Engine.RestoreSecurity,false);}catch(IOException){failed=true;}}
                Check(failed&&Engine.Read(target).SequenceEqual(original),"Locked replacement should fail");Check(Engine.Validate(folder).Status=="failed-target-unchanged","Failure journal");
            }));
            Test("post-replacement failure journal and rollback",()=>Temporary((target,folder)=>{
                bool failed=false;try{Engine.Replace(folder,p=>{if(p==target&&Engine.Hash(Engine.Read(p))==Engine.OneGiB)throw new UnauthorizedAccessException("simulated post-replace query failure");return Engine.Security(p);},Engine.RestoreSecurity,false);}catch(UnauthorizedAccessException){failed=true;}
                Check(failed&&Engine.Validate(folder).Status=="recovery-required","Recovery journal");
                Engine.Replace(folder,Engine.Security,Engine.RestoreSecurity,true);Check(Engine.Read(target).SequenceEqual(original),"Recovery rollback");
            }));
            Test("confirmation mismatch refused",()=>Temporary((target,folder)=>{var approved=Engine.Validate(folder);approved.Target+=".other";Reject(()=>Engine.Replace(folder,Engine.Security,Engine.RestoreSecurity,false,approved));Check(Engine.Read(target).SequenceEqual(original),"Target changed");}));
            Test("normal mode installation gate refuses",()=>{bool failed=false;try{WindowsPlatform.RequireInstallEnvironment("C:\\not-a-driver.dll");}catch(InvalidOperationException){failed=true;}Check(failed,"Environment gate did not block");});
            Test("manifest isolation policy fails closed without developer override",()=>{
                Check(!Isolation.DotLocalCandidate(true,false),"Manifest must block simple redirection");
                Check(Isolation.DotLocalCandidate(false,false),"Unmanifested executable is a candidate, not a guarantee");
                Check(Isolation.DotLocalCandidate(true,true),"Existing developer override permits candidate evaluation");
            });
            Test("resource-only application manifest inspection",()=>{
                string exe=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"IntelArcCacheCap.exe");
                Check(Isolation.Manifest(exe).Contains("asInvoker"),"Native GUI manifest should be detected");
            });
            Test("developer setting rejects unelevated writes",()=>{
                if(IsolationControl.Administrator)throw new Exception("Run this test suite unelevated; it must not alter Windows settings");
                bool rejected=false;try{IsolationControl.Enable();}catch(InvalidOperationException){rejected=true;}Check(rejected,"Enable must require manual elevation");
            });
            Test("unknown game build refuses private-copy setup",()=>Temporary((target,folder)=>{
                string gameDir=Path.Combine(Path.GetDirectoryName(target),"x64_dx12");Directory.CreateDirectory(gameDir);string exe=Path.Combine(gameDir,"witcher3.exe");File.WriteAllText(exe,"unknown executable");
                Reject(()=>IsolationControl.PreparePrivateCopy(exe,target,Path.Combine(gameDir,"receipt.json"),1024));Check(!Directory.Exists(exe+".local"),"Unknown game setup changed files");
            }));
            string gameFixture=Isolation.FindWitcher();
            if(gameFixture!=null&&System.Diagnostics.Process.GetProcessesByName("witcher3").Length==0)Test("Witcher private-copy preparation, conflict refusal and owned removal on TEMPORARY copy",()=>Temporary((target,folder)=>{
                string gameDir=Path.Combine(Path.GetDirectoryName(target),"x64_dx12");Directory.CreateDirectory(gameDir);string exe=Path.Combine(gameDir,"witcher3.exe");File.Copy(gameFixture,exe);
                string receipt=Path.Combine(Path.GetDirectoryName(target),"private-copy.json");var r=IsolationControl.PreparePrivateCopy(exe,target,receipt,1024);
                Check(File.Exists(Path.Combine(r.Folder,"igd12um64xe2.dll"))&&r.DllHash==Engine.OneGiB,"Private copy failed");
                bool rejected=false;try{IsolationControl.PreparePrivateCopy(exe,target,receipt,1024);}catch(IOException){rejected=true;}Check(rejected,"Existing setup must not be overwritten");
                string extra=Path.Combine(r.Folder,"unowned.txt");File.WriteAllText(extra,"unowned");Reject(()=>IsolationControl.RemovePrivateCopy(receipt));Check(File.Exists(extra),"Unowned file was deleted");File.Delete(extra);
                IsolationControl.RemovePrivateCopy(receipt);Check(!Directory.Exists(r.Folder)&&File.Exists(exe),"Removal affected original executable");
                File.WriteAllText(Path.Combine(gameDir,"d3d12.dll"),"unverified local proxy");
                Reject(()=>IsolationControl.PreparePrivateCopy(exe,target,Path.Combine(Path.GetDirectoryName(target),"second-receipt.json"),1024));
                Check(!Directory.Exists(r.Folder),"Local proxy guard must prevent setup");
            }));
            Test("preflight detects an existing D3D12 proxy without loading it",()=>{
                Temporary((target,folder)=>{
                    string local=Path.Combine(Path.GetDirectoryName(target),"d3d12.dll");File.WriteAllText(local,"unverified proxy");
                    Check(Isolation.LocalD3D12(target)==local,"Local proxy must be detected");Reject(()=>Isolation.RequireNoLocalD3D12(target));
                    Check(File.ReadAllText(local)=="unverified proxy","Inspection must not change or execute the proxy");
                });
            });
            Test("native module enumeration sees this process kernel32",()=>{
                Check(WindowsPlatform.ModulePaths(System.Diagnostics.Process.GetCurrentProcess().Id).Any(p=>Path.GetFileName(p).Equals("kernel32.dll",StringComparison.OrdinalIgnoreCase)),"Native module enumeration failed");
            });
            Test("matched receipt rejects changed profile, extra DLL and unsupported cap",()=>{
                var r=new MatchedSetReceipt{Schema=3,Executable=@"C:\Temporary\bin\x64_dx12\witcher3.exe",Folder=@"C:\Temporary\bin\x64_dx12\witcher3.exe.local",MiB=512,Files=new Dictionary<string,string>(MatchedSet.Originals)};
                MatchedSet.ValidateReceipt(r);r.Files["igd12um64xe2.dll"]=Engine.OneGiB;r.MiB=1024;MatchedSet.ValidateReceipt(r);
                r.Files["igd12um64xe2.dll"]=Engine.TwoGiB;r.MiB=2048;MatchedSet.ValidateReceipt(r);
                r.Files["igd12um64xe2.dll"]=Engine.FourGiB;r.MiB=4096;MatchedSet.ValidateReceipt(r);
                r.Files["igd12um64xe2.dll"]=Engine.OneGiB;r.MiB=1024;
                r.MiB=2047;Reject(()=>MatchedSet.ValidateReceipt(r));r.MiB=1024;
                r.Files.Add("unowned.dll",Engine.Original);Reject(()=>MatchedSet.ValidateReceipt(r));r.Files.Remove("unowned.dll");
                r.Files["igd12umd64.dll"]=Engine.Original;Reject(()=>MatchedSet.ValidateReceipt(r));
            });
            Test("debug-helper receipt pins both helpers and rejects missing or unowned entries",()=>{
                var r=new MatchedSetReceipt{Schema=4,Executable=@"C:\Temporary\bin\x64_dx12\witcher3.exe",Folder=@"C:\Temporary\bin\x64_dx12\witcher3.exe.local",MiB=512,Files=new Dictionary<string,string>(MatchedSet.Originals)};
                foreach(var f in MatchedSet.DebugHelpers)r.Files.Add(f.Key,f.Value);MatchedSet.ValidateReceipt(r);
                r.Files["dbghelp.dll"]=Engine.Original;Reject(()=>MatchedSet.ValidateReceipt(r));r.Files["dbghelp.dll"]=MatchedSet.DebugHelpers["dbghelp.dll"];
                r.Schema=3;Reject(()=>MatchedSet.ValidateReceipt(r));r.Schema=4;
                r.Files.Remove("dbgcore.dll");Reject(()=>MatchedSet.ValidateReceipt(r));r.Files.Add("unowned.dll",Engine.Original);Reject(()=>MatchedSet.ValidateReceipt(r));
            });
            Test("qualified Windows debug sources have exact hashes",()=>MatchedSet.ValidateDebugSources());
            Test("unknown or malformed local debug helper blocks instead of being replaced",()=>Temporary((target,folder)=>{
                string exe=Path.Combine(Path.GetDirectoryName(target),"game.exe"),helper=Path.Combine(Path.GetDirectoryName(target),"dbghelp.dll");
                Check(!MatchedSet.NeedsDebugRoutingFix(exe),"No helper incorrectly needs fix");File.WriteAllText(helper,"unqualified DLL");Reject(()=>MatchedSet.NeedsDebugRoutingFix(exe));Check(File.ReadAllText(helper)=="unqualified DLL","Inspection changed bundled file");
            }));
            if(System.Diagnostics.Process.GetProcessesByName("witcher3").Length==0)Test("29-file recovery removes tracked Windows helpers and preserves bundled game files",()=>Temporary((target,folder)=>{
                string exe=Path.Combine(Path.GetDirectoryName(target),"bin","x64_dx12","witcher3.exe"),local=exe+".local";Directory.CreateDirectory(local);
                string bundled=Path.Combine(Path.GetDirectoryName(exe),"dbghelp.dll");File.WriteAllText(bundled,"bundled game file must survive");
                var r=new MatchedSetReceipt{Schema=4,Executable=exe,Folder=local,MiB=512,Status="prepared-runtime-unverified",Files=new Dictionary<string,string>(MatchedSet.Originals)};
                string driver=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),@"System32\DriverStore\FileRepository\iigd_dch_d.inf_amd64_6a5efc7401ff0ebc");
                foreach(var f in MatchedSet.Originals)File.Copy(Path.Combine(driver,f.Key),Path.Combine(local,f.Key));
                foreach(var f in MatchedSet.DebugHelpers){r.Files.Add(f.Key,f.Value);File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),f.Key),Path.Combine(local,f.Key));}
                MatchedSet.ValidateContents(r);string receipt=Path.Combine(Path.GetDirectoryName(target),"debug-recovery.json");File.WriteAllText(receipt,new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(r));
                MatchedSet.Remove(receipt);Check(!Directory.Exists(local)&&MatchedSet.Read(receipt).Status=="removed","Tracked files not fully removed");Check(File.ReadAllText(bundled)=="bundled game file must survive","Bundled DLL changed during rollback");
            }));
            Test("runtime profiles need no title whitelist and recovery metadata is self-contained",()=>{
                var profile=new MatchedSet.ProfileInfo{Schema=1,AppId=987654,Name="Fixture",Root=@"C:\Temporary\ObservedGame",RelativeExecutable=@"Engine\Win64\renderer.exe",Hash=Engine.Original,ObservedDriverHash=Engine.Original};
                string exe=MatchedSet.ProfileExecutable(profile);
                var receipt=new MatchedSetReceipt{Schema=3,Executable=exe,Folder=exe+".local",MiB=512,Files=new Dictionary<string,string>(MatchedSet.Originals),Game=profile};
                MatchedSet.ValidateReceipt(receipt);
                profile.RelativeExecutable=@"..\escaped.exe";Reject(()=>MatchedSet.ProfileExecutable(profile));profile.RelativeExecutable=@"Engine\Win64\renderer.exe";
                profile.ObservedDriverHash=Engine.OneGiB;Reject(()=>MatchedSet.ValidateReceipt(receipt));profile.ObservedDriverHash=Engine.Original;
                profile.Hash="unknown";Reject(()=>MatchedSet.ValidateReceipt(receipt));profile.Hash=Engine.Original;
                receipt.Executable=@"C:\Temporary\Other\renderer.exe";Reject(()=>MatchedSet.ValidateReceipt(receipt));
            });
            Test("matched receipt rejects arbitrary folder and changed contents",()=>Temporary((target,folder)=>{
                var r=new MatchedSetReceipt{Schema=3,Executable=Path.Combine(Path.GetDirectoryName(target),"bin","x64_dx12","witcher3.exe"),MiB=512,Files=new Dictionary<string,string>(MatchedSet.Originals)};
                r.Folder=folder;Reject(()=>MatchedSet.ValidateReceipt(r));r.Folder=r.Executable+".local";Directory.CreateDirectory(r.Folder);
                File.WriteAllText(Path.Combine(r.Folder,"unowned.txt"),"preserve");Reject(()=>MatchedSet.ValidateContents(r));
                Check(File.ReadAllText(Path.Combine(r.Folder,"unowned.txt"))=="preserve","Validation deleted unowned contents");
            }));
            if(args.Length>2)Test("live matched-set receipt and all pinned files validate READ ONLY",()=>MatchedSet.ValidateContents(MatchedSet.Read(args[2])));
            if(System.Diagnostics.Process.GetProcessesByName("witcher3").Length>0)report.Add("SKIP temporary single-DLL setup/removal: Witcher running; process guard intentionally applies.");
            Console.WriteLine("Passed "+passed+" tests. No installed driver replacement performed.");
            if(args.Length>1)File.WriteAllLines(args[1],report.Concat(new[]{"Passed "+passed+" tests; transactions used temporary files and real Windows ACL APIs."}));return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
}
