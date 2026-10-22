#Requires -Version 5
<#
  Cierra el programa, saca el arranque automatico, borra la config y devuelve
  los cursores originales de Windows. No borra los archivos del proyecto.
#>

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe  = Join-Path $root 'bin\DildoCursor.exe'

if (Test-Path $exe) { & $exe --quit | Out-Null }

Get-Process -Name 'DildoCursor' -ErrorAction SilentlyContinue | ForEach-Object {
    if (-not $_.WaitForExit(3000)) { try { $_.Kill() } catch {} }
}

Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' `
                    -Name 'DildoCursor' -ErrorAction SilentlyContinue
Write-Host "Arranque con Windows: DESACTIVADO" -ForegroundColor Yellow

$cfg = Join-Path $env:APPDATA 'DildoCursor'
if (Test-Path $cfg) { Remove-Item $cfg -Recurse -Force -ErrorAction SilentlyContinue }

Add-Type -Name SPI -Namespace W32U -MemberDefinition '[DllImport("user32.dll")] public static extern bool SystemParametersInfo(int a,int b,System.IntPtr c,int d);'
[W32U.SPI]::SystemParametersInfo(0x57,0,[IntPtr]::Zero,0x02) | Out-Null

Write-Host "Listo. Cursores originales restaurados." -ForegroundColor Green
Write-Host "Si queres borrar todo, elimina la carpeta: $root" -ForegroundColor Gray
