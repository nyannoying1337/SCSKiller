@echo off
setlocal
cd /d "%~dp0"
if not exist bin mkdir bin
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /win32manifest:app.manifest /out:bin\IntelArcCacheCap.exe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll Engine.cs WindowsPlatform.cs Isolation.cs IsolationControl.cs MatchedSet.cs Safety\GameSafety.cs Program.cs
if errorlevel 1 exit /b 1
copy /y IntelArcCacheCap.exe.config bin\IntelArcCacheCap.exe.config >nul
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /platform:x64 /out:bin\ArcCap.Tests.exe /reference:System.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll Engine.cs WindowsPlatform.cs Isolation.cs IsolationControl.cs MatchedSet.cs Safety\GameSafety.cs Tests.cs
exit /b %errorlevel%
