# Quick DLL Inspector using .NET Reflection
$dllPaths = @(
    'D:\SteamLibrary\steamapps\common\BONELAB\Mods\PowerTools.dll',
    'D:\SteamLibrary\steamapps\common\BONELAB\Mods\ToggleableGodMode.dll'
)

Add-Type -AssemblyName System.Reflection

foreach ($dllPath in $dllPaths) {
    Write-Host "`n$('='*80)" -ForegroundColor Cyan
    Write-Host "Decompiling: $(Split-Path $dllPath -Leaf)" -ForegroundColor Cyan
    Write-Host "$('='*80)`n" -ForegroundColor Cyan
    
    try {
        $assembly = [System.Reflection.Assembly]::LoadFrom($dllPath)
        
        Write-Host "Assembly: $($assembly.GetName().Name)" -ForegroundColor Green
        Write-Host "Version: $($assembly.GetName().Version)`n" -ForegroundColor Green
        
        # Get all types
        $types = $assembly.GetTypes()
        
        Write-Host "=== ALL NAMESPACES ===" -ForegroundColor Yellow
        $types | Select-Object -ExpandProperty Namespace -Unique | Sort-Object | ForEach-Object {
            if ($_) { Write-Host "using $_;" }
        }
        
        Write-Host "`n=== TYPES CONTAINING 'MOD' ===" -ForegroundColor Yellow
        $types | Where-Object { $_.Name -like "*Mod*" -or $_.BaseType.Name -like "*MelonMod*" } | ForEach-Object {
            Write-Host "`nclass $($_.Name)" -ForegroundColor Green
            if ($_.BaseType) { Write-Host "    : $($_.BaseType.Name)" }
            $_.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static) | 
                Where-Object { -not $_.IsSpecialName } | 
                ForEach-Object {
                    $params = ($_.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
                    Write-Host "    public $($_.ReturnType.Name) $($_.Name)($params)"
                }
        }
        
        Write-Host "`n=== TYPES CONTAINING 'MENU' ===" -ForegroundColor Yellow
        $types | Where-Object { $_.Name -like "*Menu*" -or $_.GetMethods().Name -contains "*Menu*" } | ForEach-Object {
            Write-Host "`nclass $($_.Name)" -ForegroundColor Green
            $_.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance) | 
                Where-Object { -not $_.IsSpecialName -and $_.Name -like "*Menu*" } | 
                ForEach-Object {
                    $params = ($_.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
                    Write-Host "    public $($_.ReturnType.Name) $($_.Name)($params)"
                }
        }
        
        Write-Host "`n=== PUBLIC CLASSES ===" -ForegroundColor Yellow
        $types | Where-Object { $_.IsPublic } | ForEach-Object {
            Write-Host "`nclass $($_.Name)" -ForegroundColor Green
            if ($_.BaseType -and $_.BaseType.Name -ne "Object") { Write-Host "    : $($_.BaseType.Name)" }
            $_.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance) | 
                Where-Object { -not $_.IsSpecialName } | 
                Select-Object -First 5 | 
                ForEach-Object {
                    $params = ($_.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }) -join ", "
                    Write-Host "    public $($_.ReturnType.Name) $($_.Name)($params)"
                }
            if (($_.GetMethods([System.Reflection.BindingFlags]::Public).Count) -gt 5) {
                Write-Host "    ... and more"
            }
        }
        
    } catch {
        Write-Host "Error: $_" -ForegroundColor Red
    }
}
