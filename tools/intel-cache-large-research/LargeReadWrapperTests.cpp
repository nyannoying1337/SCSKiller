#include <windows.h>
#include <iostream>
#include <cstring>
#include <cstdint>
static unsigned calls;static uint64_t requestedTotal;static bool fail,shortOnce,raise;
uint64_t fakeRead(void*,void*,uint64_t n){++calls;requestedTotal+=n;if(raise&&calls==2)RaiseException(0xE0424242,0,0,nullptr);if(fail&&calls==2)return 0;if(shortOnce&&calls==1)return n/2;return n;}
static bool catches(void* memory){__try{auto f=(uint64_t(*)(void*,void*,uint64_t))memory;f(nullptr,(void*)0x1000,8ULL<<30);return false;}__except(GetExceptionCode()==0xE0424242?EXCEPTION_EXECUTE_HANDLER:EXCEPTION_CONTINUE_SEARCH){return true;}}
int main(){
 unsigned char bytes[]={0x53,0x56,0x57,0x41,0x54,0x41,0x55,0x48,0x83,0xec,0x20,0x48,0x8b,0xd9,0x48,0x8b,0xf2,0x49,0x8b,0xf8,0x45,0x33,0xe4,0x48,0x85,0xff,0x74,0x2c,0x48,0x8b,0xc7,0x49,0x2b,0xc4,0x41,0xb8,0x00,0x00,0x00,0x10,0x49,0x3b,0xc0,0x4c,0x0f,0x42,0xc0,0x48,0x8b,0xcb,0x4a,0x8d,0x14,0x26,0xe8,0x00,0x00,0x00,0x00,0x48,0x85,0xc0,0x74,0x08,0x4c,0x03,0xe0,0x4c,0x3b,0xe7,0x72,0xd4,0x49,0x8b,0xc4,0x48,0x83,0xc4,0x20,0x41,0x5d,0x41,0x5c,0x5f,0x5e,0x5b,0xc3};unsigned char unwind[]={0x01,0x0b,0x06,0x00,0x0b,0x32,0x07,0xd0,0x05,0xc0,0x03,0x70,0x02,0x60,0x01,0x30};
 auto mem=(unsigned char*)VirtualAlloc(nullptr,4096,MEM_COMMIT|MEM_RESERVE,PAGE_READWRITE);if(!mem)return 2;
 memcpy(mem,bytes,sizeof(bytes));memcpy(mem+88,unwind,sizeof(unwind));
 mem[512]=0x48;mem[513]=0xb8;void* target=(void*)fakeRead;memcpy(mem+514,&target,8);mem[522]=0xff;mem[523]=0xe0;
 int32_t displacement=512-59;memcpy(mem+55,&displacement,4);
 RUNTIME_FUNCTION entry={0,DWORD(sizeof(bytes)),88};if(!RtlAddFunctionTable(&entry,1,(DWORD64)mem))return 3;
 DWORD old;if(!VirtualProtect(mem,4096,PAGE_EXECUTE_READ,&old))return 4;FlushInstructionCache(GetCurrentProcess(),mem,4096);
 auto f=(uint64_t(*)(void*,void*,uint64_t))mem;int failures=0;
 auto check=[&](bool ok,const char*s){std::cout<<(ok?"PASS ":"FAIL ")<<s<<'\n';if(!ok)++failures;};
 for(uint64_t size:{0ULL,1ULL,4ULL<<30,(8ULL<<30)+4096,16ULL<<30}){calls=0;requestedTotal=0;auto actual=f(nullptr,(void*)0x1000,size);check(actual==size&&requestedTotal==size&&calls==(size+0xfffffff)/0x10000000,"wrapper chunks full 64-bit length correctly");}
 calls=0;fail=true;check(f(nullptr,(void*)0x1000,8ULL<<30)==(256ULL<<20)&&calls==2,"zero transfer returns partial count for original caller to reject");fail=false;
 calls=0;shortOnce=true;check(f(nullptr,(void*)0x1000,8ULL<<30)==(8ULL<<30),"partial success continues from actual transferred count");shortOnce=false;
 calls=0;raise=true;check(catches(mem),"Windows exception unwinds through MASM-generated frame metadata");raise=false;
 RtlDeleteFunctionTable(&entry);VirtualFree(mem,0,MEM_RELEASE);return failures?1:0;
}
