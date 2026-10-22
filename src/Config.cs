using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;

namespace DildoCursor
{
    /// <summary>Config persistida en %APPDATA%\DildoCursor\config.ini (key=value, sin dependencias).</summary>
    internal sealed class Config
    {
        public static readonly string Dir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DildoCursor");
        public static readonly string FilePath = Path.Combine(Dir, "config.ini");

        // v2 = rediseño plano de una sola tinta. Un config viejo trae colores pensados para
        // el dibujo anterior (con glande aparte), asi que esos se descartan al migrar.
        const int CurrentVersion = 2;
        int _loadedVersion = 1;

        // ---- apariencia ----
        public Color Skin = Color.FromArgb(255, 38, 48, 94);     // azul marino del icono
        public Color DroolColor = Color.FromArgb(215, 150, 190, 235);
        public float Scale = 1.0f;          // 0.4 .. 2.5
        public float Alpha = 1.0f;          // 0.2 .. 1.0

        // ---- comportamiento ----
        public bool DroolEnabled = true;
        public float DroolAmount = 1.0f;    // 0 .. 2.5
        public bool HardOnClickable = true;
        public bool SpinOnLoading = true;
        public float SpinSpeed = 2.2f;      // vueltas por segundo
        public bool HideRealCursor = true;
        public bool KeepNativeForTextAndResize = true;
        public bool Paused = false;
        public float Wobble = 1.0f;         // 0 .. 2.5  (cuanto se bambolea la goma)
        public bool ClickPush = true;       // al clic, empuja con la punta
        public bool Outline = true;         // contorno, para no perderlo en fondos parecidos
        public Color OutlineColor = Color.White;
        public float OutlineWidth = 2.2f;   // px a escala 100 %

        public static Config Load()
        {
            var c = new Config();
            try
            {
                if (!File.Exists(FilePath)) return c;
                foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == (char)35 || line[0] == (char)59) continue;
                    int eq = line.IndexOf((char)61);
                    if (eq <= 0) continue;
                    c.Set(line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim());
                }

                if (c._loadedVersion < CurrentVersion)
                {
                    var d = new Config();
                    c.Skin = d.Skin;
                    c.DroolColor = d.DroolColor;
                }
            }
            catch { /* config corrupta: se usan los defaults */ }
            return c;
        }

        void Set(string k, string v)
        {
            switch (k)
            {
                case "version": int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out _loadedVersion); break;
                case "skin": Skin = ParseColor(v, Skin); break;
                case "drool_color": DroolColor = ParseColor(v, DroolColor); break;
                case "scale": Scale = ParseF(v, Scale); break;
                case "alpha": Alpha = ParseF(v, Alpha); break;
                case "drool": DroolEnabled = ParseB(v, DroolEnabled); break;
                case "drool_amount": DroolAmount = ParseF(v, DroolAmount); break;
                case "hard_on_clickable": HardOnClickable = ParseB(v, HardOnClickable); break;
                case "spin_on_loading": SpinOnLoading = ParseB(v, SpinOnLoading); break;
                case "spin_speed": SpinSpeed = ParseF(v, SpinSpeed); break;
                case "hide_real_cursor": HideRealCursor = ParseB(v, HideRealCursor); break;
                case "keep_native_text_resize": KeepNativeForTextAndResize = ParseB(v, KeepNativeForTextAndResize); break;
                case "paused": Paused = ParseB(v, Paused); break;
                case "wobble": Wobble = ParseF(v, Wobble); break;
                case "click_push": ClickPush = ParseB(v, ClickPush); break;
                case "outline": Outline = ParseB(v, Outline); break;
                case "outline_color": OutlineColor = ParseColor(v, OutlineColor); break;
                case "outline_width": OutlineWidth = ParseF(v, OutlineWidth); break;
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var sb = new StringBuilder();
                sb.AppendLine("# DildoCursor - configuracion");
                sb.AppendLine("version=" + CurrentVersion);
                sb.AppendLine("skin=" + Hex(Skin));
                sb.AppendLine("drool_color=" + Hex(DroolColor));
                sb.AppendLine("scale=" + F(Scale));
                sb.AppendLine("alpha=" + F(Alpha));
                sb.AppendLine("drool=" + DroolEnabled);
                sb.AppendLine("drool_amount=" + F(DroolAmount));
                sb.AppendLine("hard_on_clickable=" + HardOnClickable);
                sb.AppendLine("spin_on_loading=" + SpinOnLoading);
                sb.AppendLine("spin_speed=" + F(SpinSpeed));
                sb.AppendLine("hide_real_cursor=" + HideRealCursor);
                sb.AppendLine("keep_native_text_resize=" + KeepNativeForTextAndResize);
                sb.AppendLine("paused=" + Paused);
                sb.AppendLine("wobble=" + F(Wobble));
                sb.AppendLine("click_push=" + ClickPush);
                sb.AppendLine("outline=" + Outline);
                sb.AppendLine("outline_color=" + Hex(OutlineColor));
                sb.AppendLine("outline_width=" + F(OutlineWidth));
                    File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
                _loadedVersion = CurrentVersion;
            }
            catch { /* sin permisos de escritura: seguimos solo en memoria */ }
        }

        public void ResetToDefaults()
        {
            var d = new Config();
            Skin = d.Skin; DroolColor = d.DroolColor;
            Scale = d.Scale; Alpha = d.Alpha;
            DroolEnabled = d.DroolEnabled; DroolAmount = d.DroolAmount;
            HardOnClickable = d.HardOnClickable; SpinOnLoading = d.SpinOnLoading; SpinSpeed = d.SpinSpeed;
            HideRealCursor = d.HideRealCursor; KeepNativeForTextAndResize = d.KeepNativeForTextAndResize;
            Paused = false; Wobble = d.Wobble;
            ClickPush = d.ClickPush;
            Outline = d.Outline; OutlineColor = d.OutlineColor; OutlineWidth = d.OutlineWidth;
        }

        static string F(float f) { return f.ToString("0.###", CultureInfo.InvariantCulture); }

        static string Hex(Color c)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:X2}{1:X2}{2:X2}{3:X2}", c.A, c.R, c.G, c.B);
        }

        static float ParseF(string v, float def)
        {
            float f;
            return float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f) ? f : def;
        }

        static bool ParseB(string v, bool def)
        {
            bool b;
            return bool.TryParse(v, out b) ? b : def;
        }

        static Color ParseColor(string v, Color def)
        {
            try
            {
                v = v.TrimStart((char)35);
                if (v.Length == 6) v = "FF" + v;
                if (v.Length != 8) return def;
                uint u = uint.Parse(v, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return Color.FromArgb(unchecked((int)u));
            }
            catch { return def; }
        }
    }
}
