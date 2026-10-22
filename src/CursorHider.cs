using System;
using System.Collections.Generic;

namespace DildoCursor
{
    internal enum CursorKind
    {
        Normal,     // flecha comun
        Hand,       // algo cliqueable -> se pone duro
        Wait,       // cargando -> gira en circulos
        Text,       // barra de texto
        Resize,     // flechas de redimensionar / mover
        Other,
        Hidden      // no hay cursor visible en pantalla
    }

    /// <summary>
    /// Reemplaza los cursores del sistema por cursores 100% transparentes (asi solo se ve
    /// nuestro dibujo) y despues identifica que forma de cursor pediria Windows en cada momento.
    ///
    /// El truco para identificar: aunque los cursores instalados sean todos invisibles, cada uno
    /// es un handle distinto. Con LoadCursor(IDC_*) recuperamos el handle que ocupa cada "ranura"
    /// del sistema y lo comparamos con el que devuelve GetCursorInfo.
    /// </summary>
    internal static class CursorHider
    {
        // Cursores que nos "apropiamos" (dibujamos nosotros el reemplazo).
        static readonly int[] Owned =
        {
            Native.OCR_NORMAL, Native.OCR_HAND, Native.OCR_WAIT, Native.OCR_APPSTARTING,
            Native.OCR_HELP, Native.OCR_NO, Native.OCR_CROSS, Native.OCR_UP
        };

        // Cursores que opcionalmente dejamos nativos: escribir texto y redimensionar sin el
        // cursor real es un suplicio.
        static readonly int[] TextAndResize =
        {
            Native.OCR_IBEAM, Native.OCR_SIZEALL, Native.OCR_SIZENS, Native.OCR_SIZEWE,
            Native.OCR_SIZENESW, Native.OCR_SIZENWSE
        };

        static bool _hidden;
        static bool _textAndResizeHidden;
        static readonly Dictionary<IntPtr, CursorKind> _byHandle = new Dictionary<IntPtr, CursorKind>();

        public static bool IsHidden { get { return _hidden; } }

        public static void Apply(bool hide, bool keepNativeForTextAndResize)
        {
            if (hide)
            {
                bool hideTextAndResize = !keepNativeForTextAndResize;
                if (_hidden && _textAndResizeHidden == hideTextAndResize) return;

                if (_hidden) Restore();

                foreach (int id in Owned) Blank(id);
                if (hideTextAndResize) foreach (int id in TextAndResize) Blank(id);

                _hidden = true;
                _textAndResizeHidden = hideTextAndResize;
            }
            else if (_hidden)
            {
                Restore();
            }
            RefreshHandleMap();
        }

        static void Blank(int ocrId)
        {
            // AND = 1 y XOR = 0 en cada pixel => pixel totalmente transparente.
            const int w = 32, h = 32;
            var and = new byte[w * h / 8];
            var xor = new byte[w * h / 8];
            for (int i = 0; i < and.Length; i++) { and[i] = 0xFF; xor[i] = 0x00; }

            IntPtr cur = Native.CreateCursor(IntPtr.Zero, 0, 0, w, h, and, xor);
            if (cur != IntPtr.Zero) Native.SetSystemCursor(cur, ocrId); // SetSystemCursor se queda con el handle
        }

        /// <summary>Devuelve los cursores originales del esquema del usuario.</summary>
        public static void Restore()
        {
            try { Native.SystemParametersInfo(Native.SPI_SETCURSORS, 0, IntPtr.Zero, Native.SPIF_SENDCHANGE); }
            catch { }
            _hidden = false;
            _textAndResizeHidden = false;
            _byHandle.Clear();
        }

        /// <summary>
        /// Relee que handle ocupa cada ranura de cursor. Hay que rellamarlo cada tanto porque
        /// cambiar el tema o el esquema de punteros reemplaza los handles.
        /// </summary>
        public static void RefreshHandleMap()
        {
            _byHandle.Clear();
            Map(Native.OCR_NORMAL, CursorKind.Normal);
            Map(Native.OCR_HAND, CursorKind.Hand);
            Map(Native.OCR_WAIT, CursorKind.Wait);
            Map(Native.OCR_APPSTARTING, CursorKind.Wait);
            Map(Native.OCR_IBEAM, CursorKind.Text);
            Map(Native.OCR_SIZEALL, CursorKind.Resize);
            Map(Native.OCR_SIZENS, CursorKind.Resize);
            Map(Native.OCR_SIZEWE, CursorKind.Resize);
            Map(Native.OCR_SIZENESW, CursorKind.Resize);
            Map(Native.OCR_SIZENWSE, CursorKind.Resize);
            Map(Native.OCR_NO, CursorKind.Other);
            Map(Native.OCR_HELP, CursorKind.Other);
            Map(Native.OCR_CROSS, CursorKind.Other);
            Map(Native.OCR_UP, CursorKind.Other);
        }

        static void Map(int ocrId, CursorKind kind)
        {
            IntPtr h = Native.LoadCursor(IntPtr.Zero, ocrId);
            if (h != IntPtr.Zero && !_byHandle.ContainsKey(h)) _byHandle[h] = kind;
        }

        /// <summary>Que forma de cursor esta mostrando Windows ahora mismo.</summary>
        public static CursorKind Current()
        {
            var ci = new Native.CURSORINFO();
            ci.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.CURSORINFO));
            if (!Native.GetCursorInfo(ref ci)) return CursorKind.Normal;
            if ((ci.flags & 0x1) == 0) return CursorKind.Hidden;  // CURSOR_SHOWING no esta puesto

            CursorKind k;
            if (_byHandle.TryGetValue(ci.hCursor, out k)) return k;

            // Handle desconocido: puede ser un cursor propio de la app de turno (p.ej. el de un
            // juego o un IDE). Lo tratamos como flecha normal.
            return CursorKind.Normal;
        }
    }
}
