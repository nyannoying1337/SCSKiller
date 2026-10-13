using System;using System.IO;using System.Runtime.InteropServices;using ArcCap;
class LargePatchTests {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
 [DllImport("kernel32.dll")]static extern bool FreeLibrary(IntPtr module);
 [DllImport("kernel32.dll")]static extern IntPtr RtlLookupFunctionEntry(ulong pc,out ulong image,IntPtr history);
 static int failures;
 static void Check(bool ok,string name){Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(!ok)failures++;}
 static void Reject(byte[] bytes,string name){try{Engine.Inspect(bytes);Check(false,name);}catch(InvalidDataException){Check(true,name);}}
 static int Main(string[] args){try{
  byte[] original=File.ReadAllBytes(args[0]);foreach(int mib in new[]{8192,16384}){
   byte[] patched=Engine.Patch(original,mib);Check(Engine.Inspect(patched).MiB==mib,"qualified experimental cap "+mib);
   Check(Engine.Hash(Engine.Patch(patched,4096))==Engine.FourGiB,"exact rollback to 4 GiB "+mib);
   Check(Engine.Hash(Engine.Patch(patched,512))==Engine.Original,"exact rollback to original "+mib);
   byte[] changed=(byte[])patched.Clone();changed[0x2000]^=1;Reject(changed,"unrelated modification rejected");
   changed=(byte[])patched.Clone();changed[Engine.Size+88]^=1;Reject(changed,"unwind mutation rejected");
   changed=(byte[])patched.Clone();changed[0x17aa21]^=1;Reject(changed,"reload branch mutation rejected");
   string path=Path.Combine(args[1],"experimental-"+mib+".dll");File.WriteAllBytes(path,patched);
   // DONT_RESOLVE_DLL_REFERENCES: maps PE for loader metadata QA, does not call
   // DllMain, resolve imports, create a device or execute driver code.
   IntPtr module=LoadLibraryEx(path,IntPtr.Zero,1);Check(module!=IntPtr.Zero,"Windows maps experimental PE without driver execution");
   if(module!=IntPtr.Zero){ulong image;IntPtr entry=RtlLookupFunctionEntry((ulong)module.ToInt64()+0x287600b,out image,IntPtr.Zero);Check(entry!=IntPtr.Zero&&image==(ulong)module.ToInt64(),"Windows finds appended function unwind metadata");entry=RtlLookupFunctionEntry((ulong)module.ToInt64()+0x17b550,out image,IntPtr.Zero);Check(entry!=IntPtr.Zero,"original reload function unwind metadata preserved");FreeLibrary(module);}
  }
  Check(Engine.Inspect(Engine.Patch(Engine.Patch(original,8192),16384)).MiB==16384,"8 to 16 GiB change is deterministic");
  return failures==0?0:1;
 }catch(Exception e){Console.WriteLine(e);return 2;}}
}
