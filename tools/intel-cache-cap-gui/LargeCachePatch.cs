using System;
using System.IO;
using System.Linq;
namespace ArcCap {
// Experimental only: exact image, appended read-loop + complete unwind table.
// Contiguous allocation/parser ownership is deliberately preserved; RAM cost remains.
public static class LargeCachePatch {
 public const string EightGiB="BF74F45935E6BEB8B958728F96D49E7384C99ACEE0B82247981A1FC820C7405C",SixteenGiB="D7B7D5092232165AA2B37A181FD0940C5167C2EEFD1D696FCAC83C1159CB3132";
 public static readonly byte[] Code=Engine.Hex("535657415441554883ec20488bd9488bf2498bf84533e44885ff742c488bc7492bc441b800000010493bc04c0f42c0488bcb4a8d1426e8000000004885c074084c03e04c3be772d4498bc44883c420415d415c5f5e5bc3");
 public static readonly byte[] Unwind=Engine.Hex("010b06000b3207d005c0037002600130");
 const int NewRva=0x2876000,TableOffset=104,OriginalTableSize=0x7e8c4;
 static int Align(int n,int a){return checked((n+a-1)/a*a);}
 static void Put(byte[] b,int p,int v){Array.Copy(BitConverter.GetBytes(v),0,b,p,4);}
 static void Need(bool yes,string message){if(!yes)throw new InvalidDataException(message);}
 public static byte[] Build(byte[] original,int mib){
  Need(mib==8192||mib==16384,"Experimental limits: 8 or 16 GiB only.");Need(original.Length==Engine.Size&&Engine.Hash(original)==Engine.Original,"Exact original driver required for large-cache research.");
  int nt=BitConverter.ToInt32(original,0x3c),optional=nt+24;
  Need(BitConverter.ToUInt16(original,nt+6)==7&&BitConverter.ToInt32(original,optional+56)==NewRva,"Qualified PE layout mismatch.");
  Need(original.Skip(0x348).Take(40).All(x=>x==0),"No qualified section-header space.");
  Need(original.Skip(0x17aa20).Take(5).SequenceEqual(Engine.Hex("e8cb160000")),"Qualified cache reload call mismatch.");
  int raw=Align(original.Length,512),virtualSize=TableOffset+OriginalTableSize+12,rawSize=Align(virtualSize,512);
  byte[] output=new byte[checked(raw+rawSize)];Array.Copy(original,output,original.Length);
  byte[] constructor=Engine.Hex("48b800000000000000004889410848894110893990909090");
  Array.Copy(BitConverter.GetBytes((ulong)mib*1048576),0,constructor,2,8);Array.Copy(constructor,0,output,Engine.Offset-4,24);
  Put(output,0x17aa21,NewRva-(0x17b620+5));
  output[nt+6]=8;Put(output,optional+56,Align(NewRva+virtualSize,4096));
  byte[] name=System.Text.Encoding.ASCII.GetBytes(".arccap");Array.Copy(name,0,output,0x348,name.Length);
  Put(output,0x350,virtualSize);Put(output,0x354,NewRva);Put(output,0x358,rawSize);Put(output,0x35c,raw);Put(output,0x36c,unchecked((int)0x60000020));
  byte[] code=(byte[])Code.Clone();int call=Array.IndexOf(code,(byte)0xe8);Need(call==54,"Unexpected wrapper call position.");Put(code,call+1,0x17ccf0-(NewRva+call+5));Array.Copy(code,0,output,raw,code.Length);
  Array.Copy(Unwind,0,output,raw+88,Unwind.Length);
  // Existing .pdata is copied verbatim; append one sorted RUNTIME_FUNCTION.
  Array.Copy(original,0x277a400,output,raw+TableOffset,OriginalTableSize);
  int tail=raw+TableOffset+OriginalTableSize;Put(output,tail,NewRva);Put(output,tail+4,NewRva+code.Length);Put(output,tail+8,NewRva+88);
  Put(output,optional+112+3*8,NewRva+TableOffset);Put(output,optional+112+3*8+4,OriginalTableSize+12);
  Put(output,optional+64,0);Put(output,optional+64,Checksum(output,optional+64));return output;
 }
 static int Checksum(byte[] b,int skip){ulong sum=0;for(int i=0;i<b.Length;i+=2){if(i==skip||i==skip+2)continue;sum+=(uint)b[i]|(uint)(i+1<b.Length?b[i+1]:0)<<8;sum=(sum&65535)+(sum>>16);}sum=(sum&65535)+(sum>>16);return unchecked((int)(sum+(uint)b.Length));}
 public static byte[] Original(byte[] sample){
  Need(sample.Length>Engine.Size,"Not a large-cache research image.");byte[] original=new byte[Engine.Size];Array.Copy(sample,original,original.Length);
  int nt=BitConverter.ToInt32(original,0x3c);Need(nt>0&&nt<1024,"Invalid PE header.");int optional=nt+24;
  Array.Copy(Engine.Hex("48c741100000002048b80000000001000000893948894108"),0,original,Engine.Offset-4,24);
  Array.Copy(Engine.Hex("e8cb160000"),0,original,0x17aa20,5);
  original[nt+6]=7;original[nt+7]=0;Put(original,optional+56,NewRva);Put(original,optional+64,0x287d864);
  Array.Clear(original,0x348,40);Put(original,optional+112+3*8,0x27d6000);Put(original,optional+112+3*8+4,OriginalTableSize);
  Need(Engine.Hash(original)==Engine.Original,"Large-cache image has unrelated or unsupported changes.");return original;
 }
 public static Inspection Inspect(byte[] sample){
  byte[] original=Original(sample);ulong cap=BitConverter.ToUInt64(sample,Engine.Offset-2);int mib=checked((int)(cap/1048576));
  Need(mib==8192||mib==16384,"Unsupported experimental value.");byte[] expected=Build(original,mib);
  Need(sample.SequenceEqual(expected),"Experimental payload, unwind table, headers or bytes differ.");
  string hash=Engine.Hash(sample),pin=mib==8192?EightGiB:SixteenGiB;Need(hash==pin,"Experimental output hash is not qualified.");
  return new Inspection{Hash=hash,MiB=mib,Version="32.0.101.9034",State="Experimental large cap + chunked reload; whole-file RAM allocation remains; gameplay unverified"};
 }
}
}
