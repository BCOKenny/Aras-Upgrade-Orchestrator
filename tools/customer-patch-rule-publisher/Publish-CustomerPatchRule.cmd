@echo off
setlocal
powershell.exe -NoProfile -File "%~dp0Publish-CustomerPatchRule.ps1"
set "exitCode=%ERRORLEVEL%"
if not "%exitCode%"=="0" (
  echo.
  echo Customer-Patch rule publication did not run or did not complete.
  echo If this was blocked by your organization's PowerShell execution policy, contact IT.
  echo This launcher does not bypass execution policy.
  pause
)
exit /b %exitCode%
