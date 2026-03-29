@echo off
REM Decompile PowerTools.dll and ToggleableGodMode.dll using dnSpy command-line and export to C#
set DNSPY="D:\Bonelab Code mod\dnSpy\dnSpy.exe"
set OUT_DIR="d:\Bonelab Code mod\Decompiled"

if not exist %OUT_DIR% mkdir %OUT_DIR%

echo Decompiling PowerTools.dll...
%DNSPY% "D:\SteamLibrary\steamapps\common\BONELAB\Mods\PowerTools.dll" --select "PowerTools.Main" > %OUT_DIR%\PowerTools_Main.txt 2>&1

echo Decompiling ToggleableGodMode.dll...
%DNSPY% "D:\SteamLibrary\steamapps\common\BONELAB\Mods\ToggleableGodMode.dll" > %OUT_DIR%\ToggleableGodMode.txt 2>&1

echo Done! Check the Decompiled folder.
