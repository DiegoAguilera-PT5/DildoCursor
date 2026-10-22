using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DildoCursor
{
    /// <summary>
    /// Panel de control (Ctrl+Alt+P). Todo se aplica en vivo y se guarda al instante.
    ///
    /// El contenido va en un FlowLayoutPanel con AutoScroll y los botones de accion en una
    /// barra fija abajo: asi nunca queda contenido cortado, aunque Windows escale las fuentes
    /// o la pantalla sea baja.
    /// </summary>
    internal sealed class PanelForm : Form
    {
        static readonly Color BG = Color.FromArgb(28, 28, 34);
        static readonly Color BG2 = Color.FromArgb(44, 44, 54);
        static readonly Color FG = Color.FromArgb(237, 237, 242);
        static readonly Color DIM = Color.FromArgb(162, 162, 176);
        static readonly Color ACC = Color.FromArgb(232, 106, 138);

        const int ROW = 404;   // ancho util de cada fila dentro del scroll

        readonly Config _cfg;
        readonly Overlay _overlay;

        FlowLayoutPanel _flow;
        Button _btnSkin, _btnDrool, _btnEdge;
        TrackBar _tbScale, _tbAlpha, _tbDrool, _tbSpin, _tbWobble, _tbEdge;
        Label _lbScale, _lbAlpha, _lbDrool, _lbSpin, _lbWobble, _lbEdge;
        CheckBox _cbDrool, _cbHard, _cbSpin, _cbHide, _cbNative, _cbPause, _cbStartup;
        CheckBox _cbClick, _cbEdge;
        bool _loading;

        public PanelForm(Config cfg, Overlay overlay)
        {
            _cfg = cfg;
            _overlay = overlay;

            Text = "DildoCursor  -  Panel de control";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = BG;
            ForeColor = FG;
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.Font;
            MinimumSize = new Size(440, 300);
            KeyPreview = true;

            Build();
            RefreshFromConfig();
            FitToContent();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        // ------------------------------------------------------------------ UI

        void Build()
        {
            // Durante la construccion nada debe escribir el config: si un control ajusta
            // su valor solo (p.ej. un TrackBar al fijarle el minimo) no queremos que eso
            // se guarde como si el usuario lo hubiera elegido.
            _loading = true;
            try { BuildInner(); }
            finally { _loading = false; }
        }

        void BuildInner()
        {
            // OJO: el Fill va primero en la coleccion para que el docking le de el
            // espacio que sobra despues de la barra de abajo.
            _flow = new FlowLayoutPanel();
            _flow.Dock = DockStyle.Fill;
            _flow.FlowDirection = FlowDirection.TopDown;
            _flow.WrapContents = false;
            _flow.AutoScroll = true;
            _flow.BackColor = BG;
            _flow.Padding = new Padding(18, 14, 10, 14);
            Controls.Add(_flow);

            Controls.Add(BuildActionBar());

            Add(Title("DildoCursor", 15f, ACC));
            Add(Caption("Ctrl+Alt+P abre y cierra este panel.  Esc para cerrar."));

            Section("Color");
            _btnSkin = ColorRow("Color del cursor", delegate { PickColor(ref _cfg.Skin, _btnSkin); });
            _btnDrool = ColorRow("Color del babeo", delegate { PickColor(ref _cfg.DroolColor, _btnDrool); });

            Section("Contorno");
            Add(Caption("Un borde alrededor, para no perder el cursor en fondos parecidos."));
            _cbEdge = Check("Dibujar contorno", delegate
            {
                _cfg.Outline = _cbEdge.Checked;
                _btnEdge.Enabled = _cbEdge.Checked;
                _tbEdge.Enabled = _cbEdge.Checked;
                Apply();
            });
            _btnEdge = ColorRow("Color del contorno", delegate { PickColor(ref _cfg.OutlineColor, _btnEdge); });
            _tbEdge = Slider(5, 60, out _lbEdge, delegate
            {
                _cfg.OutlineWidth = _tbEdge.Value / 10f;
                _lbEdge.Text = "Grosor del contorno:  " + (_tbEdge.Value / 10f).ToString("0.0") + " px";
                Apply();
            });

            Section("Tamaño");
            _tbScale = Slider(40, 250, out _lbScale, delegate
            {
                _cfg.Scale = _tbScale.Value / 100f;
                _lbScale.Text = "Tamaño:  " + _tbScale.Value + " %";
                Apply();
            });
            _tbAlpha = Slider(15, 100, out _lbAlpha, delegate
            {
                _cfg.Alpha = _tbAlpha.Value / 100f;
                _lbAlpha.Text = "Opacidad:  " + _tbAlpha.Value + " %";
                Apply();
            });

            Section("Goma");
            Add(Caption("Cuanto se bambolea al moverlo. En 0 queda rigido."));
            _tbWobble = Slider(0, 250, out _lbWobble, delegate
            {
                _cfg.Wobble = _tbWobble.Value / 100f;
                _lbWobble.Text = "Bamboleo:  " + _tbWobble.Value + " %";
                _lbEdge.Text = "Grosor del contorno:  " + (_tbEdge.Value / 10f).ToString("0.0") + " px";
                Apply();
            });

            Section("Babeo");
            _cbDrool = Check("Babear al mover el mouse", delegate
            {
                _cfg.DroolEnabled = _cbDrool.Checked;
                _tbDrool.Enabled = _cbDrool.Checked;
                Apply();
            });
            _tbDrool = Slider(0, 250, out _lbDrool, delegate
            {
                _cfg.DroolAmount = _tbDrool.Value / 100f;
                _lbDrool.Text = "Cantidad de babeo:  " + _tbDrool.Value + " %";
                Apply();
            });

            Section("Reacciones");
            _cbHard = Check("Ponerse duro sobre links y botones", delegate
            {
                _cfg.HardOnClickable = _cbHard.Checked;
                Apply();
            });
            _cbClick = Check("Al hacer clic, empujar con la punta", delegate
            {
                _cfg.ClickPush = _cbClick.Checked;
                Apply();
            });
            _cbSpin = Check("Girar en circulos mientras algo carga", delegate
            {
                _cfg.SpinOnLoading = _cbSpin.Checked;
                _tbSpin.Enabled = _cbSpin.Checked;
                Apply();
            });
            _tbSpin = Slider(20, 600, out _lbSpin, delegate
            {
                _cfg.SpinSpeed = _tbSpin.Value / 100f;
                _lbSpin.Text = "Velocidad de giro:  " + (_tbSpin.Value / 100f).ToString("0.0") + " vueltas/s";
                Apply();
            });

            Section("Sistema");
            _cbHide = Check("Ocultar el cursor real de Windows", delegate
            {
                _cfg.HideRealCursor = _cbHide.Checked;
                _cbNative.Enabled = _cbHide.Checked;
                Apply();
            });
            _cbNative = Check("Dejar el cursor nativo al escribir y redimensionar", delegate
            {
                _cfg.KeepNativeForTextAndResize = _cbNative.Checked;
                Apply();
            });
            _cbPause = Check("Pausar (vuelve el cursor normal sin cerrar)", delegate
            {
                _cfg.Paused = _cbPause.Checked;
                Apply();
            });
            _cbStartup = Check("Arrancar con Windows", delegate
            {
                if (_loading) return;
                if (_cbStartup.Checked) Startup.Enable(); else Startup.Disable();
                _cbStartup.Checked = Startup.IsEnabled();
            });
        }

        Panel BuildActionBar()
        {
            var bar = new Panel();
            bar.Dock = DockStyle.Bottom;
            bar.Height = 86;
            bar.BackColor = BG2;
            bar.Padding = new Padding(16, 12, 16, 12);

            var grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.ColumnCount = 2;
            grid.RowCount = 2;
            grid.BackColor = BG2;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            grid.Controls.Add(ActionButton("Restaurar valores", delegate
            {
                _cfg.ResetToDefaults();
                RefreshFromConfig();
                Apply();
            }), 0, 0);

            grid.Controls.Add(ActionButton("Cerrar panel", delegate { Close(); }), 1, 0);
            grid.Controls.Add(ActionButton("Salir del programa", delegate { _overlay.Quit(); }), 0, 1);

            var un = ActionButton("Desinstalar", delegate { Uninstall(); });
            un.BackColor = Color.FromArgb(104, 34, 46);
            grid.Controls.Add(un, 1, 1);

            bar.Controls.Add(grid);
            return bar;
        }

        Button ActionButton(string text, EventHandler onClick)
        {
            var b = new Button();
            b.Text = text;
            b.Dock = DockStyle.Fill;
            b.Margin = new Padding(3);
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Color.FromArgb(58, 58, 70);
            b.ForeColor = FG;
            b.FlatAppearance.BorderColor = Color.FromArgb(92, 92, 106);
            b.UseVisualStyleBackColor = false;
            b.Click += onClick;
            return b;
        }

        // ---- helpers de fila ----

        void Add(Control c) { _flow.Controls.Add(c); }

        Label Title(string text, float size, Color color)
        {
            var l = new Label();
            l.Text = text;
            l.Font = new Font("Segoe UI Semibold", size, FontStyle.Bold);
            l.ForeColor = color;
            l.AutoSize = true;
            l.Margin = new Padding(0, 0, 0, 2);
            return l;
        }

        Label Caption(string text)
        {
            var l = new Label();
            l.Text = text;
            l.ForeColor = DIM;
            l.AutoSize = false;
            l.Size = new Size(ROW, 18);
            l.Margin = new Padding(0, 0, 0, 1);
            return l;
        }

        void Section(string text)
        {
            var l = new Label();
            l.Text = text.ToUpperInvariant();
            l.Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);
            l.ForeColor = ACC;
            l.AutoSize = false;
            l.Size = new Size(ROW, 22);
            l.TextAlign = ContentAlignment.BottomLeft;
            l.Margin = new Padding(0, 10, 0, 4);
            _flow.Controls.Add(l);

            var line = new Panel();
            line.BackColor = Color.FromArgb(62, 62, 74);
            line.Size = new Size(ROW, 1);
            line.Margin = new Padding(0, 0, 0, 8);
            _flow.Controls.Add(line);
        }

        Button ColorRow(string caption, EventHandler onClick)
        {
            _flow.Controls.Add(Caption(caption));

            var b = new Button();
            b.Size = new Size(ROW, 28);
            b.Margin = new Padding(0, 0, 0, 8);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Color.FromArgb(92, 92, 106);
            b.Text = "";
            b.Click += onClick;
            _flow.Controls.Add(b);
            return b;
        }

        TrackBar Slider(int min, int max, out Label label, EventHandler onChange)
        {
            label = Caption("");
            _flow.Controls.Add(label);

            var tb = new TrackBar();
            tb.Size = new Size(ROW, 30);
            tb.Margin = new Padding(-6, 0, 0, 2);
            tb.Minimum = min;
            tb.Maximum = max;
            tb.TickStyle = TickStyle.None;
            tb.BackColor = BG;
            tb.Scroll += onChange;
            tb.ValueChanged += onChange;
            _flow.Controls.Add(tb);
            return tb;
        }

        CheckBox Check(string text, EventHandler onChange)
        {
            var cb = new CheckBox();
            cb.Text = text;
            cb.ForeColor = FG;
            cb.AutoSize = false;
            cb.Size = new Size(ROW, 24);
            cb.Margin = new Padding(0, 0, 0, 2);
            cb.CheckedChanged += onChange;
            _flow.Controls.Add(cb);
            return cb;
        }

        /// <summary>Alto justo para el contenido, sin pasarse de la pantalla.</summary>
        void FitToContent()
        {
            _flow.PerformLayout();
            int content = 0;
            foreach (Control c in _flow.Controls)
                if (c.Bottom > content) content = c.Bottom;
            content += _flow.Padding.Bottom;

            var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            int chrome = Height - ClientSize.Height;
            int wanted = content + 86 + chrome;                 // + barra de acciones

            // Ni mas alto que la pantalla ni una ventana interminable: lo que sobre se scrollea.
            int cap = Math.Min(wa.Height - 80, 720);
            ClientSize = new Size(448, Math.Min(wanted, cap) - chrome);
        }

        // ------------------------------------------------------------------ logica

        public void RefreshFromConfig()
        {
            _loading = true;
            try
            {
                _btnSkin.BackColor = Opaque(_cfg.Skin);
                _btnDrool.BackColor = Opaque(_cfg.DroolColor);
                _btnEdge.BackColor = Opaque(_cfg.OutlineColor);

                _tbScale.Value = Clamp((int)(_cfg.Scale * 100f), _tbScale);
                _tbAlpha.Value = Clamp((int)(_cfg.Alpha * 100f), _tbAlpha);
                _tbDrool.Value = Clamp((int)(_cfg.DroolAmount * 100f), _tbDrool);
                _tbSpin.Value = Clamp((int)(_cfg.SpinSpeed * 100f), _tbSpin);
                _tbWobble.Value = Clamp((int)(_cfg.Wobble * 100f), _tbWobble);
                _tbEdge.Value = Clamp((int)(_cfg.OutlineWidth * 10f), _tbEdge);

                _lbScale.Text = "Tamaño:  " + _tbScale.Value + " %";
                _lbAlpha.Text = "Opacidad:  " + _tbAlpha.Value + " %";
                _lbDrool.Text = "Cantidad de babeo:  " + _tbDrool.Value + " %";
                _lbSpin.Text = "Velocidad de giro:  " + (_tbSpin.Value / 100f).ToString("0.0") + " vueltas/s";
                _lbWobble.Text = "Bamboleo:  " + _tbWobble.Value + " %";
                _lbEdge.Text = "Grosor del contorno:  " + (_tbEdge.Value / 10f).ToString("0.0") + " px";

                _cbDrool.Checked = _cfg.DroolEnabled;
                _cbHard.Checked = _cfg.HardOnClickable;
                _cbClick.Checked = _cfg.ClickPush;
                _cbEdge.Checked = _cfg.Outline;
                _btnEdge.Enabled = _cfg.Outline;
                _tbEdge.Enabled = _cfg.Outline;
                _cbSpin.Checked = _cfg.SpinOnLoading;
                _cbHide.Checked = _cfg.HideRealCursor;
                _cbNative.Checked = _cfg.KeepNativeForTextAndResize;
                _cbPause.Checked = _cfg.Paused;
                _cbStartup.Checked = Startup.IsEnabled();

                _tbDrool.Enabled = _cfg.DroolEnabled;
                _tbSpin.Enabled = _cfg.SpinOnLoading;
                _cbNative.Enabled = _cfg.HideRealCursor;
            }
            finally { _loading = false; }
        }

        static int Clamp(int v, TrackBar tb)
        {
            return v < tb.Minimum ? tb.Minimum : (v > tb.Maximum ? tb.Maximum : v);
        }

        static Color Opaque(Color c) { return Color.FromArgb(255, c.R, c.G, c.B); }

        void Apply()
        {
            if (_loading) return;
            _cfg.Save();
            _overlay.OnConfigChanged();
        }

        void PickColor(ref Color target, Button b)
        {
            using (var dlg = new ColorDialog())
            {
                dlg.FullOpen = true;
                dlg.AnyColor = true;
                dlg.Color = Opaque(target);
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                target = Color.FromArgb(target.A, dlg.Color.R, dlg.Color.G, dlg.Color.B);
                b.BackColor = Opaque(target);
                Apply();
            }
        }

        void Uninstall()
        {
            var r = MessageBox.Show(this,
                "Esto va a:\n\n" +
                "  -  quitar el arranque automatico con Windows\n" +
                "  -  restaurar los cursores originales\n" +
                "  -  borrar la configuracion guardada\n" +
                "  -  cerrar el programa\n\n" +
                "Despues te pregunto si queres borrar tambien los archivos.\n\n" +
                "Seguimos?",
                "Desinstalar DildoCursor", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;

            Startup.Disable();
            CursorHider.Restore();
            try { if (Directory.Exists(Config.Dir)) Directory.Delete(Config.Dir, true); }
            catch { }

            string folder = null;
            try { folder = Path.GetDirectoryName(Startup.ExePath); }
            catch { }

            if (!string.IsNullOrEmpty(folder))
            {
                var r2 = MessageBox.Show(this,
                    "Borrar tambien la carpeta del programa?\n\n" + folder,
                    "Desinstalar DildoCursor", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r2 == DialogResult.Yes) ScheduleSelfDelete(folder);
            }

            _overlay.Quit();
        }

        /// <summary>Un .cmd que espera a que cierre el proceso, borra la carpeta y se borra solo.</summary>
        static void ScheduleSelfDelete(string folder)
        {
            try
            {
                string cmd = Path.Combine(Path.GetTempPath(), "dildocursor_uninstall.cmd");
                File.WriteAllText(cmd,
                    "@echo off\r\n" +
                    "ping -n 4 127.0.0.1 >nul\r\n" +
                    "rmdir /s /q \"" + folder + "\"\r\n" +
                    "del \"%~f0\"\r\n");

                var psi = new ProcessStartInfo("cmd.exe", "/c \"" + cmd + "\"");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            catch { }
        }
    }
}
