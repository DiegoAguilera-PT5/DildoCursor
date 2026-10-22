#Requires -Version 5
<#
  Compila la app y la mete adentro de un instalador de un solo archivo:
      dist\DildoCursor-Setup.exe

  El instalador es un programa propio (setup\Installer.cs) y no un empaquetador
  de terceros. Se probo con IExpress, el que trae Windows, pero falla sin dar
  ningun motivo y no hay forma de depurarlo; ademas asi el instalador queda como
  codigo del repo y se puede cambiar.

  Lo que hace el instalador:
    - instala en %LOCALAPPDATA%\DildoCursor, sin pedir permisos de administrador
    - deja el programa corriendo
    - opcionalmente arranca con Windows y crea acceso directo
    - aparece en "Aplicaciones instaladas" con su desinstalador
    - el mismo exe sirve de desinstalador (/uninstall) y acepta /silent
#>

$ErrorActionPreference = 'Stop'
$root  = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist  = Join-Path $root 'dist'
$exe   = Join-Path $root 'bin\DildoCursor.exe'
$setup = Join-Path $dist 'DildoCursor-Setup.exe'

& (Join-Path $root 'compilar.ps1')
if ($LASTEXITCODE -ne 0) { throw "La compilacion de la app fallo." }
if (-not (Test-Path $exe)) { throw "No aparecio $exe" }

if (-not (Test-Path $dist)) { New-Item -ItemType Directory -Path $dist -Force | Out-Null }

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }

$files = Get-ChildItem -Path (Join-Path $root 'setup') -Filter *.cs | ForEach-Object { $_.FullName }
Write-Host "Compilando el instalador..." -ForegroundColor Cyan

$args = @(
    '/nologo', '/target:winexe', '/optimize+', '/platform:anycpu'
    "/out:$setup"
    "/resource:$exe,Setup.DildoCursor.exe"
    '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll'
) + $files

& $csc $args
if ($LASTEXITCODE -ne 0) { throw "No se pudo compilar el instalador." }

$kb = [math]::Round((Get-Item $setup).Length / 1KB, 1)
Write-Host ""
Write-Host "Instalador listo:  $setup   ($kb KB)" -ForegroundColor Green
Write-Host "Es un solo archivo: copialo a cualquier Windows 10/11 y ejecutalo." -ForegroundColor Green
