@echo off
rem Floaticon 构建入口：调用同目录下的 build.ps1，参数原样透传。
rem 用法：
rem   build.cmd            Release 编译 -> release\
rem   build.cmd -Pack      编译 + NuGet 打包 -> artifacts\
rem   build.cmd -Clean -Pack
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
exit /b %ERRORLEVEL%
