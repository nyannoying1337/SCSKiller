#include <windows.h>
#include <cstdint>
#include <cstring>
#include <iostream>
static const unsigned char originalAlloc[]={0x48,0x89,0x5c,0x24,0x08,0x48,0x89,0x74,0x24,0x10,0x57,0x48,0x83,0xec,0x30,0x48,0x8b,0xf1,0x48,0x8b,0xfa,0x48,0x8b,0xca,0xe8,0xd3,0x9a,0xf3,0x00,0x4c,0x8b,0xc7,0x33,0xd2,0x48,0x8b,0xc8,0x48,0x8b,0xd8,0xe8,0x33,0x8c,0xf7,0x00,0x48,0x89,0x1e,0x48,0x8b,0xc6,0x48,0x8b,0x74,0x24,0x48,0x48,0x8b,0x5c,0x24,0x40,0x48,0x83,0xc4,0x30,0x5f,0xc3};

static unsigned char sentinel[128];static uint64_t allocation,cleared;static unsigned allocations,clears;
void* fakeAllocate(uint64_t n){allocation=n;++allocations;return sentinel;}
void* fakeClear(void* p,int value,uint64_t n){if(p!=sentinel||value!=0)TerminateProcess(GetCurrentProcess(),11);cleared=n;++clears;return p;}
int main(){
 auto memory=(unsigned char*)VirtualAlloc(nullptr,4096,MEM_COMMIT|MEM_RESERVE,PAGE_READWRITE);if(!memory)return 2;
 memcpy(memory,originalAlloc,sizeof(originalAlloc));
 auto trampoline=[&](size_t where,void* target,size_t disp,size_t next){memory[where]=0x48;memory[where+1]=0xb8;memcpy(memory+where+2,&target,8);memory[where+10]=0xff;memory[where+11]=0xe0;int32_t d=int32_t(where)-int32_t(next);memcpy(memory+disp,&d,4);};
 trampoline(512,(void*)fakeAllocate,0x19,0x1d);trampoline(544,(void*)fakeClear,0x29,0x2d);
 DWORD old;if(!VirtualProtect(memory,4096,PAGE_EXECUTE_READ,&old)){VirtualFree(memory,0,MEM_RELEASE);return 2;}
 FlushInstructionCache(GetCurrentProcess(),memory,4096);
 auto alloc=reinterpret_cast<void*(*)(void*,uint64_t)>(memory);int failures=0;
 for(uint64_t gib:{1ULL,4ULL,8ULL,16ULL}){void* object=nullptr;allocations=clears=0;void* result=alloc(&object,gib<<30);bool pass=allocations==1&&clears==1&&allocation==(gib<<30)&&cleared==allocation&&object==sentinel&&result==&object;if(!pass)++failures;std::cout<<(pass?"PASS ":"FAIL ")<<"whole-file allocation="<<allocation<<" zero-fill="<<cleared<<" (simulated, no large allocation)\n";}
 VirtualFree(memory,0,MEM_RELEASE);return failures?1:0;
}
