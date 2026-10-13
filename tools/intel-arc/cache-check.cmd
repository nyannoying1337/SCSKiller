@echo off
rem Double-click: fills the Intel shader cache past 512 MB under a throwaway exe name (about an hour), then checks whether
rem the first PSOs it made still come from the cache (cache-limit.ps1). The throwaway cache file is deleted at the end.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0cache-limit.ps1" -MaxGB 1 %*
pause
