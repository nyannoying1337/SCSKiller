using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Web.Script.Serialization;

namespace ArcCap {
public class Inspection {
    public string Hash {get;set;} public int MiB {get;set;}
    public string Version {get;set;} public string State {get;set;}
}
public class AclSnapshot {
    public string Sddl {get;set;} public string Owner {get;set;} public string Group {get;set;}
    public string Dacl {get;set;} public bool Protected {get;set;} public bool Present {get;set;}
}
public class Bundle {
    public int Schema {get;set;} public string Target {get;set;}
    public Inspection Before {get;set;} public Inspection After {get;set;}
    public AclSnapshot Security {get;set;} public string Status {get;set;}
    public string Warning {get;set;}
}
public static class Engine {
    public const string Original="1A552E471AE986A52D045C4112E1980FB7171AABEBFF93C027A4BBA10A3C6229";
    public const string OneGiB="74CC626FA3F16E4365152B2FD241A11641E3713B5A2F822662F3DD0E1825FCE3";
    public const string TwoGiB="596DEEA6EB0467C0E1FE7C92F8CB83049CE9BF38E82758C84FE6B2C2A31DED12";
    public const string FourGiB="9B43A3F28C4A2ED28BB70BC16845C03D8EC919811B955854D56DB1884C92F85C";
    public const int Size=42440832, Offset=0x18BC60, Rva=0x18C85C;
    static readonly byte[] Context=Hex("48895c2408574883ec2033ff48c741100000002048b80000000001000000893948894108");
    // Same 24-byte constructor region; no code cave, branch or ABI changes.
    // Store the original 4 GiB field first, then store the cap through RAX.
    // RAX is overwritten after the following helper call; ADD overwrites flags before it.
    static readonly byte[] WideTwo=Hex("48B800000000010000004889410848D1E848894110893990");
    static readonly byte[] WideFour=Hex("48B800000000010000004889410848894110893990909090");
    public static byte[] Hex(string text) {return Enumerable.Range(0,text.Length/2).Select(i=>Convert.ToByte(text.Substring(i*2,2),16)).ToArray();}
    public static string Hash(byte[] bytes) {using(var s=SHA256.Create()) return BitConverter.ToString(s.ComputeHash(bytes)).Replace("-","");}
    public static void CheckPath(string path) {
        string full=Path.GetFullPath(path);
        for(string p=full; !String.IsNullOrEmpty(p); p=Path.GetDirectoryName(p)) {
            if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0) throw new InvalidDataException("Links and junctions are blocked.");
        }
    }
    public static byte[] Read(string path) {CheckPath(path); if(new FileInfo(path).Length!=Size) throw new InvalidDataException("Unsupported DLL size."); return File.ReadAllBytes(path);}
    static void Require(bool condition,string error) {if(!condition) throw new InvalidDataException(error);}
    static uint U32(byte[] b,int p) {Require(p>=0&&p<=b.Length-4,"Truncated PE."); return BitConverter.ToUInt32(b,p);}
    static ushort U16(byte[] b,int p) {Require(p>=0&&p<=b.Length-2,"Truncated PE."); return BitConverter.ToUInt16(b,p);}
    static void PE(byte[] b) {
        Require(b[0]==77&&b[1]==90,"Not a PE image.");
        uint raw=U32(b,0x3c); Require(raw<(uint)b.Length-24,"Invalid PE header."); int p=(int)raw;
        Require(U32(b,p)==0x4550 && U16(b,p+4)==0x8664 && U16(b,p+24)==0x20b,"Expected AMD64 PE32+.");
        int table=p+24+U16(b,p+20), matches=0;
        for(int i=0;i<U16(b,p+6);i++) {
            int q=table+i*40; uint vs=U32(b,q+8),va=U32(b,q+12),rs=U32(b,q+16),fo=U32(b,q+20),flags=U32(b,q+36);
            if(va<=Rva&&(ulong)Rva<(ulong)va+Math.Min(vs,rs)) {
                Require((ulong)fo+Rva-va==Offset-4&&(flags&0x20000000)!=0,"Verified RVA does not map to executable instruction."); matches++;
            }
        }
        Require(matches==1,"Ambiguous executable section mapping.");
    }
    public static Inspection Inspect(byte[] bytes) {
        Require(bytes.Length==Size,"Unsupported DLL size."); PE(bytes);
        bool two=bytes.Skip(Offset-4).Take(24).SequenceEqual(WideTwo),four=bytes.Skip(Offset-4).Take(24).SequenceEqual(WideFour);
        uint value=U32(bytes,Offset);
        Require(two||four||(value>=128U*1048576&&value<=2047U*1048576&&value%1048576==0),"Unsupported cap encoding.");
        byte[] normalized=(byte[])bytes.Clone();
        if(two||four)Array.Copy(Context,12,normalized,Offset-4,24);
        else Array.Copy(BitConverter.GetBytes(512*1048576),0,normalized,Offset,4);
        Require(Hash(normalized)==Original,"Unknown driver version or unrelated changes. Patching is blocked.");
        Require(normalized.Skip(0x18BC50).Take(Context.Length).SequenceEqual(Context),"Verified instruction context mismatch.");
        string hash=Hash(bytes);
        Require(!two||hash==TwoGiB,"Known 2 GiB hash mismatch.");Require(!four||hash==FourGiB,"Known 4 GiB hash mismatch.");
        return new Inspection{Hash=hash,MiB=two?2048:four?4096:(int)(value/1048576),Version="32.0.101.9034",State=hash==Original?"Original":two||four?"Verified encoding; runtime cap effect untested":"Verified cap-only modification"};
    }
    public static byte[] Patch(byte[] bytes,int mib) {
        Inspect(bytes); Require((mib>=128&&mib<=2047)||mib==2048||mib==4096,"Choose 128–2047 MiB, or experimental 2048/4096 MiB.");
        byte[] result=(byte[])bytes.Clone();Array.Copy(Context,12,result,Offset-4,24);
        if(mib==2048||mib==4096)Array.Copy(mib==2048?WideTwo:WideFour,0,result,Offset-4,24);
        else Array.Copy(BitConverter.GetBytes(mib*1048576),0,result,Offset,4);Inspect(result);
        Require(mib!=1024||Hash(result)==OneGiB,"Known 1 GiB hash mismatch."); return result;
    }
    public static AclSnapshot Canonical(string sddl) {
        var r=new RawSecurityDescriptor(sddl); byte[] d=null;
        if(r.DiscretionaryAcl!=null) {d=new byte[r.DiscretionaryAcl.BinaryLength];r.DiscretionaryAcl.GetBinaryForm(d,0);}
        return new AclSnapshot{Sddl=sddl,Owner=r.Owner.Value,Group=r.Group.Value,Dacl=d==null?null:Convert.ToBase64String(d),
            Protected=(r.ControlFlags&ControlFlags.DiscretionaryAclProtected)!=0,Present=(r.ControlFlags&ControlFlags.DiscretionaryAclPresent)!=0};
    }
    public static AclSnapshot Security(string path) {
        var a=File.GetAccessControl(path,AccessControlSections.Owner|AccessControlSections.Group|AccessControlSections.Access);
        return Canonical(a.GetSecurityDescriptorSddlForm(AccessControlSections.Owner|AccessControlSections.Group|AccessControlSections.Access));
    }
    public static bool Equivalent(AclSnapshot a,AclSnapshot b) {
        return a.Owner==b.Owner&&a.Group==b.Group&&a.Dacl==b.Dacl&&a.Protected==b.Protected&&a.Present==b.Present;
    }
    public static void RestoreSecurity(string path,AclSnapshot snapshot) {
        var a=new FileSecurity(); a.SetSecurityDescriptorSddlForm(snapshot.Sddl,AccessControlSections.Owner|AccessControlSections.Group|AccessControlSections.Access); File.SetAccessControl(path,a);
    }
    public static void Save(string folder,Bundle b) {
        string file=Path.Combine(folder,"manifest.json"),tmp=Path.Combine(folder,"manifest.tmp");
        using(var stream=new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None)) {
            var bytes=System.Text.Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(b)); stream.Write(bytes,0,bytes.Length); stream.Flush(true);
        }
        if(File.Exists(file)) File.Replace(tmp,file,null); else File.Move(tmp,file);
    }
    public static Bundle Prepare(string source,string folder,int mib) {
        byte[] before=Read(source),after=Patch(before,mib); var acl=Security(source);
        Require(!Directory.Exists(folder),"Bundle already exists. Choose a fresh directory."); Directory.CreateDirectory(folder);
        var b=new Bundle{Schema=2,Target=Path.GetFullPath(source),Before=Inspect(before),After=Inspect(after),Security=acl,Status="prepared",
            Warning="Experimental default per-cache-file cap; profiles can override it. Original signature/catalog correspondence invalidated. Runtime effect unverified."};
        File.WriteAllBytes(Path.Combine(folder,"before.dll"),before);File.WriteAllBytes(Path.Combine(folder,"patched.dll"),after);Save(folder,b);
        Validate(folder);
        Require(Read(source).SequenceEqual(before)&&Equivalent(Security(source),acl),"Source changed during backup. Discard this bundle.");
        return b;
    }
    public static Bundle Validate(string folder) {
        CheckPath(folder);
        var b=new JavaScriptSerializer().Deserialize<Bundle>(File.ReadAllText(Path.Combine(folder,"manifest.json")));
        Require(b!=null&&b.Schema==2&&b.Before!=null&&b.After!=null&&b.Security!=null,"Unsupported or incomplete bundle.");
        byte[] before=Read(Path.Combine(folder,"before.dll")),after=Read(Path.Combine(folder,"patched.dll"));
        Require(Inspect(before).Hash==b.Before.Hash&&Inspect(before).MiB==b.Before.MiB&&Inspect(after).Hash==b.After.Hash&&Inspect(after).MiB==b.After.MiB,"Bundle hashes or metadata differ.");
        Require(Patch(before,b.After.MiB).SequenceEqual(after),"Unexpected bundle changes.");
        Require(Equivalent(Canonical(b.Security.Sddl),b.Security),"ACL snapshot is inconsistent."); return b;
    }
    public static string Replace(string folder,Func<string,AclSnapshot> security,Action<string,AclSnapshot> restore,bool rollback,Bundle approved=null) {
        var b=Validate(folder); string target=b.Target; CheckPath(target);
        if(approved!=null) Require(b.Target==approved.Target&&b.Before.Hash==approved.Before.Hash&&b.After.Hash==approved.After.Hash&&Equivalent(b.Security,approved.Security),"Bundle differs from the confirmed operation.");
        byte[] before=Read(Path.Combine(folder,"before.dll")),after=Read(Path.Combine(folder,"patched.dll"));
        byte[] expected=rollback?after:before,desired=rollback?before:after,current=Read(target);
        Require(Equivalent(security(target),b.Security),"Target ACL changed. Refusing replacement; use the official installer if recovery is needed.");
        if(current.SequenceEqual(desired)) return "Already matches; no change.";
        Require(current.SequenceEqual(expected),"Target changed since backup. Refusing to overwrite.");
        string stage=target+"."+Guid.NewGuid().ToString("N")+".tmp";bool replaced=false;
        try {
            b.Status=rollback?"rollback-started":"install-started";Save(folder,b);
            using(var s=new FileStream(stage,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {s.Write(desired,0,desired.Length);s.Flush(true);}
            restore(stage,b.Security);
            Require(Read(stage).SequenceEqual(desired)&&Equivalent(security(stage),b.Security),"Staged bytes or ACL failed verification.");
            Require(Read(target).SequenceEqual(expected)&&Equivalent(security(target),b.Security),"Target changed while staging.");
            File.Replace(stage,target,null);replaced=true;
            Require(Read(target).SequenceEqual(desired)&&Equivalent(security(target),b.Security),"Post-replacement verification failed; use this bundle for recovery.");
            b.Status=rollback?"rolled-back":"installed-reboot-required";Save(folder,b);return b.Status;
        } catch {
            b.Status=replaced?"recovery-required":"failed-target-unchanged";Save(folder,b);throw;
        } finally {if(File.Exists(stage)) File.Delete(stage);}
    }
}
}
