#define main ExistingConstructorProbeMain
#include "CapConstructorProbe.cpp"
#undef main
int main(int argc,char**argv){
 if(argc!=4)return 2;
 try{
  auto original=run(argv[1],512ULL<<20);
  for(int i=2;i<4;i++){
   auto sample=run(argv[i],(i==2?8ULL:16ULL)<<30);
   memcpy(sample.data()+16,original.data()+16,8);
   if(sample!=original)throw "Unrelated constructor state changed";
   std::cout<<"PASS all unrelated state preserved; driver never loaded\n";
  }
  return 0;
 }catch(const char*e){std::cerr<<e<<'\n';return 1;}
}
