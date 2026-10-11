@echo off
setlocal
cd /d "%~dp0"

echo [*] ayugram-telegram-alias - build (C#, no install needed)
echo [*] Looking for built-in C# compiler (csc.exe from .NET Framework)...

set "CSC="
for /f "delims=" %%d in ('dir /b /ad /on "%WINDIR%\Microsoft.NET\Framework64\v4*" 2^>nul') do (
  if exist "%WINDIR%\Microsoft.NET\Framework64\%%d\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework64\%%d\csc.exe"
)
if not defined CSC (
  for /f "delims=" %%d in ('dir /b /ad /on "%WINDIR%\Microsoft.NET\Framework\v4*" 2^>nul') do (
    if exist "%WINDIR%\Microsoft.NET\Framework\%%d\csc.exe" set "CSC=%WINDIR%\Microsoft.NET\Framework\%%d\csc.exe"
  )
)
if not defined CSC (
  echo.
  echo [ERROR] csc.exe not found. Your Windows has no .NET Framework 4.x?!
  echo Install free ".NET Framework 4.8" or "Microsoft .NET SDK" and run again.
  pause
  exit /b 1
)
echo [*] Compiler: %CSC%

set "ICONARG="
if exist "AyuGram.ico" set "ICONARG=/win32icon:AyuGram.ico"

echo [*] Compiling Program.cs ...
"%CSC%" /nologo /target:winexe /optimize+ /out:"ayugram-telegram-alias.exe" %ICONARG% "Program.cs"
if errorlevel 1 (
  echo.
  echo [ERROR] Compile failed, see errors above.
  pause
  exit /b 1
)

echo.
echo [*] Done: ayugram-telegram-alias.exe
echo [*] It runs with NO console window. Pin it to the taskbar directly.
pause
