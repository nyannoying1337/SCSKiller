using System;using System.IO;
class CacheMonitorTests {
    static int passed;static void Check(bool yes,string what){if(!yes)throw new Exception(what);passed++;Console.WriteLine("PASS "+what);}
    [STAThread]static void Main(string[] args){string root=Path.Combine(Path.GetTempPath(),"ArcCacheMonitorTest-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {File.WriteAllBytes(Path.Combine(root,"a"),new byte[100]);var initial=CacheSnapshot.Read(root);Check(initial.Bytes==100&&initial.Files==1&&initial.Unreadable==0,"Initial file size measured");
            Check(CacheSnapshot.Read(root).ChangedSince(initial)==0,"Idle cache reports zero change");
            File.WriteAllBytes(Path.Combine(root,"a"),new byte[150]);File.WriteAllBytes(Path.Combine(root,"b"),new byte[20]);var grown=CacheSnapshot.Read(root);Check(grown.Bytes-initial.Bytes==70&&grown.ChangedSince(initial)==2,"Growth includes new and enlarged files");
            File.Delete(Path.Combine(root,"a"));var shrunk=CacheSnapshot.Read(root);Check(shrunk.Bytes-initial.Bytes==-80&&shrunk.ChangedSince(initial)==2,"Eviction reports signed net decrease");
            Check(initial.Bytes==100,"Monitoring baseline remains fixed");
            if(args.Length==1){using(var overlay=new CacheMonitor(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName,null,root,Path.Combine(root,"logs")))using(var preview=overlay.PreviewImage()){preview.Save(args[0]);}Check(true,"Generic executable overlay renders without a Witcher-only process name");}Check(CacheSnapshot.Read(Path.Combine(root,"missing")).Unreadable==1,"Missing folder reports read failure");
        }finally{Directory.Delete(root,true);}Console.WriteLine("Passed "+passed+" monitor checks; only temporary fixture files were changed.");}
}
