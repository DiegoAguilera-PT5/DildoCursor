<#
  BOTON DE PANICO.

  Si por lo que sea te quedaste sin cursor visible (por ejemplo porque mataste el
  proceso desde el Administrador de tareas, que saltea la limpieza), corre esto y
  Windows recarga sus cursores originales al instante.

  Se puede correr incluso a ciegas: Win+R  ->  powershell -ExecutionPolicy Bypass -File "<ruta>\rescate-cursores.ps1"
#>

Add-Type -Name SPI -Namespace Rescate -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SystemParametersInfo(int a,int b,System.IntPtr c,int d);
'@

if ([Rescate.SPI]::SystemParametersInfo(0x57, 0, [IntPtr]::Zero, 0x02)) {
    Write-Host "Cursores restaurados." -ForegroundColor Green
} else {
    Write-Host "No se pudo. Cerra sesion y volve a entrar." -ForegroundColor Red
}
