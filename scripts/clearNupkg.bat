@echo off
chcp 65001 > nul
:: 清理所有Release下的nupkg包
set /p confirm=Clear all nupkg packages in Release? (y/N)
set orgcd = %cd%
setlocal enabledelayedexpansion
if "!confirm!" == "y" (
	echo Clear starting...
	cd /d %~dp0/..
	for /f "delims=" %%i in ('dir /s/b "*.nupkg"') do (
		echo %%~pi | findstr /c:"Release" > nul
		if !errorlevel! equ 0 (
			echo Clearing %%~ni
			del /q /f %%i
		)
	)
	echo Clear symbol starting...
	cd /d %~dp0/..
	for /f "delims=" %%j in ('dir /s/b "*.snupkg"') do (
		echo %%~pj | findstr /c:"Release" > nul
		if !errorlevel! equ 0 (
			echo Claering %%~nj
			del /q /f %%j
		)
	)
	echo Clear finished.
)
cd /d %orgcd%
pause
