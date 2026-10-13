using System;using System.IO;using ArcCap;
class DriverCatalogTests {
 static int count;static void Check(bool good,string name){if(!good)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
 static int Main(string[]args){string root=Path.Combine(Path.GetTempPath(),"ArcDriverReview-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{
  var package=new AdapterPackage{Adapter="Intel Arc B580",Version="32.0.101.9034",Folder=root};string initial=DriverCatalog.Identity(package);
  Check(initial==DriverCatalog.Identity(package),"unchanged metadata has stable identity");
  File.WriteAllText(Path.Combine(root,"igd12um64xe2.dll"),"test fixture");Check(initial!=DriverCatalog.Identity(package),"new DLL changes package identity without hashing DLL contents");
  string identity=DriverCatalog.Identity(package);package.Version="32.0.101.9999";Check(identity!=DriverCatalog.Identity(package),"registry driver version changes identity");string reason;
  Check(!DriverCatalog.CohortQualified(package,out reason),"unknown new version remains blocked even on B580");
  package.Version=DriverCatalog.KnownVersion;package.Adapter="Intel Arc B390";Check(!DriverCatalog.CohortQualified(package,out reason),"Xe3 hardware cannot inherit Xe2 qualification");
  byte[] original=File.ReadAllBytes(args[0]);Check(DriverCatalog.CandidateOffsets(original).Length==1,"qualified Xe2 constructor is found once in executable section");
  byte[] xe3=File.ReadAllBytes(args[1]);Check(DriverCatalog.CandidateOffsets(xe3).Length==1,"Xe3 constructor is found independently at a different offset");
  string candidate=Path.Combine(root,"igd12um64xe3.dll");File.WriteAllBytes(candidate,xe3);var review=DriverCatalog.Review(candidate,false);
  Check(review.State=="Xe3ConstructorReviewedHardwareUnverified"&&!review.PatchEnabled,"Xe3 image review never enables a game patch");
  xe3[0x2000]^=1;File.WriteAllBytes(candidate,xe3);review=DriverCatalog.Review(candidate,false);
  Check(review.CandidateCount==1&&review.State=="Unqualified"&&!review.PatchEnabled,"matching signature in unknown image never enables patching");
  byte[] malformed=new byte[512];Check(DriverCatalog.CandidateOffsets(malformed).Length==0,"malformed PE has no trusted signature candidate");
  Console.WriteLine("Passed "+count+" driver catalogue checks. Only temporary fixtures were changed.");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}finally{Directory.Delete(root,true);}}
}
