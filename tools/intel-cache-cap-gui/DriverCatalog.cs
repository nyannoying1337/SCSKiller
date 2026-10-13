using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Diagnostics;using Microsoft.Win32;using System.Runtime.InteropServices;using System.Text;
namespace ArcCap {
public class AdapterPackage {public string Adapter,Version,Folder;}
public class DriverFileReview {public string Name,Version,Hash,Signature,State,Error;public long Size;public int CandidateCount;public int[] CandidateOffsets;public bool PatchEnabled;}
public static class DriverCatalog {
 public const string KnownVersion="32.0.101.9034",Xe3Original="029E091F5B67A7418B39900E2AC4EAC9EF64767595E0138FF6569CC42FEB4911";
 public static readonly string[] Names={"igd12um64xe2.dll","igd12um64xe3.dll","igd12um64xeh.dll"};
 [StructLayout(LayoutKind.Sequential)]struct DeviceInfo {public uint Size;public Guid ClassGuid;public uint DevInst;public UIntPtr Reserved;}
 [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr SetupDiGetClassDevs(ref Guid guid,string enumerator,IntPtr window,uint flags);
 [DllImport("setupapi.dll",SetLastError=true)]static extern bool SetupDiEnumDeviceInfo(IntPtr set,uint index,ref DeviceInfo info);
 [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetupDiGetDeviceRegistryProperty(IntPtr set,ref DeviceInfo info,uint property,out uint type,byte[] buffer,uint size,out uint required);
 [DllImport("setupapi.dll")]static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
 [DllImport("cfgmgr32.dll")]static extern uint CM_Get_DevNode_Status(out uint status,out uint problem,uint node,uint flags);
 static HashSet<string> ActiveClassKeys(){
  Guid guid=new Guid("4d36e968-e325-11ce-bfc1-08002be10318");IntPtr set=SetupDiGetClassDevs(ref guid,null,IntPtr.Zero,2);
  if(set==new IntPtr(-1))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());var keys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  try{for(uint i=0;;i++){var info=new DeviceInfo{Size=(uint)Marshal.SizeOf(typeof(DeviceInfo))};if(!SetupDiEnumDeviceInfo(set,i,ref info)){if(Marshal.GetLastWin32Error()==259)break;throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
   uint status,problem;if(CM_Get_DevNode_Status(out status,out problem,info.DevInst,0)!=0||(status&8)==0)continue;
   uint type,required;byte[] value=new byte[1024];if(!SetupDiGetDeviceRegistryProperty(set,ref info,9,out type,value,(uint)value.Length,out required)||type!=1||required>value.Length)continue;
   string name=Encoding.Unicode.GetString(value,0,(int)required).TrimEnd('\0');string prefix="{"+guid.ToString()+"}\\";
   if(name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)){string sub=name.Substring(prefix.Length);if(sub.Length==4&&sub.All(Char.IsDigit))keys.Add(sub);}
  }}finally{SetupDiDestroyDeviceInfoList(set);}return keys;
 }
 public static List<AdapterPackage> Packages(){
  var active=ActiveClassKeys();var result=new List<AdapterPackage>();const string root=@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
  using(var hive=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))using(var display=hive.OpenSubKey(root)){
   if(display==null)return result;
   foreach(string sub in display.GetSubKeyNames().Where(n=>active.Contains(n)))using(var key=display.OpenSubKey(sub)){
    string adapter=key.GetValue("DriverDesc") as string,provider=key.GetValue("ProviderName") as string;
    if(((adapter??"")+" "+(provider??"")).IndexOf("Intel",StringComparison.OrdinalIgnoreCase)<0)continue;
    object raw=key.GetValue("UserModeDriverName");string[] names=raw as string[]??(raw is string?new[]{(string)raw}:new string[0]);
    foreach(string name in names){string path=Environment.ExpandEnvironmentVariables(name);if(!Path.IsPathRooted(path)||!(Path.GetFileName(path).Equals("igd12umd64.dll",StringComparison.OrdinalIgnoreCase)||Names.Contains(Path.GetFileName(path),StringComparer.OrdinalIgnoreCase)))continue;
     string folder=Path.GetDirectoryName(Path.GetFullPath(path));try{Engine.CheckPath(folder);}catch(IOException){continue;}catch(UnauthorizedAccessException){continue;}result.Add(new AdapterPackage{Adapter=adapter??"Intel GPU",Version=key.GetValue("DriverVersion") as string,Folder=folder});break;}
   }
  }return result;
 }
 public static AdapterPackage Selected(){var packages=Packages();return packages.FirstOrDefault(p=>p.Adapter.IndexOf("B580",StringComparison.OrdinalIgnoreCase)>=0)??packages.FirstOrDefault();}
 public static string Identity(AdapterPackage p){
  if(p==null)return "no-active-intel-package";var fields=new List<string>{p.Adapter,p.Version,p.Folder};
  foreach(string name in MatchedSet.Originals.Keys.Concat(Names).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n=>n,StringComparer.OrdinalIgnoreCase)){
   string file=Path.Combine(p.Folder,name);var info=new FileInfo(file);fields.Add(name+":"+(info.Exists?info.Length+":"+info.LastWriteTimeUtc.Ticks:"missing"));
  }return Engine.Hash(System.Text.Encoding.UTF8.GetBytes(String.Join("\n",fields)));
 }
 public static int[] CandidateOffsets(byte[] bytes){
  byte[] signature=Engine.Hex("48895c2408574883ec2033ff48c741100000002048b80000000001000000893948894108");var found=new List<int>();
  if(bytes.Length<256||bytes[0]!=77||bytes[1]!=90)return found.ToArray();int nt=BitConverter.ToInt32(bytes,0x3c);
  if(nt<0||nt>bytes.Length-26||BitConverter.ToUInt32(bytes,nt)!=0x4550||BitConverter.ToUInt16(bytes,nt+4)!=0x8664)return found.ToArray();
  int table=nt+24+BitConverter.ToUInt16(bytes,nt+20),count=BitConverter.ToUInt16(bytes,nt+6);
  if(count>96||table<0||table>bytes.Length-count*40)return found.ToArray();
  for(int section=0;section<count;section++){int q=table+40*section;if((BitConverter.ToUInt32(bytes,q+36)&0x20000000)==0)continue;
   uint start=BitConverter.ToUInt32(bytes,q+20),size=BitConverter.ToUInt32(bytes,q+16);if((ulong)start+size>(ulong)bytes.Length)continue;
   int end=(int)(start+size)-signature.Length;
   for(int i=(int)start;i<=end;i++){if(bytes[i]!=signature[0])continue;int j=1;while(j<signature.Length&&bytes[i+j]==signature[j])j++;if(j==signature.Length){found.Add(i);if(found.Count>=64)return found.ToArray();}}
  }return found.ToArray();
 }
 public static DriverFileReview Review(string file,bool signatures){
  var r=new DriverFileReview{Name=Path.GetFileName(file),State="Missing",CandidateOffsets=new int[0]};
  try{Engine.CheckPath(file);var info=new FileInfo(file);r.Size=info.Length;if(r.Size<256||r.Size>128L*1024*1024)throw new InvalidDataException("DLL size outside review bounds.");
   byte[] bytes=File.ReadAllBytes(file);r.Hash=Engine.Hash(bytes);r.Version=FileVersionInfo.GetVersionInfo(file).FileVersion;r.CandidateOffsets=CandidateOffsets(bytes);r.CandidateCount=r.CandidateOffsets.Length;
   if(r.Name.Equals("igd12um64xe2.dll",StringComparison.OrdinalIgnoreCase)){try{var inspection=Engine.Inspect(bytes);r.State=inspection.Hash==Engine.Original?"QualifiedOriginal":"QualifiedModified";r.PatchEnabled=inspection.Hash==Engine.Original;}catch(InvalidDataException){r.State="Unqualified";}}
   else if(r.Name.Equals("igd12um64xe3.dll",StringComparison.OrdinalIgnoreCase)&&r.Hash==Xe3Original)r.State="Xe3ConstructorReviewedHardwareUnverified";
   else r.State="Unqualified";
   if(signatures)r.Signature=WindowsPlatform.EmbeddedSignature(file);
  }catch(Exception e){r.Error=e.Message;r.State="Unavailable";}return r;
 }
 public static bool CohortQualified(AdapterPackage p,out string reason){
  if(p==null){reason="No active Intel package found.";return false;}
  if(p.Adapter.IndexOf("B580",StringComparison.OrdinalIgnoreCase)<0||p.Version!=KnownVersion){reason="Adapter/driver version has no qualified private-copy definition.";return false;}
  foreach(var entry in MatchedSet.Originals){string file=Path.Combine(p.Folder,entry.Key);try{Engine.CheckPath(file);if(IsolationControl.FileHash(file)!=entry.Value){reason="Unqualified package file: "+entry.Key;return false;}}catch(Exception e){reason=e.Message;return false;}}
  reason="Exact supported Xe2 package; all private-copy dependencies match.";return true;
 }
}
}
