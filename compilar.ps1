#Requires -Version 5
<#
  Compila DildoCursor usando el compilador de C# que ya viene con Windows
  (.NET Framework 4.x). No hace falta instalar nada.
#>

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src  = Join-Path $root 'src'
$bin  = Join-Path $root 'bin'
$exe  = Join-Path $bin 'DildoCursor.exe'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    Write-Host "No encontre csc.exe (.NET Framework 4.x). Instala .NET Framework 4.8." -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $bin)) { New-Item -ItemType Directory -Path $bin | Out-Null }

# Si ya esta corriendo, lo cerramos para poder sobreescribir el exe.
# La ventana es toolwindow, asi que CloseMainWindow() no sirve: usamos el --quit del propio exe.
$running = Get-Process -Name 'DildoCursor' -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Cerrando instancia en ejecucion..." -ForegroundColor Yellow
    if (Test-Path $exe) { & $exe --quit | Out-Null }
    $running | ForEach-Object {
        if (-not $_.WaitForExit(3000)) {
            try { $_.Kill() } catch {}
            # Matarlo a la fuerza saltea la restauracion: la hacemos nosotros.
            Add-Type -Name SPI -Namespace W32 -MemberDefinition '[DllImport("user32.dll")] public static extern bool SystemParametersInfo(int a,int b,System.IntPtr c,int d);' -ErrorAction SilentlyContinue
            [W32.SPI]::SystemParametersInfo(0x57,0,[IntPtr]::Zero,0x02) | Out-Null
        }
    }
}

$files = Get-ChildItem -Path $src -Filter *.cs | ForEach-Object { $_.FullName }

Write-Host "Compilando $($files.Count) archivos..." -ForegroundColor Cyan

$refs = @(
    '/r:System.dll'
    '/r:System.Core.dll'
    '/r:System.Drawing.dll'
    '/r:System.Windows.Forms.dll'
)

# La silueta viaja embebida dentro del exe, asi anda sola donde la copies.
$art = Join-Path $root 'Dildo-ejemplo.png'
$extra = @()
if (Test-Path $art) {
    $extra += "/resource:$art,DildoCursor.Dildo-ejemplo.png"
} else {
    Write-Host "AVISO: no encontre Dildo-ejemplo.png; se usara la forma de respaldo." -ForegroundColor Yellow
}

$cscArgs = @('/nologo', '/target:winexe', '/optimize+', '/unsafe+', '/platform:anycpu', "/out:$exe") +
           $refs + $extra + $files
& $csc $cscArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "La compilacion fallo." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Listo:  $exe" -ForegroundColor Green
Write-Host "Ejecutalo y presiona Ctrl+Alt+P para abrir el panel." -ForegroundColor Green
