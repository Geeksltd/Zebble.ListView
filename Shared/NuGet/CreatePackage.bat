@echo off
del -y pub.bat
del -y Zebble.*
Nuget.exe pack Nuget.nuspec

for /f "delims=" %%a in ('dir Zebble.* /b') do set "name=%%a"

publish %name%
