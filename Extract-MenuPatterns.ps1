
$dllPaths = @(
    'D:\SteamLibrary\steamapps\common\BONELAB\Mods\PowerTools.dll',
    'D:\SteamLibrary\steamapps\common\BONELAB\Mods\ToggleableGodMode.dll'
)

function Extract-ReadableStrings {
    param([string]$dllPath)
    
    $bytes = [System.IO.File]::ReadAllBytes($dllPath)
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    
    # Find BoneMenuCreator context
    $result = @()
    
    # Pattern 1: Look for method context around BoneMenuCreator
    if ($text -match '(BoneMenuCreator.*?(?:OnInitializeMelon|OnSceneWasLoaded))') {
        $result += "=== BONELIB MENU CREATION PATTERN ==="
        $result += ($matches[1] | Select-String -AllMatches -Pattern '([A-Za-z_][A-Za-z0-9_]*(?:<.*?>)?\.(?:Create|Add|Register|Toggle|Button|Category)\([^)]*\))' -AllMatches).Matches.Value
    }
    
    # Pattern 2: Look for class definitions and hierarchy
    $result += "`n=== EXTRACTED STRINGS (FILTERED) ==="
    $lines = $text -split '([^[:print:]])' | Where-Object { 
        $_ -match '(class|MelonMod|OnInitialize|MenuManager|BoneMenu|CreateCategory|CreatePage|AddButton|CreateToggle|CreateEnum|RegisterMenu|BoneLibMenu|Preferences|CreateBool|CreateFloat|CreateInt|CreateString)' 
    } | Select-Object -Unique
    
    $result += $lines
    
    return $result -join "`n"
}

foreach ($dllPath in $dllPaths) {
    Write-Host "`n$('='*80)" -ForegroundColor Cyan
    Write-Host "Extracting from: $(Split-Path $dllPath -Leaf)" -ForegroundColor Cyan
    Write-Host "$('='*80)`n" -ForegroundColor Cyan
    
    Extract-ReadableStrings $dllPath
}
