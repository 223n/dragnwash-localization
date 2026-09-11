@echo off
powershell -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0installer\Installer.ps1"
if errorlevel 1 pause
