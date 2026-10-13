#include <windows.h>
#include <d3d12.h>
#include <dxgi1_6.h>
#include <d3dcompiler.h>
#include <iostream>
#include <string>
#include <psapi.h>
#include <wrl/client.h>
using Microsoft::WRL::ComPtr;
int wmain(){
 ComPtr<IDXGIFactory6> factory;HRESULT hr=CreateDXGIFactory2(0,IID_PPV_ARGS(&factory));if(FAILED(hr))return 2;
 ComPtr<IDXGIAdapter1> chosen;
 for(UINT i=0;;++i){ComPtr<IDXGIAdapter1>a;if(factory->EnumAdapterByGpuPreference(i,DXGI_GPU_PREFERENCE_HIGH_PERFORMANCE,IID_PPV_ARGS(&a))==DXGI_ERROR_NOT_FOUND)break;DXGI_ADAPTER_DESC1 d={};a->GetDesc1(&d);if(d.VendorId==0x8086&&wcsstr(d.Description,L"B580")){chosen=a;std::wcout<<L"Adapter: "<<d.Description<<L'\n';break;}}
 if(!chosen){std::cerr<<"B580 unavailable; no adapter fallback\n";return 3;}
 ComPtr<ID3D12Device> device;hr=D3D12CreateDevice(chosen.Get(),D3D_FEATURE_LEVEL_12_0,IID_PPV_ARGS(&device));
 std::cout<<"D3D12CreateDevice HRESULT=0x"<<std::hex<<(unsigned)hr<<'\n';if(FAILED(hr))return 4;
 const char* source="[numthreads(1,1,1)] void main(uint3 id : SV_DispatchThreadID) {}";
 ComPtr<ID3DBlob> shader,error;hr=D3DCompile(source,strlen(source),"ArcLargeProbe",nullptr,nullptr,"main","cs_5_0",0,0,&shader,&error);if(FAILED(hr))return 5;
 D3D12_ROOT_SIGNATURE_DESC desc={};ComPtr<ID3DBlob> rootData;hr=D3D12SerializeRootSignature(&desc,D3D_ROOT_SIGNATURE_VERSION_1,&rootData,&error);if(FAILED(hr))return 6;
 ComPtr<ID3D12RootSignature> root;hr=device->CreateRootSignature(0,rootData->GetBufferPointer(),rootData->GetBufferSize(),IID_PPV_ARGS(&root));if(FAILED(hr))return 7;
 D3D12_COMPUTE_PIPELINE_STATE_DESC pipeline={};pipeline.pRootSignature=root.Get();pipeline.CS={shader->GetBufferPointer(),shader->GetBufferSize()};ComPtr<ID3D12PipelineState> state;
 hr=device->CreateComputePipelineState(&pipeline,IID_PPV_ARGS(&state));std::cout<<"CreateComputePipelineState HRESULT=0x"<<std::hex<<(unsigned)hr<<'\n';if(FAILED(hr))return 8;
 HMODULE modules[4096];DWORD needed;if(!EnumProcessModules(GetCurrentProcess(),modules,sizeof(modules),&needed))return 9;bool privateLoaded=false;wchar_t own[4096];GetModuleFileNameW(nullptr,own,4096);std::wstring expected=std::wstring(own)+L".local\\igd12um64xe2.dll";
 for(DWORD i=0;i<needed/sizeof(HMODULE)&&i<4096;i++){wchar_t path[4096];if(GetModuleFileNameW(modules[i],path,4096)&&wcsstr(path,L"igd12um64xe2.dll")){std::wcout<<L"Loaded Intel module: "<<path<<L'\n';privateLoaded=_wcsicmp(path,expected.c_str())==0;}}
 std::cout<<(privateLoaded?"PASS private driver loaded; B580 device and pipeline created\n":"FAIL private driver was not observed\n");return privateLoaded?0:10;
}
