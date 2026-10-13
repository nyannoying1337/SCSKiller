#include <windows.h>
#include <stdio.h>
int wmain(int argc,wchar_t**){bool expectFailure=argc>1;SetLastError(0);HMODULE m=LoadLibraryW(L"d3d12.dll");DWORD error=GetLastError();wchar_t got[MAX_PATH]={0};if(m)GetModuleFileNameW(m,got,MAX_PATH);wprintf(L"loaded=%d error=%lu module=%ls expectedFailure=%d\n",m?1:0,error,got,expectFailure?1:0);if(m)FreeLibrary(m);return expectFailure?(!m&&error==ERROR_DLL_INIT_FAILED?0:1):(m?0:1);}
