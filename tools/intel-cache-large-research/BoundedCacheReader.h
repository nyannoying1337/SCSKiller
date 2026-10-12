#pragma once
#include <windows.h>
#include <algorithm>
#include <cstdint>
#include <limits>
#include <vector>
namespace LargeCacheResearch {
enum class Status { Complete, Cancelled, UnexpectedEof, IoError, ConsumerStopped, InvalidRequest };
struct Result { Status status; uint64_t transferred; DWORD error; };
// Consumer must finish using each buffer before returning. Retaining buffer pointers
// is unsupported: this is NOT a replacement for Intel's contiguous-buffer parser.
template<class Source, class Consumer, class Cancel>
Result ReadBounded(Source&& source,uint64_t start,uint64_t length,size_t bufferBytes,Consumer consume,Cancel cancelled){
 if(bufferBytes==0||bufferBytes>64U*1024U*1024U||length>(std::numeric_limits<uint64_t>::max)()-start)
  return {Status::InvalidRequest,0,ERROR_INVALID_PARAMETER};
 std::vector<unsigned char> buffer(bufferBytes);uint64_t done=0;
 while(done<length){
  if(cancelled())return {Status::Cancelled,done,ERROR_CANCELLED};
  DWORD requested=DWORD(std::min<uint64_t>(buffer.size(),length-done)),actual=0,error=0;
  if(!source(start+done,buffer.data(),requested,actual,error))return {Status::IoError,done,error};
  if(actual>requested)return {Status::IoError,done,ERROR_INVALID_DATA};
  if(actual==0)return {Status::UnexpectedEof,done,ERROR_HANDLE_EOF};
  uint64_t chunkPosition=start+done;done+=actual;
  if(!consume(chunkPosition,buffer.data(),actual))return {Status::ConsumerStopped,done,ERROR_CANCELLED};
 }
 return {Status::Complete,done,ERROR_SUCCESS};
}
// Borrowed FILE_FLAG_OVERLAPPED handle; waits for completion before buffer reuse.
// Cancellation is checked between chunks. It does not cancel another owner's I/O.
class WindowsSource {
 HANDLE file;HANDLE event;DWORD eventError;
public:
 explicit WindowsSource(HANDLE borrowed):file(borrowed),event(CreateEventW(nullptr,TRUE,FALSE,nullptr)),eventError(event?ERROR_SUCCESS:GetLastError()){}
 ~WindowsSource(){if(event)CloseHandle(event);}
 WindowsSource(const WindowsSource&)=delete;WindowsSource& operator=(const WindowsSource&)=delete;
 bool operator()(uint64_t offset,unsigned char* buffer,DWORD requested,DWORD&actual,DWORD&error)const {
  OVERLAPPED ov={};ov.Offset=DWORD(offset);ov.OffsetHigh=DWORD(offset>>32);
  if(!event){error=eventError;return false;}
  if(!ResetEvent(event)){error=GetLastError();return false;}
  ov.hEvent=event;
  BOOL ok=ReadFile(file,buffer,requested,&actual,&ov);
  if(!ok){error=GetLastError();if(error==ERROR_IO_PENDING){ok=GetOverlappedResult(file,&ov,&actual,TRUE);error=ok?ERROR_SUCCESS:GetLastError();}}
  if(ok)error=ERROR_SUCCESS;return ok!=FALSE;
 }
};
}
