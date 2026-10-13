using System;
using System.IO;
using ArcCap;
class Probe {
 static int Main(string[] args){try{bool needs=MatchedSet.NeedsDebugRoutingFix(args[0]);if(!needs)throw new Exception("Expected known x86 helper conflict");Console.WriteLine("PASS known x86 debug helper detected read-only");var r=new MatchedSetReceipt{Executable=args[0],Schema=3};bool rejected=false;try{MatchedSet.RequireDebugCompatibility(r);}catch(InvalidDataException){rejected=true;}if(!rejected)throw new Exception("Old setup allowed");Console.WriteLine("PASS legacy receipt blocked before cap change");r.Schema=4;MatchedSet.RequireDebugCompatibility(r);MatchedSet.ValidateDebugSources();Console.WriteLine("PASS qualified helper schema and source hashes accepted");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
