# ─── Build, Obfuscate & Deploy ───
# Builds the mod, saves a clean copy to Decryption/, obfuscates, and deploys.

$ErrorActionPreference = "Stop"
$root = "D:\Bonelab Code mod"
$buildOut = Join-Path $root "bin\Debug\net6.0"
$obfOut = Join-Path $root "bin\Obfuscated"
$decrypt = Join-Path $root "Decryption"
$modsDir = "D:\SteamLibrary\steamapps\common\BONELAB\Mods"
$pluginDir = "D:\SteamLibrary\steamapps\common\BONELAB\Plugins"
$dllName = "BonelabUtilityMod.dll"

Set-Location $root

# ── Step 1: Build ──
Write-Host "`n=== Building ===" -ForegroundColor Cyan
dotnet build BonelabUtilityMod.csproj /p:AssemblyName=BonelabUtilityMod
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED" -ForegroundColor Red; exit 1 }

# ── Step 2: Save clean copy to Decryption/ ──
Write-Host "`n=== Saving clean copy to Decryption/ ===" -ForegroundColor Cyan
if (!(Test-Path $decrypt)) { New-Item -ItemType Directory -Path $decrypt | Out-Null }
$cleanDll = Join-Path $buildOut $dllName
$decryptDll = Join-Path $decrypt $dllName
Copy-Item $cleanDll $decryptDll -Force
Write-Host ("  Clean DLL saved: " + $decryptDll)

# ── Step 3: Obfuscate ──
Write-Host "`n=== Obfuscating ===" -ForegroundColor Cyan
if (Test-Path $obfOut) { Remove-Item $obfOut -Recurse -Force }
New-Item -ItemType Directory -Path $obfOut | Out-Null

$obfConfig = Join-Path $root "obfuscar.xml"
obfuscar.console $obfConfig
if ($LASTEXITCODE -ne 0) {
    Write-Host "OBFUSCATION FAILED - deploying clean build instead" -ForegroundColor Yellow
    Copy-Item $cleanDll (Join-Path $modsDir $dllName) -Force
    Write-Host ("  Deployed (clean): " + (Join-Path $modsDir $dllName))
    exit 0
}

# ── Step 4: Deploy obfuscated DLL ──
Write-Host "`n=== Deploying obfuscated build ===" -ForegroundColor Cyan
$obfDll = Join-Path $obfOut $dllName
$deployDll = Join-Path $modsDir $dllName
Copy-Item $obfDll $deployDll -Force
Write-Host ("  Deployed: " + $deployDll)

Write-Host "`n=== Done ===" -ForegroundColor Green
Write-Host ("  Clean (readable):      " + $decryptDll)
Write-Host ("  Obfuscated (deployed): " + $deployDll)
