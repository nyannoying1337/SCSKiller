#include <algorithm>
#include <cstdint>
#include <iostream>
#include <limits>
#include <vector>
struct Result {bool complete;uint64_t transferred;};
// Research algorithm only. No driver loading, real I/O, allocation of a cache buffer,
// code injection or changes to the app's accepted patch catalogue.
template<class Read> Result readChunks(uint64_t length,uint64_t start,Read read){
 if(length>std::numeric_limits<uint64_t>::max()-start)return {false,0};
 const uint32_t maxChunk=256U*1024U*1024U;uint64_t done=0;
 while(done<length){
  uint32_t requested=uint32_t(std::min<uint64_t>(length-done,maxChunk)),actual=0;
  if(!read(start+done,done,requested,actual)||actual==0||actual>requested)return {false,done};
  done+=actual;
 }
 return {true,done};
}
int main(){
 int failed=0;auto check=[&](bool good,const char* name){std::cout<<(good?"PASS ":"FAIL ")<<name<<'\n';if(!good)++failed;};
 for(uint64_t gib:{4ULL,8ULL,16ULL}){
  uint64_t expected=0,calls=0;bool ordering=true;uint64_t start=(5ULL<<30)+77;
  auto r=readChunks(gib<<30,start,[&](uint64_t offset,uint64_t bufferOffset,uint32_t count,uint32_t&actual){ordering&=offset==start+expected&&bufferOffset==expected&&count<=256U*1024U*1024U;expected+=count;++calls;actual=count;return true;});
  check(r.complete&&r.transferred==(gib<<30)&&expected==r.transferred&&ordering&&calls==gib*4,"full simulated file read above 32-bit boundary; offsets preserved");
 }
 unsigned calls=0;auto shortRead=readChunks((4ULL<<30)+123,0,[&](uint64_t,uint64_t,uint32_t n,uint32_t&a){++calls;a=std::min(n,64U*1024U*1024U);return true;});
 check(shortRead.complete&&shortRead.transferred==(4ULL<<30)+123&&calls==65,"partial successful transfers advance and complete");
 calls=0;auto eof=readChunks(8ULL<<30,0,[&](uint64_t,uint64_t,uint32_t n,uint32_t&a){a=++calls==2?0:n;return true;});
 check(!eof.complete&&eof.transferred==(256ULL<<20)&&calls==2,"premature EOF stops without claiming success");
 calls=0;auto error=readChunks(8ULL<<30,0,[&](uint64_t,uint64_t,uint32_t n,uint32_t&a){a=n;return ++calls!=3;});
 check(!error.complete&&error.transferred==(512ULL<<20)&&calls==3,"I/O failure stops and reports completed bytes");
 calls=0;auto overflow=readChunks(4096,std::numeric_limits<uint64_t>::max()-1024,[&](uint64_t,uint64_t,uint32_t,uint32_t&){++calls;return true;});
 check(!overflow.complete&&overflow.transferred==0&&calls==0,"offset overflow rejected before I/O");
 auto invalid=readChunks(4096,0,[](uint64_t,uint64_t,uint32_t n,uint32_t&a){a=n+1;return true;});
 check(!invalid.complete&&invalid.transferred==0,"invalid reported transfer count rejected");
 return failed?1:0;
}
