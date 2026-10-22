#Requires -Version 5
<#
  Compila (si hace falta), activa el arranque con Windows y deja el programa corriendo.
#>

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe  = Join-Path $root 'bin\DildoCursor.exe'

& (Join-Path $root 'compilar.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
New-ItemProperty -Path $runKey -Name 'DildoCursor' -Value ('"' + $exe + '"') `
                 -PropertyType String -Force | Out-Null
Write-Host "Arranque con Windows: ACTIVADO" -ForegroundColor Green

Start-Process -FilePath $exe
Write-Host ""
Write-Host "Corriendo. Ctrl+Alt+P abre el panel de control." -ForegroundColor Green
Write-Host "Tambien tenes el icono en la bandeja del sistema (al lado del reloj)." -ForegroundColor Green
