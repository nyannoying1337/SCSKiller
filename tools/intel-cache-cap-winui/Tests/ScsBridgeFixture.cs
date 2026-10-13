using System;
using System.Threading;
class Fixture {
    static int Main(string[] args) {
        Console.WriteLine("fixture:"+String.Join("|",args));
        Console.Error.WriteLine("intel="+Environment.GetEnvironmentVariable("SCSKILLER_EXPERIMENTAL_INTEL"));
        if(args.Length>1&&args[1]=="steam:1")Thread.Sleep(30000);
        return args.Length>1&&args[1]=="steam:2"?7:0;
    }
}
