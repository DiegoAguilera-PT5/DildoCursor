@echo off
rem ---------------------------------------------------------------------------
rem  BOTON DE PANICO: devuelve los cursores de Windows al instante.
rem  Sirve si mataste el proceso desde el Administrador de tareas, que saltea
rem  la limpieza que hace el programa al cerrar.
rem ---------------------------------------------------------------------------
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Add-Type -Name S -Namespace U -MemberDefinition '[DllImport(\"user32.dll\")] public static extern bool SystemParametersInfo(int a,int b,System.IntPtr c,int d);';" ^
  "if ([U.S]::SystemParametersInfo(0x57,0,[IntPtr]::Zero,0x02)) { 'Cursores restaurados.' } else { 'No se pudo. Cerra sesion y volve a entrar.' }"
ping -n 3 127.0.0.1 >nul
