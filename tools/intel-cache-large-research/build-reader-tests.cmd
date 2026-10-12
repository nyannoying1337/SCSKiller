@echo off
setlocal
pushd "%~dp0"
where cl >nul 2>nul
if errorlevel 1 call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat" >nul
where cl >nul 2>nul
if errorlevel 1 exit /b 2
if not exist bin mkdir bin
cl /nologo /EHsc /O2 /W4 /Fe:bin\BoundedCacheReaderTests.exe /Fo:bin\ BoundedCacheReaderTests.cpp
if errorlevel 1 exit /b 1
bin\BoundedCacheReaderTests.exe
exit /b %errorlevel%
