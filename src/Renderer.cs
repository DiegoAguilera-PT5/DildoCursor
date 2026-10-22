using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DildoCursor
{
    internal sealed class Particle
    {
        public float X, Y;        // posicion en coordenadas de pantalla
        public float VX, VY;      // velocidad px/s
        public float R;           // radio
        public float Life;        // segundos restantes
        public float MaxLife;
        public float Wob;         // fase de deformacion
    }

    /// <summary>Estado de un frame: lo arma Overlay, lo consume Renderer.</summary>
    internal sealed class SpriteState
    {
        public PointF[] Spine;            // eje del cuerpo (pantalla). [0] = punta, [n-1] = base
        public float Length;              // largo del eje, en px
        public Sprite Art;                // la silueta del ejemplo, ya canonizada
        public float Hardness;
        public float Throb;               // -1..1, el latido cuando esta duro
        public float DripCharge;          // 0..1, gota colgando
        public List<Particle> Parts = new List<Particle>();

        /// <summary>Medio grosor del cuerpo en la zona del glande (px). Lo usa el babeo.</summary>
        public float GlansHalf
        {
            get
            {
                if (Art == null || Art.W <= 0) return Length * 0.16f;
                return Art.GlansHalf * (Length / Art.W);
            }
        }
    }

    /// <summary>
    /// Dibuja la silueta del ejemplo doblada sobre la curva que devuelve la fisica.
    ///
    /// La mascara canonica (eje horizontal) se corta en tiras verticales y cada tira se
    /// dibuja sobre el tramo de curva que le toca, girada segun la normal de ese tramo.
    /// Con tiras finas el resultado es una goma que se dobla, no una imagen rotada rigida.
    /// </summary>
    internal static class Renderer
    {
        const int Strips = 64;
        const float SwellAmp = 0.085f;    // cuanto se hincha de ancho al latir


        /// <summary>Convierte el seno del latido en algo con forma de pulso: sube de golpe
        /// y afloja despacio, como un latido y no como una onda.</summary>
        static float Pulse(float throb)
        {
            float v = (throb + 1f) * 0.5f;          // 0..1
            return (float)Math.Pow(v, 1.9) * 2f - 0.5f;
        }

        // ------------------------------------------------------------------ geometria

        /// <summary>
        /// Largo del cuerpo. Al endurecerse se estira bastante, y el estiron sale **hacia
        /// la base**: la punta es el extremo libre y se compensa contra el hotspot, asi que
        /// crece para el otro lado y no se pierde la referencia de donde apunta el cursor.
        /// </summary>
        public static float BodyLength(float scale, float hardness)
        {
            return scale * (116f + 24f * hardness);
        }

        /// <summary>Angulo de reposo del cuerpo, de la punta hacia la base.</summary>
        public static float RestAngleDeg(float hardness) { return 54f - 10f * hardness; }

        /// <summary>De donde cuelga el babeo: por fuera del glande, abajo y a la izquierda.</summary>
        public static PointF DripOrigin(SpriteState s)
        {
            PointF tip = s.Spine[0];
            float gh = s.GlansHalf;

            float dx = s.Spine[1].X - tip.X, dy = s.Spine[1].Y - tip.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len > 0.001f) { dx /= len; dy /= len; } else { dx = 1f; dy = 0f; }

            // centro del glande, y de ahi afuera: abajo y a la izquierda
            return new PointF(tip.X + dx * gh - gh * 0.80f,
                              tip.Y + dy * gh + gh * 0.92f);
        }

        // ------------------------------------------------------------------ dibujo

        public static void Draw(Graphics g, Config cfg, SpriteState s)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            DrawParticles(g, cfg, s);

            if (s.Art != null) DrawWarped(g, cfg, s);
            else DrawFallback(g, cfg, s);

            if (cfg.DroolEnabled && s.DripCharge > 0.04f)
                DrawHangingDrip(g, cfg, s);
        }

        static void DrawWarped(Graphics g, Config cfg, SpriteState s)
        {
            Sprite art = s.Art;
            int wDst = (int)Math.Round(s.Length);
            if (wDst < 8) wDst = 8;

            // Doble resolucion de la que se ve, para que el filtrado no quede pobre.
            Bitmap bmp = art.Scaled(wDst * 2, cfg.Skin);

            // OJO: el tamaño en pantalla sale de las proporciones del sprite original, NO
            // del bitmap cacheado. Asi la resolucion de la copia se puede redondear sin que
            // el cuerpo cambie de grosor.
            float k = s.Length / art.W;                      // px de pantalla por px del sprite
            float sk = (float)bmp.Width / art.W;             // px del bitmap por px del sprite

            // El latido: se hincha y se deshincha a lo ancho. Va solo en el grosor y no en
            // el largo, porque el largo decide que copia escalada se usa y cambiarlo en
            // cada frame tiraria el cache.
            float swell = 1f + SwellAmp * Pulse(s.Throb) * Math.Min(1f, s.Hardness);
            float axis = art.AxisY * k * swell;

            // Solo se dobla el cuerpo. La base de un dildo es una pieza rigida, y ademas
            // en el dibujo es una barra ancha y diagonal: pasarla por el doblado la
            // desfigura. Va aparte, entera, girada segun como quedo el final del cuerpo.
            int splitPx = (int)Math.Round(art.SplitX * sk);
            if (splitPx <= 4 || splitPx > bmp.Width) splitPx = bmp.Width;

            float trunkTop = art.TrunkTop * k * swell - axis;   // franja del cuerpo, respecto del eje
            float trunkBot = (art.TrunkBottom + 1) * k * swell - axis;
            int srcTop = (int)Math.Floor(art.TrunkTop * sk);
            int srcBot = (int)Math.Ceiling((art.TrunkBottom + 1) * sk);
            if (srcBot > bmp.Height) srcBot = bmp.Height;
            if (srcTop < 0) srcTop = 0;

            PointF[] pts = Resample(s.Spine, Strips + 1);
            PointF[] nrm = Normals(pts);

            var old = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.Bilinear;

            // Contorno: la misma silueta teñida, dibujada corrida en varias direcciones por
            // detras. Sale mas barato que dilatar la mascara y no necesita margen extra en
            // el bitmap. Con pocas tiras alcanza: es un halo, no se le miran los detalles.
            if (cfg.Outline && cfg.OutlineWidth > 0.15f)
            {
                // A media resolucion y con pocas tiras: es un halo detras del cuerpo, no
                // se le miran los detalles, y cada pasada extra se paga en cada frame.
                Bitmap edge = art.ScaledEdge(wDst, cfg.OutlineColor);
                float r = cfg.OutlineWidth * Math.Max(0.4f, Math.Min(2.5f, s.Length / 118f));
                int halo = Math.Max(8, Strips / 4);
                float skEdge = (float)edge.Width / art.W;
                int splitE = (int)Math.Round(art.SplitX * skEdge);
                if (splitE <= 4 || splitE > edge.Width) splitE = edge.Width;
                int srcTopE = (int)Math.Floor(art.TrunkTop * skEdge);
                int srcBotE = (int)Math.Ceiling((art.TrunkBottom + 1) * skEdge);
                if (srcBotE > edge.Height) srcBotE = edge.Height;
                if (srcTopE < 0) srcTopE = 0;

                for (int d = 0; d < 4; d++)
                {
                    double ang = d * Math.PI / 2.0 + Math.PI / 4.0;
                    WarpPass(g, edge, pts, nrm, splitE, skEdge, k, axis, swell,
                             trunkTop, trunkBot, srcTopE, srcBotE, halo,
                             (float)Math.Cos(ang) * r * 1.3f, (float)Math.Sin(ang) * r * 1.3f);
                }
            }

            WarpPass(g, bmp, pts, nrm, splitPx, sk, k, axis, swell,
                     trunkTop, trunkBot, srcTop, srcBot, Strips, 0f, 0f);

            g.InterpolationMode = old;
        }


        /// <summary>
        /// Una pasada del doblado: la base entera y despues el cuerpo en tiras. El
        /// desplazamiento (dx, dy) es lo que permite reusar esto para el contorno.
        /// </summary>
        static void WarpPass(Graphics g, Bitmap bmp, PointF[] pts, PointF[] nrm,
                             int splitPx, float sk, float k, float axis, float swell,
                             float trunkTop, float trunkBot, int srcTop, int srcBot,
                             int strips, float dx, float dy)
        {
            var dst = new PointF[3];

            // --- base rigida (primero: el cuerpo le tapa despues el empalme) ---
            if (splitPx < bmp.Width)
            {
                int bx = Math.Max(0, splitPx - Math.Max(2, bmp.Width / Strips));
                float ub = (float)bx / bmp.Width * Strips;
                PointF bp = SampleAt(pts, ub), bn = SampleAt(nrm, ub);
                PointF bt = new PointF(bn.Y, -bn.X);             // tangente del cuerpo ahi
                float bw = bmp.Width - bx;

                float bwScr = bw / sk * k;                       // ancho en pantalla
                float hScr = bmp.Height / sk * k * swell;        // alto en pantalla
                dst[0] = new PointF(bp.X + bn.X * -axis + dx, bp.Y + bn.Y * -axis + dy);
                dst[1] = new PointF(dst[0].X + bt.X * bwScr, dst[0].Y + bt.Y * bwScr);
                dst[2] = new PointF(bp.X + bn.X * (hScr - axis) + dx,
                                    bp.Y + bn.Y * (hScr - axis) + dy);

                g.DrawImage(bmp, dst, new RectangleF(bx, 0, bw, bmp.Height), GraphicsUnit.Pixel);
            }

            // --- cuerpo flexible ---
            int bodyStrips = Math.Max(6, (int)((long)strips * splitPx / bmp.Width));
            for (int i = 0; i < bodyStrips; i++)
            {
                int sx0 = (int)((long)i * splitPx / bodyStrips);
                int sx1 = (int)((long)(i + 1) * splitPx / bodyStrips);
                if (sx1 <= sx0) continue;

                // DrawImage de 3 puntos dibuja un PARALELOGRAMO: su lado derecho sale
                // paralelo al izquierdo, asi que en una curva no encastra con la tira
                // siguiente y queda una cuña de aire. Cada tira se dibuja entonces un
                // poco mas larga, y la siguiente (que va encima) le tapa el sobrante.
                int extra = Math.Max(2, sx1 - sx0);
                int sxEnd = Math.Min(bmp.Width, sx1 + extra);

                float u0 = (float)sx0 / bmp.Width * Strips;
                float uEnd = (float)sxEnd / bmp.Width * Strips;

                PointF p0 = SampleAt(pts, u0), n0 = SampleAt(nrm, u0);
                PointF pe = SampleAt(pts, uEnd);

                // Los dos lados verticales van paralelos a la normal local: es lo que un
                // paralelogramo puede representar sin torcerse. Girar el lado derecho
                // hacia la normal de mas adelante desalinea la tira entera (el cuerpo es
                // muchas veces mas alto que ancha cada tira, y el error se amplifica ahi).
                dst[0] = new PointF(p0.X + n0.X * trunkTop + dx, p0.Y + n0.Y * trunkTop + dy);
                dst[1] = new PointF(pe.X + n0.X * trunkTop + dx, pe.Y + n0.Y * trunkTop + dy);
                dst[2] = new PointF(p0.X + n0.X * trunkBot + dx, p0.Y + n0.Y * trunkBot + dy);

                g.DrawImage(bmp, dst, new RectangleF(sx0, srcTop, sxEnd - sx0, srcBot - srcTop),
                            GraphicsUnit.Pixel);
            }
        }

        /// <summary>Por si la imagen no carga: una capsula, para no quedarnos sin cursor.</summary>
        static void DrawFallback(Graphics g, Config cfg, SpriteState s)
        {
            PointF[] pts = Resample(s.Spine, Strips + 1);
            float r = s.Length * 0.11f;
            using (var pen = new Pen(cfg.Skin, r * 2f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawCurve(pen, pts, 0.5f);
            }
        }

        // ------------------------------------------------------------------ babeo

        static void DrawHangingDrip(Graphics g, Config cfg, SpriteState s)
        {
            float c = Math.Min(1.2f, s.DripCharge);
            PointF o = DripOrigin(s);
            float R = s.GlansHalf;
            float w = R * (0.17f + 0.28f * c);
            float h = R * (0.22f + 0.90f * c);
            float x = o.X;
            float y = o.Y + R * (0.18f + 0.62f * c);

            using (var brush = new SolidBrush(cfg.DroolColor))
            {
                using (var p = new Pen(cfg.DroolColor, Math.Max(1f, w * 0.40f)))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawLine(p, x, o.Y - R * 0.22f, x, y + h * 0.3f);
                }
                using (var path = Teardrop(x, y, w, h))
                    g.FillPath(brush, path);
            }
        }

        static void DrawParticles(Graphics g, Config cfg, SpriteState s)
        {
            if (!cfg.DroolEnabled || s.Parts.Count == 0) return;

            foreach (var p in s.Parts)
            {
                float t = Math.Max(0f, Math.Min(1f, p.Life / p.MaxLife));
                float fade = t * t * (3f - 2f * t);            // smoothstep
                int a = (int)(cfg.DroolColor.A * fade);
                if (a < 6) continue;

                // Al caer se estira, y al estirarse se afina: el liquido no aparece de la
                // nada. Dividir por la raiz del estiron mantiene el area mas o menos igual.
                float stretch = 1f + Math.Min(1.5f, Math.Abs(p.VY) / 620f);
                float w = p.R / (float)Math.Sqrt(stretch) * (1f + 0.12f * (float)Math.Sin(p.Wob));

                using (var brush = new SolidBrush(Color.FromArgb(a, cfg.DroolColor)))
                using (var path = Teardrop(p.X, p.Y, w, p.R * stretch))
                    g.FillPath(brush, path);
            }
        }

        static GraphicsPath Teardrop(float x, float y, float w, float h)
        {
            var path = new GraphicsPath();
            path.AddBezier(x, y - h, x + w, y - h * 0.25f, x + w * 0.9f, y + h, x, y + h);
            path.AddBezier(x, y + h, x - w * 0.9f, y + h, x - w, y - h * 0.25f, x, y - h);
            path.CloseFigure();
            return path;
        }

        // ------------------------------------------------------------------ util

        /// <summary>Punto del array en un indice fraccionario; extrapola si se pasa del final.</summary>
        static PointF SampleAt(PointF[] a, float u)
        {
            if (u <= 0f) return a[0];
            int i = (int)u;
            if (i >= a.Length - 1) i = a.Length - 2;
            float f = u - i;
            return new PointF(a[i].X + (a[i + 1].X - a[i].X) * f,
                              a[i].Y + (a[i + 1].Y - a[i].Y) * f);
        }

        static PointF[] Normals(PointF[] p)
        {
            var n = new PointF[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                PointF a = p[Math.Max(0, i - 1)], b = p[Math.Min(p.Length - 1, i + 1)];
                float tx = b.X - a.X, ty = b.Y - a.Y;
                float len = (float)Math.Sqrt(tx * tx + ty * ty);
                if (len < 0.0001f) { tx = 1f; ty = 0f; len = 1f; }
                n[i] = new PointF(-ty / len, tx / len);
            }
            return n;
        }

        /// <summary>Catmull-Rom: pocos nodos de fisica -> muchos puntos de curva suave.</summary>
        static PointF[] Resample(PointF[] p, int m)
        {
            int n = p.Length;
            var o = new PointF[m];
            for (int k = 0; k < m; k++)
            {
                float u = (float)k / (m - 1) * (n - 1);
                int i = (int)u;
                if (i > n - 2) i = n - 2;
                float f = u - i;

                PointF p0 = p[Math.Max(0, i - 1)], p1 = p[i], p2 = p[i + 1], p3 = p[Math.Min(n - 1, i + 2)];
                float f2 = f * f, f3 = f2 * f;
                o[k] = new PointF(
                    0.5f * ((2f * p1.X) + (-p0.X + p2.X) * f +
                            (2f * p0.X - 5f * p1.X + 4f * p2.X - p3.X) * f2 +
                            (-p0.X + 3f * p1.X - 3f * p2.X + p3.X) * f3),
                    0.5f * ((2f * p1.Y) + (-p0.Y + p2.Y) * f +
                            (2f * p0.Y - 5f * p1.Y + 4f * p2.Y - p3.Y) * f2 +
                            (-p0.Y + 3f * p1.Y - 3f * p2.Y + p3.Y) * f3));
            }
            return o;
        }

        public static Color Mix(Color a, Color b, float t)
        {
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            return Color.FromArgb(a.A,
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }
    }
}
