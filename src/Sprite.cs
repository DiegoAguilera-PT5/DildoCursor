using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

namespace DildoCursor
{
    /// <summary>
    /// La silueta del cursor, tomada de una imagen (Dildo-ejemplo.png) en vez de dibujarla
    /// a mano: asi la forma es exactamente la del ejemplo.
    ///
    /// Al cargarla la dejamos "canonica": una mascara de alpha rotada de modo que el eje
    /// largo del cuerpo quede horizontal, con la punta a la izquierda. De esa forma, para
    /// doblarla sobre la curva de la fisica alcanza con cortarla en tiras verticales y
    /// dibujar cada tira sobre el tramo que le toca.
    /// </summary>
    internal sealed class Sprite
    {
        public Bitmap Mask;          // canonica: eje horizontal, punta a la izquierda
        public int W, H;
        public float AxisY;          // altura del eje dentro de la mascara
        public float GlansHalf;      // medio alto del cuerpo cerca de la punta, en px canonicos
        public int SplitX;           // donde termina el cuerpo flexible y empieza la base
        public int TrunkTop;         // franja vertical que ocupa el cuerpo (sin la base)
        public int TrunkBottom;

        // Perfil del cuerpo a lo largo del eje, relativo al eje y en px canonicos. Sirve
        // para dibujar venas que queden siempre dentro de la silueta, sin recortes.
        public const int ProfN = 64;
        public float[] ProfTop = new float[ProfN];
        public float[] ProfBot = new float[ProfN];

        /// <summary>Borde de arriba y de abajo del cuerpo en t (0 = punta, 1 = base).</summary>
        public void ProfileAt(float t, out float top, out float bot)
        {
            float u = t * (ProfN - 1);
            if (u < 0f) u = 0f;
            int i = (int)u;
            if (i > ProfN - 2) i = ProfN - 2;
            float f = u - i;
            top = ProfTop[i] + (ProfTop[i + 1] - ProfTop[i]) * f;
            bot = ProfBot[i] + (ProfBot[i + 1] - ProfBot[i]) * f;
        }

        // Cache del reescalado: redimensionar una vez por tamaño sale mucho mas barato
        // que hacer que cada tira reescale en cada frame.
        /// <summary>Una copia escalada y ya teñida. Hay dos: el cuerpo y el contorno.</summary>
        sealed class Tinted
        {
            public Bitmap Bmp;
            public int W;
            public Color Color;
        }

        readonly Tinted _body = new Tinted();
        readonly Tinted _edge = new Tinted();

        const int EdgeContrast = 6;   // cuanto se endurece el borde de la silueta

        public static Sprite Load()
        {
            using (Bitmap src = OpenSource())
            {
                if (src == null) return null;
                return Build(src);
            }
        }

        static Bitmap OpenSource()
        {
            // 1) recurso embebido en el exe
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                foreach (string name in asm.GetManifestResourceNames())
                {
                    if (name.IndexOf("Dildo", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    using (Stream st = asm.GetManifestResourceStream(name))
                        if (st != null) return new Bitmap(st);
                }
            }
            catch { }

            // 2) al lado del exe o un nivel mas arriba (util mientras se desarrolla)
            try
            {
                string base_ = AppDomain.CurrentDomain.BaseDirectory;
                string[] tries =
                {
                    Path.Combine(base_, "Dildo-ejemplo.png"),
                    Path.Combine(base_, @"..\Dildo-ejemplo.png")
                };
                foreach (string p in tries)
                    if (File.Exists(p)) return new Bitmap(p);
            }
            catch { }

            return null;
        }

        // ------------------------------------------------------------------ construccion

        static Sprite Build(Bitmap src)
        {
            int w = src.Width, h = src.Height;
            byte[] alpha = ToAlpha(src, w, h);

            // Eje principal de la mancha (PCA sobre los pixeles opacos).
            double sum = 0, mx = 0, my = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double a = alpha[y * w + x] / 255.0;
                    if (a < 0.35) continue;
                    sum += a; mx += x * a; my += y * a;
                }
            if (sum < 1) return null;
            mx /= sum; my /= sum;

            double sxx = 0, sxy = 0, syy = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double a = alpha[y * w + x] / 255.0;
                    if (a < 0.35) continue;
                    double dx = x - mx, dy = y - my;
                    sxx += dx * dx * a; sxy += dx * dy * a; syy += dy * dy * a;
                }
            double theta = 0.5 * Math.Atan2(2 * sxy, sxx - syy);

            // De los dos extremos del eje, la punta es la que queda mas arriba a la izquierda.
            double ux = Math.Cos(theta), uy = Math.Sin(theta);
            double pMin = double.MaxValue, pMax = double.MinValue;
            double exMin = 0, eyMin = 0, exMax = 0, eyMax = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (alpha[y * w + x] < 90) continue;
                    double p = (x - mx) * ux + (y - my) * uy;
                    if (p < pMin) { pMin = p; exMin = x; eyMin = y; }
                    if (p > pMax) { pMax = p; exMax = x; eyMax = y; }
                }

            double angDeg = theta * 180.0 / Math.PI;
            if (exMax + eyMax < exMin + eyMin) angDeg += 180.0;   // la punta quedo a la derecha

            Bitmap canon = Rotate(src, alpha, w, h, (float)-angDeg);
            Bitmap cropped = CropToContent(canon);
            canon.Dispose();
            if (cropped == null) return null;

            var sp = new Sprite();
            sp.Mask = cropped;
            sp.W = cropped.Width;
            sp.H = cropped.Height;
            sp.Measure();
            return sp;
        }

        /// <summary>Negro sobre blanco (o PNG con alpha) -> mascara de opacidad.</summary>
        static byte[] ToAlpha(Bitmap src, int w, int h)
        {
            var outp = new byte[w * h];
            using (var tmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(tmp))
                {
                    g.Clear(Color.Transparent);

                    // Pixel a pixel. DrawImageUnscaled NO copia pixeles: respeta los DPI que
                    // declara el archivo, asi que una imagen a 72 ppp se dibuja un 33 % mas
                    // grande y se recorta contra el lienzo (se perdia justo la base, que
                    // queda abajo a la derecha).
                    g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel);
                }
                var data = tmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly,
                                        PixelFormat.Format32bppArgb);
                try
                {
                    unsafe
                    {
                        byte* p = (byte*)data.Scan0;
                        for (int y = 0; y < h; y++)
                        {
                            byte* row = p + y * data.Stride;
                            for (int x = 0; x < w; x++)
                            {
                                byte b = row[x * 4 + 0], gg = row[x * 4 + 1];
                                byte r = row[x * 4 + 2], a = row[x * 4 + 3];
                                int lum = (r * 77 + gg * 151 + b * 28) >> 8;
                                int v = a * (255 - lum) / 255;

                                // Umbral con pendiente: muchas siluetas vienen con el borde
                                // muy difuso, y tomado tal cual ese halo entra como parte de
                                // la figura: la engorda y rellena los detalles finos (las
                                // muescas de la base desaparecen). Esto devuelve un borde
                                // nitido dejando apenas un pixel de antialias.
                                int sharp = (v - 128) * EdgeContrast + 128;
                                outp[y * w + x] = (byte)(sharp < 0 ? 0 : (sharp > 255 ? 255 : sharp));
                            }
                        }
                    }
                }
                finally { tmp.UnlockBits(data); }
            }
            return outp;
        }

        static Bitmap Rotate(Bitmap src, byte[] alpha, int w, int h, float angDeg)
        {
            // La mascara en blanco con el alpha calculado; teñimos despues, al dibujar.
            using (var mask = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                var data = mask.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly,
                                         PixelFormat.Format32bppArgb);
                try
                {
                    unsafe
                    {
                        byte* p = (byte*)data.Scan0;
                        for (int y = 0; y < h; y++)
                        {
                            byte* row = p + y * data.Stride;
                            for (int x = 0; x < w; x++)
                            {
                                byte a = alpha[y * w + x];
                                row[x * 4 + 0] = 255; row[x * 4 + 1] = 255;
                                row[x * 4 + 2] = 255; row[x * 4 + 3] = a;
                            }
                        }
                    }
                }
                finally { mask.UnlockBits(data); }

                int d = (int)Math.Ceiling(Math.Sqrt((double)w * w + (double)h * h)) + 4;
                var dst = new Bitmap(d, d, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(dst))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.TranslateTransform(d / 2f, d / 2f);
                    g.RotateTransform(angDeg);
                    g.DrawImage(mask, -w / 2f, -h / 2f, w, h);
                }
                return dst;
            }
        }

        static Bitmap CropToContent(Bitmap b)
        {
            int w = b.Width, h = b.Height;
            int minX = w, minY = h, maxX = -1, maxY = -1;

            var data = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly,
                                  PixelFormat.Format32bppArgb);
            try
            {
                unsafe
                {
                    byte* p = (byte*)data.Scan0;
                    for (int y = 0; y < h; y++)
                    {
                        byte* row = p + y * data.Stride;
                        for (int x = 0; x < w; x++)
                            if (row[x * 4 + 3] > 10)
                            {
                                if (x < minX) minX = x;
                                if (x > maxX) maxX = x;
                                if (y < minY) minY = y;
                                if (y > maxY) maxY = y;
                            }
                    }
                }
            }
            finally { b.UnlockBits(data); }

            if (maxX < minX || maxY < minY) return null;
            var rect = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return b.Clone(rect, PixelFormat.Format32bppArgb);
        }

        /// <summary>Eje vertical del cuerpo y grosor en la zona del glande.</summary>
        void Measure()
        {
            var data = Mask.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly,
                                     PixelFormat.Format32bppArgb);
            try
            {
                unsafe
                {
                    byte* p = (byte*)data.Scan0;

                    // El eje lo tomamos del tronco (primer tercio), no del promedio general:
                    // la base es ancha y tironearia el eje hacia abajo.
                    double sum = 0, sy = 0;
                    int lim = Math.Max(1, W / 3);
                    for (int x = 0; x < lim; x++)
                        for (int y = 0; y < H; y++)
                        {
                            double a = p[y * data.Stride + x * 4 + 3] / 255.0;
                            if (a < 0.35) continue;
                            sum += a; sy += y * a;
                        }
                    AxisY = sum > 0 ? (float)(sy / sum) : H / 2f;

                    // alto del contenido en cada columna
                    var colTop = new int[W];
                    var colBot = new int[W];
                    var hgt = new int[W];
                    for (int x = 0; x < W; x++)
                    {
                        int t = -1, b = -1;
                        for (int y = 0; y < H; y++)
                            if (p[y * data.Stride + x * 4 + 3] > 90) { if (t < 0) t = y; b = y; }
                        colTop[x] = t; colBot[x] = b;
                        hgt[x] = (t >= 0 && b >= t) ? b - t + 1 : 0;
                    }

                    int gx = Math.Max(1, (int)(W * 0.06f));
                    GlansHalf = hgt[gx] > 0 ? hgt[gx] / 2f : H / 4f;

                    // Grosor tipico del tronco: mediana del tramo central, lejos del glande
                    // (mas gordo) y de la base (mucho mas gorda).
                    int a0 = (int)(W * 0.15f), a1 = (int)(W * 0.55f);
                    var sample = new int[Math.Max(1, a1 - a0)];
                    Array.Copy(hgt, a0, sample, 0, sample.Length);
                    Array.Sort(sample);
                    int trunkH = sample[sample.Length / 2];

                    // La base arranca donde la silueta se dispara de golpe respecto de ese
                    // grosor. Ahi cortamos: el cuerpo se dobla, la base va rigida.
                    SplitX = W;
                    if (trunkH > 0)
                        for (int x = (int)(W * 0.45f); x < W; x++)
                            if (hgt[x] > trunkH * 1.45f) { SplitX = x; break; }

                    TrunkTop = H; TrunkBottom = 0;
                    for (int x = 0; x < SplitX; x++)
                    {
                        if (colTop[x] < 0) continue;
                        if (colTop[x] < TrunkTop) TrunkTop = colTop[x];
                        if (colBot[x] > TrunkBottom) TrunkBottom = colBot[x];
                    }
                    if (TrunkBottom <= TrunkTop) { TrunkTop = 0; TrunkBottom = H - 1; }

                    for (int i = 0; i < ProfN; i++)
                    {
                        int x = (int)((long)i * (SplitX - 1) / (ProfN - 1));
                        if (x < 0) x = 0;
                        if (x >= W) x = W - 1;
                        if (colTop[x] < 0) { ProfTop[i] = -1f; ProfBot[i] = 1f; continue; }
                        ProfTop[i] = colTop[x] - AxisY;
                        ProfBot[i] = colBot[x] - AxisY;
                    }
                }
            }
            finally { Mask.UnlockBits(data); }
        }

        // ------------------------------------------------------------------ uso

        /// <summary>
        /// La silueta ya reescalada Y ya pintada del color pedido, cacheada.
        ///
        /// Lo de pintarla aca es la diferencia entre que ande y que no: aplicar el
        /// ColorMatrix en cada DrawImage obliga a GDI+ a recorrer los pixeles a mano en
        /// cada una de las ~40 tiras de cada frame. Teñida de antemano, las tiras son un
        /// blit comun y corriente.
        ///
        /// El ancho se redondea a un multiplo grande porque el largo del cuerpo cambia
        /// solo (al endurecerse, al latir) y si no habria que rehacer esto 60 veces por
        /// segundo. La resolucion de la copia solo afecta la nitidez, no el tamaño en
        /// pantalla, asi que el redondeo no se ve.
        /// </summary>
        public Bitmap Scaled(int widthPx, Color tint) { return Get(_body, widthPx, tint); }

        /// <summary>La misma silueta teñida del color del contorno (cache aparte).</summary>
        public Bitmap ScaledEdge(int widthPx, Color tint) { return Get(_edge, widthPx, tint); }

        Bitmap Get(Tinted slot, int widthPx, Color tint)
        {
            widthPx = ((widthPx + 63) / 64) * 64;
            if (widthPx < 64) widthPx = 64;
            if (widthPx > W) widthPx = W;

            if (slot.Bmp != null && slot.W == widthPx && slot.Color == tint) return slot.Bmp;

            if (slot.Bmp != null) slot.Bmp.Dispose();
            int hh = Math.Max(4, (int)Math.Round(widthPx * (double)H / W));
            var b = new Bitmap(widthPx, hh, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(b))
            using (var attrs = Tint(tint))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(Mask, new Rectangle(0, 0, widthPx, hh), 0, 0, W, H,
                            GraphicsUnit.Pixel, attrs);
            }
            slot.Bmp = b;
            slot.W = widthPx;
            slot.Color = tint;
            return slot.Bmp;
        }

        /// <summary>Pinta la mascara del color pedido conservando su alpha.</summary>
        public static ImageAttributes Tint(Color c)
        {
            var m = new ColorMatrix(new float[][]
            {
                new float[] {0f, 0f, 0f, 0f, 0f},
                new float[] {0f, 0f, 0f, 0f, 0f},
                new float[] {0f, 0f, 0f, 0f, 0f},
                new float[] {0f, 0f, 0f, 1f, 0f},
                new float[] {c.R / 255f, c.G / 255f, c.B / 255f, 0f, 1f}
            });
            var ia = new ImageAttributes();
            ia.SetColorMatrix(m, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            ia.SetWrapMode(WrapMode.TileFlipXY);   // evita el halo en los bordes de cada tira
            return ia;
        }
    }
}
