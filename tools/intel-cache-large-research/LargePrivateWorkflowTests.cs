using System;using System.IO;using System.Linq;using System.Web.Script.Serialization;using ArcCap;
class LargePrivateWorkflowTests {
 static void Check(bool good,string message){if(!good)throw new Exception(message);Console.WriteLine("PASS "+message);}
 static int Main(string[]args){string root=Path.Combine(Path.GetTempPath(),"ArcLargeWorkflow-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{
  string exe=Path.Combine(root,"bin","x64_dx12","witcher3.exe");Directory.CreateDirectory(Path.GetDirectoryName(exe));File.Copy(args[0],exe);
  string receipt=Path.Combine(root,"receipt.json"),backups=Path.Combine(root,"backups");
  MatchedSet.Prepare(exe,args[1],receipt,512);var before=MatchedSet.Read(receipt);var acl=Engine.Security(Path.Combine(before.Folder,"igd12um64xe2.dll"));
  MatchedSet.ChangeCap(receipt,8192,backups);var large=MatchedSet.Read(receipt);MatchedSet.ValidateContents(large);
  Check(large.MiB==8192&&large.Files["igd12um64xe2.dll"]==LargeCachePatch.EightGiB,"isolated private set installs exact qualified 8 GiB image");
  Check(Engine.Equivalent(acl,Engine.Security(Path.Combine(large.Folder,"igd12um64xe2.dll"))),"semantic ACL preserved during large change");
  string journal=Directory.GetDirectories(backups).Single();var bundle=Engine.Validate(journal);
  Check(bundle.Before.MiB==512&&bundle.After.MiB==8192&&Engine.Equivalent(bundle.Security,acl)&&File.Exists(Path.Combine(journal,"game-receipt-before.json")),"previous DLL, next DLL, ACL and receipt backup verified");
  MatchedSet.ChangeCap(receipt,4096,backups);var reverted=MatchedSet.Read(receipt);MatchedSet.ValidateContents(reverted);
  Check(reverted.MiB==4096&&reverted.Files["igd12um64xe2.dll"]==Engine.FourGiB,"isolated private set reverts to exact known 4 GiB image");
  Check(Directory.GetDirectories(backups).Length==2,"reversion also preserves pre-change backup");
  MatchedSet.Remove(receipt);Check(!Directory.Exists(reverted.Folder)&&File.Exists(exe),"stock recovery removes only owned private set");
  return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}finally{Directory.Delete(root,true);}}
}
