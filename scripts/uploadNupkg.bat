@echo off
chcp 65001 > nul
:: Upload all nupkg package
set orgcd=%cd%
setlocal enabledelayedexpansion
cd /d %~dp0/..
echo start upload nupkg packages
for /f "delims=" %%i in ('dir /s/b "*.nupkg"') do (
	echo %%~pi | findstr /c:"Release" > nul 
	if !errorlevel! equ 0 (
		echo Uploading %%~ni to Nuget Server
		dotnet nuget push %%i --api-key %GITHUB_PAT% --source "github"
	)
)
cd /d %orgcd%
pause
