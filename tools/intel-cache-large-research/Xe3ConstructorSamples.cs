using System;using System.IO;using System.Linq;using ArcCap;
class Xe3ConstructorSamples {
 static int Main(string[]args){try{
  byte[] original=File.ReadAllBytes(args[0]);if(original.Length!=42191848||Engine.Hash(original)!=DriverCatalog.Xe3Original)throw new InvalidDataException("Exact Xe3 32.0.101.9034 research image required.");
  var offsets=DriverCatalog.CandidateOffsets(original);if(offsets.Length!=1||offsets[0]!=0x1900e0)throw new InvalidDataException("Xe3 executable signature mismatch.");
  foreach(int mib in new[]{2048,4096}){
   byte[] output=(byte[])original.Clone();byte[] patch=Engine.Hex(mib==2048?"48B800000000010000004889410848D1E848894110893990":"48B800000000010000004889410848894110893990909090");
   Array.Copy(patch,0,output,0x1900ec,24);byte[] normalized=(byte[])output.Clone();Array.Copy(original,0x1900ec,normalized,0x1900ec,24);
   if(!normalized.SequenceEqual(original))throw new InvalidDataException("Unrelated Xe3 changes.");
   File.WriteAllBytes(Path.Combine(args[1],"xe3-constructor-only-"+mib+".dll"),output);Console.WriteLine("RESEARCH ONLY "+mib+" MiB SHA-256 "+Engine.Hash(output));
  }return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
