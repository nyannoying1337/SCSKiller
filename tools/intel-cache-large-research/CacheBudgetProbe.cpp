#include <windows.h>
#include <vector>
#include <cstring>
#include <iostream>
#include <cstdint>
static const unsigned char fragment[]={0x48,0x8b,0x8f,0xe0,0x0b,0x00,0x00,0x48,0x81,0xc1,0xb8,0x00,0x00,0x00,0x48,0x03,0xc1,0x48,0x3b,0x87,0x48,0x09,0x00,0x00};

int main(){
 // Lift the original add/compare instructions without executing any driver helpers.
 std::vector<unsigned char> code={0x57,0x48,0x8b,0xf9,0x48,0x8b,0xc2};
 code.insert(code.end(),std::begin(fragment),std::end(fragment));
 unsigned char suffix[]={0x0f,0x92,0xc0,0x0f,0xb6,0xc0,0x5f,0xc3};
 code.insert(code.end(),std::begin(suffix),std::end(suffix));
 void* mem=VirtualAlloc(nullptr,4096,MEM_COMMIT|MEM_RESERVE,PAGE_READWRITE);if(!mem)return 2;
 memcpy(mem,code.data(),code.size());DWORD previous;
 if(!VirtualProtect(mem,4096,PAGE_EXECUTE_READ,&previous)){VirtualFree(mem,0,MEM_RELEASE);return 2;}
 FlushInstructionCache(GetCurrentProcess(),mem,code.size());
 auto check=reinterpret_cast<int(*)(void*,uint64_t)>(mem);
 struct Sample{uint64_t budget,used,entry;int expected;const char* name;};
 Sample tests[]={{4ULL<<30,0,(4ULL<<30)-185,1,"below 4 GiB budget accepted"},{4ULL<<30,0,(4ULL<<30)-184,0,"exact 4 GiB boundary rejected"},{4ULL<<30,0,6ULL<<30,0,"6 GiB entry rejected by unchanged neighbor"},{8ULL<<30,0,6ULL<<30,1,"6 GiB comparison succeeds with 8 GiB neighbor"},{4ULL<<30,3ULL<<30,2ULL<<30,0,"aggregate above 4 GiB rejected"}};
 int failures=0;
 for(auto t:tests){std::vector<unsigned char> state(0xbe8,0);memcpy(state.data()+0x948,&t.budget,8);memcpy(state.data()+0xbe0,&t.used,8);int got=check(state.data(),t.entry);if(got!=t.expected)failures++;std::cout<<(got==t.expected?"PASS ":"FAIL ")<<t.name<<'\n';}
 VirtualFree(mem,0,MEM_RELEASE);return failures?1:0;
}
