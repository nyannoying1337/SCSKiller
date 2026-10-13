@echo off
setlocal
cd /d "%~dp0"
if not exist bin mkdir bin
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /platform:x64 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /out:bin\CacheMonitorTests.exe Program.cs ..\Worker\CacheMonitor.cs
if errorlevel 1 exit /b 1
bin\CacheMonitorTests.exe
if errorlevel 1 exit /b 1
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /platform:x64 /out:bin\SafetyTests.exe /reference:System.Core.dll SafetyTests.cs ..\..\intel-cache-cap-gui\Safety\GameSafety.cs
if errorlevel 1 exit /b 1
bin\SafetyTests.exe
