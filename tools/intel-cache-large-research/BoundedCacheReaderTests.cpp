#include "BoundedCacheReader.h"
#include <winioctl.h>
#include <array>
#include <cstring>
#include <iostream>
#include <string>
using namespace LargeCacheResearch;
struct TemporarySparseFile {
 HANDLE handle=INVALID_HANDLE_VALUE;std::wstring path;
 TemporarySparseFile(){wchar_t folder[MAX_PATH],name[MAX_PATH];if(!GetTempPathW(MAX_PATH,folder)||!GetTempFileNameW(folder,L"ACR",0,name))throw "Temporary path failed";path=name;
  handle=CreateFileW(name,GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ|FILE_SHARE_DELETE,nullptr,OPEN_EXISTING,FILE_ATTRIBUTE_TEMPORARY|FILE_FLAG_DELETE_ON_CLOSE|FILE_FLAG_OVERLAPPED,nullptr);
  if(handle==INVALID_HANDLE_VALUE){DeleteFileW(name);throw "Temporary file failed";}}
 ~TemporarySparseFile(){if(handle!=INVALID_HANDLE_VALUE)CloseHandle(handle);}
};
int main(){
 try{
  int failed=0;auto check=[&](bool ok,const char*label){std::cout<<(ok?"PASS ":"FAIL ")<<label<<'\n';if(!ok)++failed;};
  TemporarySparseFile file;OVERLAPPED control={};control.hEvent=CreateEventW(nullptr,TRUE,FALSE,nullptr);if(!control.hEvent)throw "Event failed";
  DWORD bytes=0;BOOL sparse=DeviceIoControl(file.handle,FSCTL_SET_SPARSE,nullptr,0,nullptr,0,&bytes,&control);
  if(!sparse&&GetLastError()==ERROR_IO_PENDING)sparse=GetOverlappedResult(file.handle,&control,&bytes,TRUE);
  CloseHandle(control.hEvent);if(!sparse)throw "Sparse files unavailable; no large non-sparse fallback";
  const uint64_t length=(8ULL<<30)+4096;LARGE_INTEGER end;end.QuadPart=length;
  if(!SetFilePointerEx(file.handle,end,nullptr,FILE_BEGIN)||!SetEndOfFile(file.handle))throw "Sparse extension failed";
  const std::array<unsigned char,16> marker={1,19,3,41,5,61,7,89,9,101,11,127,13,149,15,173};
  const uint64_t positions[]={1,(4ULL<<30)-8,(8ULL<<30)+1};
  for(auto pos:positions){OVERLAPPED ov={};ov.Offset=DWORD(pos);ov.OffsetHigh=DWORD(pos>>32);ov.hEvent=CreateEventW(nullptr,TRUE,FALSE,nullptr);if(!ov.hEvent)throw "Event failed";
   DWORD written=0;BOOL ok=WriteFile(file.handle,marker.data(),DWORD(marker.size()),&written,&ov);
   if(!ok&&GetLastError()==ERROR_IO_PENDING)ok=GetOverlappedResult(file.handle,&ov,&written,TRUE);
   CloseHandle(ov.hEvent);if(!ok||written!=marker.size())throw "Marker write failed";}
  if(!FlushFileBuffers(file.handle))throw "Flush failed";
  DWORD high=0;SetLastError(ERROR_SUCCESS);DWORD low=GetCompressedFileSizeW(file.path.c_str(),&high);
  if(low==INVALID_FILE_SIZE&&GetLastError()!=ERROR_SUCCESS)throw "Physical size unavailable";
  uint64_t physical=(uint64_t(high)<<32)|low;
  check(physical<16ULL*1024*1024,"8 GiB sparse fixture occupies less than 16 MiB on disk");
  constexpr size_t chunk=1U<<20;std::vector<unsigned char> expected(chunk,0);uint64_t seen=0,chunks=0;bool valid=true;
  auto consumed=[&](uint64_t offset,const unsigned char*data,DWORD actual){
   valid&=offset==seen&&actual<=chunk;
   for(auto pos:positions)for(size_t i=0;i<marker.size();++i)if(pos+i>=offset&&pos+i<offset+actual)expected[size_t(pos+i-offset)]=marker[i];
   valid&=memcmp(data,expected.data(),actual)==0;
   for(auto pos:positions)for(size_t i=0;i<marker.size();++i)if(pos+i>=offset&&pos+i<offset+actual)expected[size_t(pos+i-offset)]=0;
   seen+=actual;++chunks;return valid;};
  auto result=ReadBounded(WindowsSource(file.handle),0,length,chunk,consumed,[]{return false;});
  check(result.status==Status::Complete&&result.transferred==length&&seen==length&&valid,"real 8 GiB + 4096 byte read verifies every byte with 1 MiB reader buffer");
  std::cout<<"Read bytes="<<seen<<" chunks="<<chunks<<" allocated-on-disk="<<physical<<'\n';
  std::array<unsigned char,16> captured={};
  result=ReadBounded(WindowsSource(file.handle),positions[1],marker.size(),7,[&](uint64_t offset,const unsigned char*data,DWORD n){memcpy(captured.data()+size_t(offset-positions[1]),data,n);return true;},[]{return false;});
  check(result.status==Status::Complete&&captured==marker,"small reads crossing 4 GiB preserve offsets and marker data");
  result=ReadBounded(WindowsSource(file.handle),0,length,chunk,[](uint64_t,const unsigned char*,DWORD){return true;},[]{return true;});
  check(result.status==Status::Cancelled&&result.transferred==0,"cancellation before first I/O");
  auto premature=[](uint64_t,unsigned char*,DWORD,DWORD&actual,DWORD&){actual=0;return true;};
  result=ReadBounded(premature,0,100,chunk,[](uint64_t,const unsigned char*,DWORD){return true;},[]{return false;});
  check(result.status==Status::UnexpectedEof&&result.transferred==0,"premature EOF is not reported as complete");
  auto error=[](uint64_t,unsigned char*,DWORD,DWORD&,DWORD&e){e=ERROR_ACCESS_DENIED;return false;};
  result=ReadBounded(error,0,100,chunk,[](uint64_t,const unsigned char*,DWORD){return true;},[]{return false;});
  check(result.status==Status::IoError&&result.error==ERROR_ACCESS_DENIED,"I/O error is preserved");
  unsigned requests=0;
  auto full=[&](uint64_t,unsigned char*buffer,DWORD n,DWORD&a,DWORD&){++requests;memset(buffer,0,n);a=n;return true;};
  result=ReadBounded(full,0,4096,1024,[](uint64_t,const unsigned char*,DWORD){return true;},[&]{return requests==1;});
  check(result.status==Status::Cancelled&&result.transferred==1024&&requests==1,"cancellation after a chunk does not read another");
  requests=0;result=ReadBounded(full,0,4096,1024,[](uint64_t,const unsigned char*,DWORD){return false;},[]{return false;});
  check(result.status==Status::ConsumerStopped&&result.transferred==1024&&requests==1,"consumer stop prevents further I/O");
  requests=0;result=ReadBounded(full,(std::numeric_limits<uint64_t>::max)()-1,100,1024,[](uint64_t,const unsigned char*,DWORD){return true;},[]{return false;});
  check(result.status==Status::InvalidRequest&&requests==0,"offset overflow is rejected before I/O");
  result=ReadBounded(full,0,100,0,[](uint64_t,const unsigned char*,DWORD){return true;},[]{return false;});
  check(result.status==Status::InvalidRequest&&requests==0,"zero buffer is rejected before I/O");
  auto partial=[](uint64_t,unsigned char*buffer,DWORD n,DWORD&a,DWORD&){a=(std::min)(n,DWORD(7));memset(buffer,0,a);return true;};
  result=ReadBounded(partial,0,100,32,[](uint64_t,const unsigned char*,DWORD){return true;},[]{return false;});
  check(result.status==Status::Complete&&result.transferred==100,"short successful transfers complete through additional reads");
  auto invalid=[](uint64_t,unsigned char*,DWORD n,DWORD&a,DWORD&){a=n+1;return true;};
  result=ReadBounded(invalid,0,100,32,[](uint64_t,const unsigned char*,DWORD){return true;},[]{return false;});
  check(result.status==Status::IoError&&result.error==ERROR_INVALID_DATA,"invalid returned byte count is rejected");
  CloseHandle(file.handle);file.handle=INVALID_HANDLE_VALUE;SetLastError(ERROR_SUCCESS);
  check(GetFileAttributesW(file.path.c_str())==INVALID_FILE_ATTRIBUTES&&GetLastError()==ERROR_FILE_NOT_FOUND,"temporary sparse file is removed after closing");
  return failed?1:0;
 }catch(const char*e){std::cerr<<e<<" error="<<GetLastError()<<'\n';return 2;}
}
