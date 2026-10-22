# DildoCursor

Reemplaza el puntero de Windows por un dildo de goma: la silueta sale de una imagen
(`Dildo-ejemplo.png`), pintada de un solo color y doblada en tiempo real por una fisica
de goma. Babea segun se mueve el mouse, se pone duro sobre lo cliqueable y gira en
circulos cuando algo esta cargando.

Para cambiarle la forma, reemplaza `Dildo-ejemplo.png` por otra silueta y volve a
compilar: el programa la alinea, la mide y la dobla solo. Que la figura entre completa
y sin tocar los bordes del lienzo (se recorta al contenido), maciza, sobre fondo blanco
o transparente. Los DPI que declare el archivo no importan: se lee pixel a pixel.

## Instalacion

Abri PowerShell en esta carpeta y corre:

```powershell
powershell -ExecutionPolicy Bypass -File .\instalar.ps1
```

Eso compila, activa el arranque con Windows y lo deja corriendo.

No hace falta instalar nada mas: usa el compilador de C# que ya viene con Windows
(.NET Framework 4.x) y no pide permisos de administrador.

## Uso

| Accion | Como |
|---|---|
| Abrir / cerrar el panel | **Ctrl + Alt + P** |
| Abrir el panel (alternativa) | clic en el icono de la bandeja, al lado del reloj |
| Pausar sin cerrar | panel, o clic derecho en el icono de la bandeja |
| Cerrar | panel -> *Salir*, o `bin\DildoCursor.exe --quit` |

## Que hace el cursor

- **Se bambolea como goma de verdad.** No es una animacion grabada: el cuerpo es una
  cadena de nodos con resortes y gravedad. Lo que esta anclado es la **base**, asi que
  la que se sacude es la punta, como un dildo parado sobre su ventosa. Al mover el mouse
  la punta se queda atras por inercia, el cuerpo se dobla, y al frenar rebota hasta
  acomodarse. La caida en reposo tampoco esta dibujada: la pone la gravedad.
- **La punta igual apunta bien.** En paralelo corre un segundo cuerpo identico con el
  ancla clavada, que dice exactamente donde cae la punta en reposo. Restando ese offset,
  la punta del dibujo queda sobre el punto real del cursor cuando el mouse esta quieto
  (que es cuando hace falta apuntar) y libre de bambolearse cuando se mueve.
- **Normal**: cuelga caido y babea. Cuanto mas rapido moves el mouse, mas chorrea;
  quieto apenas gotea de vez en cuando. Las gotas caen con gravedad y se desvanecen.
- **Sobre un link o un boton** (donde Windows mostraria la manito): se pone duro de
  verdad. El resorte se vuelve ocho veces mas fuerte, se amortigua mucho mas y casi no
  se le permite doblarse, asi que deja de ser goma: ante el mismo zarandeo, blando
  desvia la punta 128 px y duro apenas 27 px. Ademas se engorda, late, y **se alarga
  hacia la base**: la punta es el extremo libre y se compensa contra el hotspot, asi que
  el estiron sale para el otro lado y no se pierde la referencia de a donde apunta.
- **Al hacer clic**: da una estocada corta hacia adelante, como si quisiera apretar el
  boton con la punta. El empuje entra por la fisica, asi que el cuerpo acompaña y
  rebota solo.
- **Mientras algo carga** (donde Windows mostraria el circulito): gira como helice,
  arrastrandose como un latigo, y el babeo sale despedido por la tangente. Al terminar
  completa la vuelta y se acomoda, no corta de golpe.
- **Escribiendo texto o redimensionando ventanas**: por defecto se aparta y deja el
  cursor nativo, porque con el dildo encima no se apunta bien. Es configurable.

## Panel de control (Ctrl+Alt+P)

- **Colores**: el del cursor y el del babeo, cada uno con selector completo.
- **Contorno**: borde alrededor del cursor para no perderlo en fondos parecidos, con
  color y grosor a eleccion. Se puede apagar: es lo mas caro que dibuja la app (se pinta
  la silueta cuatro veces corrida), asi que apagarlo baja el consumo a la mitad.
- **Tamaño** (40 % a 250 %) y **opacidad**.
- **Goma**: cuanto se bambolea. En 0 % queda rigido; al maximo, gelatinoso.
- **Babeo**: activar/desactivar y cantidad.
- **Reacciones**: ponerse duro sobre links, empujar al hacer clic, girar al cargar y
  velocidad de giro.
- **Sistema**: ocultar el cursor real, dejar el nativo para texto/redimensionar, pausar,
  arrancar con Windows.
- **Desinstalar**: saca el arranque automatico, restaura los cursores, borra la config y
  ofrece borrar tambien los archivos.

Todo se aplica en vivo y se guarda solo en `%APPDATA%\DildoCursor\config.ini`.

La ventana tiene scroll y se puede agrandar: los botones de accion quedan siempre
fijos abajo, asi que nunca se corta el contenido aunque Windows escale las fuentes.

## Si te quedas sin cursor

El programa restaura los cursores de Windows al cerrar, incluso si crashea o si cerras
sesion. Lo unico que saltea esa limpieza es matar el proceso a la fuerza desde el
Administrador de tareas. Si pasa:

```powershell
powershell -ExecutionPolicy Bypass -File .\rescate-cursores.ps1
```

Devuelve los cursores al instante. (Cerrar sesion y volver a entrar tambien funciona.)

## Desinstalar

```powershell
powershell -ExecutionPolicy Bypass -File .\desinstalar.ps1
```

O desde el panel, boton **Desinstalar**.

## Como esta hecho

Una ventana *layered* topmost y click-through sigue al puntero y dibuja el sprite con
GDI+ sobre un DIB de 32 bits con alpha por pixel (`UpdateLayeredWindow`). El cursor real
se esconde reemplazando los cursores del sistema por unos totalmente transparentes
(`SetSystemCursor`), y el estado (normal / manito / cargando) se detecta comparando el
handle que devuelve `GetCursorInfo` contra los handles de cada ranura del sistema.

La silueta se carga una vez: se convierte en mascara de opacidad, se le busca el eje
largo (PCA) y se la rota para dejarla horizontal con la punta a la izquierda. Ahi mismo
se mide donde termina el cuerpo y empieza la base, mirando en que columna la silueta se
dispara respecto del grosor tipico del tronco.

Despues, cada frame, el cuerpo se corta en tiras verticales y cada tira se dibuja sobre
el tramo de curva que le toca, mientras que **la base va entera y rigida**, girada segun
como quedo el final del cuerpo. Es lo correcto en los dos sentidos: la base de un dildo
no se dobla, y ademas suele ser una pieza ancha y diagonal que, pasada por el doblado,
se desfigura.

El color se aplica con un ColorMatrix sobre la mascara, de modo que la imagen original
puede ser de cualquier color. Eso si: la copia teñida se cachea. Aplicar el ColorMatrix
dentro de cada tira obliga a GDI+ a recorrer los pixeles a mano unas 40 veces por frame
y costaba 57 ms por cuadro; teñida de antemano, baja a 3,3 ms.

El contorno se hace pintando la silueta teñida cuatro veces corrida alrededor, por
detras del cuerpo. Es mas barato que dilatar la mascara y no necesita margen extra en el
bitmap, pero multiplica el trabajo por frame, asi que va a media resolucion y con pocas
tiras: es un halo, no se le miran los detalles.

| Archivo | Que hace |
|---|---|
| `src/Program.cs` | arranque, instancia unica, red de seguridad para restaurar cursores |
| `src/Overlay.cs` | ventana en capas, loop de animacion, hotkey global, bandeja |
| `src/RubberBody.cs` | la fisica: cadena de resortes con gravedad, inercia y limite de flexion (el limite tambien aplica al primer tramo contra el angulo de reposo; si no, el cuerpo gira entero sobre su base y por mas rigido que se lo ponga sigue pareciendo goma) |
| `src/Sprite.cs` | carga la imagen, la alinea y la deja lista para doblar |
| `src/Renderer.cs` | dobla la silueta sobre la curva de la fisica, y el babeo |
| `src/CursorHider.cs` | oculta los cursores reales y detecta cual pediria Windows |
| `src/PanelForm.cs` | panel de control |
| `src/Config.cs` | config en `%APPDATA%` |
| `src/Native.cs` | P/Invoke a Win32 |

### Notas

- Se declara *system DPI aware*: en un setup con dos monitores de escala distinta el
  sprite puede verse algo mas grande o chico en el secundario. Se compensa con el
  control de tamaño del panel.
- A 60 fps consume ~30 % de un nucleo moviendo el mouse sin parar, y ~7 % quieto:
  saltea el repintado cuando ni el puntero ni la goma se movieron.
- La superficie donde se pinta es de tamaño fijo a proposito. Ajustarla al contenido en
  cada frame se probo y salio peor: rehacer el DIB cuesta mas que limpiar y publicar
  algunos pixeles de mas.
- El config esta versionado. Al actualizar desde una version anterior, los colores
  guardados se descartan (estaban pensados para el dibujo viejo) y el resto se conserva.

## Pendiente / ideas

Las funciones extra del panel que quieras agregar entran en `PanelForm.Build()`, y los
estados nuevos del cursor en `Renderer.Draw()`.
