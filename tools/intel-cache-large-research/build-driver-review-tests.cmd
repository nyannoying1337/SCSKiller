@echo off
setlocal
pushd "%~dp0"
if "%~2"=="" exit /b 2
if not exist ..\intel-cache-cap-winui\Worker\bin\ArcCache.SafetyEngine.exe exit /b 2
if not exist bin mkdir bin
copy /y ..\intel-cache-cap-winui\Worker\bin\ArcCache.SafetyEngine.exe bin\ArcCache.SafetyEngine.exe >nul
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /platform:x64 /out:bin\DriverCatalogTests.exe /reference:bin\ArcCache.SafetyEngine.exe DriverCatalogTests.cs
if errorlevel 1 exit /b 1
bin\DriverCatalogTests.exe "%~1" "%~2"
if errorlevel 1 exit /b 1
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /platform:x64 /out:bin\Xe3ConstructorSamples.exe /reference:bin\ArcCache.SafetyEngine.exe /reference:System.Core.dll Xe3ConstructorSamples.cs
if errorlevel 1 exit /b 1
bin\Xe3ConstructorSamples.exe "%~2" bin
if errorlevel 1 exit /b 1
where cl >nul 2>nul
if errorlevel 1 call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat" >nul
cl /nologo /EHsc /O2 /Fe:bin\Xe3ConstructorProbe.exe /Fo:bin\ Xe3ConstructorProbe.cpp
if errorlevel 1 exit /b 1
bin\Xe3ConstructorProbe.exe "%~2" bin\xe3-constructor-only-2048.dll bin\xe3-constructor-only-4096.dll
exit /b %errorlevel%
