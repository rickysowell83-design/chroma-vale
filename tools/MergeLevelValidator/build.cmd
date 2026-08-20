@echo off
cd /d %~dp0
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /codepage:65001 /target:exe /optimize+ /out:MergeLevelValidator.exe LevelModel.cs MiniJson.cs Program.cs Solver.cs
echo BUILD_EXIT=%ERRORLEVEL%
