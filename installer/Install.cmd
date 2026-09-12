@echo off
rem Fallback launcher for machines where Windows SmartScreen or a security
rem policy stops the unsigned Install.exe. Opens the same installer window;
rem a console flashes for a moment, that is all.
start "" powershell -NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File "%~dp0installer\Installer.ps1"
