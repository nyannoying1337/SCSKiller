#include <windows.h>
#include <fstream>
#include <iostream>
#include <vector>
#include <array>
#include <cstring>
#include <cstdint>

// Executes only the reviewed constructor in private RX memory, never loads a driver.
// Its external initialization helper is replaced by RET in every sample.
// Full-image hash/signature validation belongs to Engine; supply its verified outputs.
std::array<unsigned char,128> run(const char* path, uint64_t cap) {
    std::ifstream file(path,std::ios::binary);file.seekg(0x18BC50);
    std::vector<unsigned char> code(129,0xCC);file.read((char*)code.data(),96);
    if(!file||code[68]!=0xE8||code[95]!=0xC3)throw "Unexpected constructor boundaries";
    int32_t displacement=128-(68+5);memcpy(code.data()+69,&displacement,4);code[128]=0xC3;
    void* memory=VirtualAlloc(nullptr,4096,MEM_COMMIT|MEM_RESERVE,PAGE_READWRITE);
    if(!memory)throw "Allocation failed";memcpy(memory,code.data(),code.size());DWORD previous;
    if(!VirtualProtect(memory,4096,PAGE_EXECUTE_READ,&previous)){VirtualFree(memory,0,MEM_RELEASE);throw "RX protection failed";}
    FlushInstructionCache(GetCurrentProcess(),memory,code.size());
    std::array<unsigned char,128> state;state.fill(0xA5);
    auto constructor=reinterpret_cast<void*(*)(void*)>(memory);
    void* returned=constructor(state.data());VirtualFree(memory,0,MEM_RELEASE);
    uint64_t actual,neighbor;memcpy(&actual,state.data()+16,8);memcpy(&neighbor,state.data()+8,8);
    if(returned!=state.data()||actual!=cap||neighbor!=0x100000000ULL)throw "Constructor output mismatch";
    std::cout<<"PASS constructor stores "<<cap<<" bytes; neighbor=4294967296; return=this\n";
    return state;
}
int main(int argc,char** argv) {
    if(argc!=4){std::cerr<<"Supply original, 2 GiB, 4 GiB Engine-verified DLL copies\n";return 2;}
    try {
        auto original=run(argv[1],512ULL<<20);
        for(int i=2;i<4;i++) {
            auto patched=run(argv[i],(i==2?2048ULL:4096ULL)<<20);
            memcpy(patched.data()+16,original.data()+16,8);
            if(patched!=original)throw "Unrelated constructor state changed";
            std::cout<<"PASS all other initialized and untouched bytes preserved\n";
        }
        return 0;
    }catch(const char* message){std::cerr<<message<<"\n";return 1;}
}
