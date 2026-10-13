using System;using System.IO;using System.Text.RegularExpressions;using System.Linq;using ArcCap;
class SafetyTests {
    static int passed;static void Check(bool yes,string name){if(!yes)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
    static void Main(){string root=Path.Combine(Path.GetTempPath(),"ArcSafetyTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            Check(GameSafety.Scan(root).ScanComplete,"Complete readable scan is distinguished from unknown");
            Check(!GameSafety.Assess(123,root).Allowed,"Unknown profile is blocked even with no indicators");
            Check(!GameSafety.Scan(Path.Combine(root,"missing")).ScanComplete,"Missing install fails closed");
            File.WriteAllText(Path.Combine(root,"benign.txt"),"");Check(!GameSafety.Scan(root,0).ScanComplete,"Entry budget fails closed");
            string nested=Path.Combine(root,"deep","hidden");Directory.CreateDirectory(nested);File.SetAttributes(nested,FileAttributes.Hidden);string marker=Path.Combine(nested,"easyanticheat_eos_setup.EXE");File.WriteAllText(marker,"");
            Check(GameSafety.Scan(root).Marker=="EasyAntiCheat_EOS_Setup.exe","Nested hidden anti-cheat marker blocks case-insensitively");
            Check(!GameSafety.Assess(292030,root).Allowed,"Reviewed single-player profile cannot override an anti-cheat indicator");File.Delete(marker);
            marker=Path.Combine(nested,"test_BE.exe");File.WriteAllText(marker,"");Check(GameSafety.Scan(root).Marker=="*_BE.exe","BattlEye launcher wildcard is detected");File.Delete(marker);
            marker=Path.Combine(nested,"protection.xem");File.WriteAllText(marker,"");Check(GameSafety.Scan(root).Marker=="*.xem","XIGNCODE suffix marker is detected");File.Delete(marker);
            string source=File.ReadAllText(Path.GetFullPath("..\\..\\..\\src\\SCSKiller.Core\\Games\\GameFiles.cs"));string block=Regex.Match(source,@"internal static readonly \(string Name, AntiCheat Kind\)\[\] Markers =\s*\[(.*?)\];",RegexOptions.Singleline).Groups[1].Value;
            var markers=Regex.Matches(block,"\\(\"([^\"]+)\", AntiCheat\\.(\\w+)\\)").Cast<Match>().Select(m=>m.Groups[1].Value).ToArray();Check(markers.SequenceEqual(GameSafety.Markers),"All detector markers match the existing SCSKiller rules");
        }finally{Directory.Delete(root,true);}Console.WriteLine("Passed "+passed+" safety checks; only temporary fixtures changed.");
    }
}
