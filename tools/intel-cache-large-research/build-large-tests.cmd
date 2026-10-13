@echo off
setlocal
pushd "%~dp0"
where cl >nul 2>nul
if errorlevel 1 call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat" >nul
if not exist bin mkdir bin
ml64 /nologo /c /Fobin\LargeCacheRead.obj LargeCacheRead.asm
if errorlevel 1 exit /b 1
cl /nologo /EHsc /O2 /W4 /Fe:bin\LargeReadWrapperTests.exe /Fo:bin\ LargeReadWrapperTests.cpp
if errorlevel 1 exit /b 1
bin\LargeReadWrapperTests.exe
if errorlevel 1 exit /b 1
if "%~1"=="" exit /b 0
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /platform:x64 /out:bin\LargePatchTests.exe /reference:System.Core.dll /reference:System.Web.Extensions.dll LargePatchTests.cs ..\intel-cache-cap-gui\Engine.cs ..\intel-cache-cap-gui\LargeCachePatch.cs
if errorlevel 1 exit /b 1
bin\LargePatchTests.exe "%~1" bin
exit /b %errorlevel%
