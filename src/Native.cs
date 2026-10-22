using System;
using System.Runtime.InteropServices;

namespace DildoCursor
{
    /// <summary>P/Invoke crudo. Todo lo que toca Win32 vive aca.</summary>
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE { public int cx; public int cy; }

        [StructLayout(LayoutKind.Sequential)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        public const int ULW_ALPHA = 0x02;
        public const byte AC_SRC_OVER = 0x00;
        public const byte AC_SRC_ALPHA = 0x01;

        public const int SPI_SETCURSORS = 0x0057;
        public const int SPIF_SENDCHANGE = 0x02;

        public const int OCR_NORMAL = 32512;
        public const int OCR_IBEAM = 32513;
        public const int OCR_WAIT = 32514;
        public const int OCR_CROSS = 32515;
        public const int OCR_UP = 32516;
        public const int OCR_SIZENWSE = 32642;
        public const int OCR_SIZENESW = 32643;
        public const int OCR_SIZEWE = 32644;
        public const int OCR_SIZENS = 32645;
        public const int OCR_SIZEALL = 32646;
        public const int OCR_NO = 32648;
        public const int OCR_HAND = 32649;
        public const int OCR_APPSTARTING = 32650;
        public const int OCR_HELP = 32651;

        public const int WM_HOTKEY = 0x0312;
        public const int MOD_ALT = 0x0001;
        public const int MOD_CONTROL = 0x0002;
        public const int MOD_NOREPEAT = 0x4000;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;

        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hDC);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr hObj);
        [DllImport("gdi32.dll")] public static extern bool GdiFlush();

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage,
            out IntPtr bits, IntPtr hSection, uint offset);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT pt);
        [DllImport("user32.dll")] public static extern bool GetCursorInfo(ref CURSORINFO pci);
        [DllImport("user32.dll")] public static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);
        [DllImport("user32.dll")] public static extern bool SetSystemCursor(IntPtr hcur, int id);

        [DllImport("user32.dll")]
        public static extern IntPtr CreateCursor(IntPtr hInst, int xHotSpot, int yHotSpot, int nWidth, int nHeight,
            byte[] pvANDPlane, byte[] pvXORPlane);

        [DllImport("user32.dll")]
        public static extern bool SystemParametersInfo(int uiAction, int uiParam, IntPtr pvParam, int fWinIni);

        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);

        public const int VK_LBUTTON = 0x01;
        public const int VK_RBUTTON = 0x02;

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    }
}
