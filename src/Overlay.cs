using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace DildoCursor
{
    /// <summary>
    /// Ventana en capas (layered), topmost, click-through, que sigue al mouse y dibuja el sprite.
    /// Tambien es la que escucha el hotkey global y hospeda el icono de la bandeja.
    /// </summary>
    internal sealed class Overlay : Form
    {
        const int HOTKEY_ID = 0xD11D;
        const int WS_EX_LAYERED = 0x00080000;
        const int WS_EX_TRANSPARENT = 0x00000020;
        const int WS_EX_NOACTIVATE = 0x08000000;
        const int WS_EX_TOOLWINDOW = 0x00000080;

        readonly Config _cfg;
        readonly System.Windows.Forms.Timer _timer;
        readonly SpriteState _state = new SpriteState();
        readonly List<Particle> _parts = new List<Particle>();
        readonly Random _rnd = new Random();
        readonly RubberBody _body = new RubberBody(7);   // el que se ve: base anclada al cursor
        readonly RubberBody _ref = new RubberBody(7);    // gemelo con el ancla fija, da el reposo
        PointF[] _spineDraw;
        Sprite _art;
        bool _bodyReady;
        float _spinBlend;
        int _stillFrames;
        bool _prevClick, _pushing;
        float _pushPhase;
        bool _blankPushed;

        const float Gravity = 1000f;
        const float MaxBend = 0.38f;       // ~22 grados de quiebre maximo entre tramos
        const float SettleSpeed = 400f;    // por debajo de esto, lo ayudamos a quedarse quieto
        const float PushPixels = 17f;      // cuanto estira la estocada del clic
        const float PushSeconds = 0.17f;

        NotifyIcon _tray;
        PanelForm _panel;
        EventWaitHandle _quitSignal;

        // superficie DIB reutilizada entre frames
        int _surfW, _surfH;
        IntPtr _hDib, _memDc, _oldBmp, _bits;
        Bitmap _surface;
        Graphics _g;

        // estado de animacion
        long _lastTicks;
        float _hardness, _throb, _spinDeg, _dripCharge, _speed;
        bool _spinning;
        int _prevX, _prevY;
        float _mvx, _mvy;
        int _lastDrawX = int.MinValue, _lastDrawY = int.MinValue;
        float _lastDrawHard = -1f, _lastDrawThrob, _lastDrawDrip;
        float _handleMapTimer, _topmostTimer, _time;
        bool _shuttingDown;

        public Overlay(Config cfg)
        {
            _cfg = cfg;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Text = "DildoCursor";
            BackColor = Color.Black;

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 15;   // ~60 fps
            _timer.Tick += OnTick;

            Native.POINT p;
            Native.GetCursorPos(out p);
            _prevX = p.X; _prevY = p.Y;
            _lastTicks = DateTime.UtcNow.Ticks;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Bounds = new Rectangle(0, 0, 256, 256);

            // La ventana es toolwindow + sin taskbar, asi que no tiene "main window" y un
            // CloseMainWindow() externo no la cierra. Este evento con nombre es la via oficial
            // para pedirle que salga limpia (si la matan a la fuerza, los cursores quedan
            // invisibles hasta el proximo SPI_SETCURSORS).
            try { _quitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, Program.QuitEventName); }
            catch { _quitSignal = null; }

            _art = Sprite.Load();

            BuildTray();
            ApplyCursorHiding();
            CursorHider.RefreshHandleMap();

            if (!Native.RegisterHotKey(Handle, HOTKEY_ID,
                    Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, (int)Keys.P))
            {
                // Si otra app ya tiene Ctrl+Alt+P, avisamos: queda la bandeja como via de escape.
                ShowBalloon("No pude registrar Ctrl+Alt+P (otra app lo esta usando). " +
                            "Usa el icono de la bandeja para abrir el panel.");
            }

            _timer.Start();
        }

        /// <summary>
        /// Lado de la superficie donde se pinta. Fijo a proposito: probamos ajustarla al
        /// contenido en cada frame y salio PEOR (recrear el DIB cuesta bastante mas que
        /// limpiar y publicar algunos pixeles de mas). Tiene que entrar el cuerpo y las
        /// gotas mientras caen.
        /// </summary>
        int SideForScale()
        {
            int side = (int)(400f * Math.Max(0.4f, Math.Min(2.5f, _cfg.Scale)));
            if (side < 256) side = 256;
            if (side > 1100) side = 1100;
            return side;
        }

        // ------------------------------------------------------------------ bandeja

        void BuildTray()
        {
            _tray = new NotifyIcon();
            _tray.Icon = MakeTrayIcon(_art);
            _tray.Text = "DildoCursor  -  Ctrl+Alt+P para el panel";
            _tray.Visible = true;

            var menu = new ContextMenuStrip();
            menu.Items.Add("Panel de control  (Ctrl+Alt+P)", null, delegate { TogglePanel(); });
            var pause = new ToolStripMenuItem("Pausar");
            pause.CheckOnClick = true;
            pause.Checked = _cfg.Paused;
            pause.Click += delegate
            {
                _cfg.Paused = pause.Checked;
                _cfg.Save();
                ApplyCursorHiding();
                if (_panel != null && !_panel.IsDisposed) _panel.RefreshFromConfig();
            };
            menu.Items.Add(pause);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Salir", null, delegate { Quit(); });
            _tray.ContextMenuStrip = menu;
            _tray.MouseClick += delegate (object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) TogglePanel();
            };
        }

        static Icon MakeTrayIcon(Sprite art)
        {
            using (var bmp = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    var st = new SpriteState();
                    st.Hardness = 1f;
                    st.Length = 27f;
                    st.Art = art;
                    var sp = new PointF[7];
                    double ang = Renderer.RestAngleDeg(1f) * Math.PI / 180.0;
                    float seg = st.Length / (sp.Length - 1);
                    for (int i = 0; i < sp.Length; i++)
                        sp[i] = new PointF(5f + (float)Math.Cos(ang) * seg * i,
                                           4f + (float)Math.Sin(ang) * seg * i);
                    st.Spine = sp;
                    var c = new Config();
                    Renderer.Draw(g, c, st);
                }
                IntPtr h = bmp.GetHicon();
                try { return (Icon)Icon.FromHandle(h).Clone(); }
                finally { DestroyIcon(h); }
            }
        }

        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);

        void ShowBalloon(string text)
        {
            try
            {
                if (_tray == null) return;
                _tray.BalloonTipTitle = "DildoCursor";
                _tray.BalloonTipText = text;
                _tray.ShowBalloonTip(6000);
            }
            catch { }
        }

        // ------------------------------------------------------------------ panel / hotkey

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                TogglePanel();
                return;
            }
            base.WndProc(ref m);
        }

        public void TogglePanel()
        {
            if (_panel != null && !_panel.IsDisposed)
            {
                if (_panel.Visible) { _panel.Close(); _panel = null; return; }
                _panel.Close();
                _panel = null;
            }
            _panel = new PanelForm(_cfg, this);
            _panel.Show();
            _panel.Activate();
        }

        /// <summary>El panel llama a esto cuando el usuario cambia algo.</summary>
        public void OnConfigChanged()
        {
            if (_surfW != SideForScale()) DisposeSurface();   // se recrea en el proximo frame
            ApplyCursorHiding();
        }

        public void ApplyCursorHiding()
        {
            bool hide = _cfg.HideRealCursor && !_cfg.Paused;
            CursorHider.Apply(hide, _cfg.KeepNativeForTextAndResize);
        }

        public void Quit()
        {
            _shuttingDown = true;
            Cleanup();
            Application.Exit();
        }

        // ------------------------------------------------------------------ loop

        void OnTick(object sender, EventArgs e)
        {
            if (_shuttingDown) return;

            long now = DateTime.UtcNow.Ticks;
            float dt = (float)((now - _lastTicks) / (double)TimeSpan.TicksPerSecond);
            _lastTicks = now;
            if (dt <= 0f) return;
            if (dt > 0.1f) dt = 0.1f;   // tras un freeze no queremos saltos gigantes
            _time += dt;

            if (_quitSignal != null && _quitSignal.WaitOne(0)) { Quit(); return; }

            _handleMapTimer += dt;
            if (_handleMapTimer > 1.5f) { _handleMapTimer = 0f; CursorHider.RefreshHandleMap(); }

            _topmostTimer += dt;
            if (_topmostTimer > 2f)
            {
                _topmostTimer = 0f;
                Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            }

            Native.POINT cp;
            if (!Native.GetCursorPos(out cp)) return;

            // velocidad del mouse (suavizada)
            bool cursorQuieto = (cp.X == _prevX && cp.Y == _prevY);
            float vx = (cp.X - _prevX) / dt;
            float vy = (cp.Y - _prevY) / dt;
            _prevX = cp.X; _prevY = cp.Y;
            _mvx += (vx - _mvx) * Math.Min(1f, dt * 14f);
            _mvy += (vy - _mvy) * Math.Min(1f, dt * 14f);
            float inst = (float)Math.Sqrt(vx * vx + vy * vy);
            _speed += (inst - _speed) * Math.Min(1f, dt * 10f);

            CursorKind kind = CursorHider.Current();

            bool hideSprite = _cfg.Paused
                              || kind == CursorKind.Hidden
                              || (_cfg.KeepNativeForTextAndResize &&
                                  (kind == CursorKind.Text || kind == CursorKind.Resize));

            if (hideSprite)
            {
                _parts.Clear();
                _dripCharge = 0f;
                _bodyReady = false;         // que reaparezca quieto, sin latigazo

                // Publicar la superficie vacia una sola vez. Repetirlo en cada frame
                // mientras el puntero esta sobre texto (o sea, media jornada) quemaba CPU
                // para no mostrar nada.
                if (!_blankPushed || cp.X != _lastDrawX || cp.Y != _lastDrawY)
                {
                    EnsureSurface(128, 128);
                    _g.ResetTransform();
                    _g.Clear(Color.Transparent);
                    Push(cp.X - 64, cp.Y - 64);
                    _blankPushed = true;
                    _lastDrawX = cp.X; _lastDrawY = cp.Y;
                    _lastDrawHard = -1f;    // fuerza redibujo al volver
                }
                return;
            }

            // --- duro / blando ---
            float target = (_cfg.HardOnClickable && kind == CursorKind.Hand) ? 1f : 0f;
            _hardness += (target - _hardness) * Math.Min(1f, dt * 11f);
            _throb = _hardness > 0.5f
                ? (float)Math.Sin(_time * 7.5)
                : _throb * (1f - Math.Min(1f, dt * 6f));

            // --- giro al cargar ---
            bool wantSpin = _cfg.SpinOnLoading && kind == CursorKind.Wait;
            float spinStep = _cfg.SpinSpeed * 360f * dt;
            if (wantSpin)
            {
                _spinning = true;
                _spinDeg = (_spinDeg + spinStep) % 360f;
            }
            else if (_spinning)
            {
                // No cortamos de golpe: terminamos la vuelta en curso y recien ahi paramos.
                float nd = _spinDeg + spinStep;
                if (nd >= 360f) { _spinning = false; _spinDeg = 0f; }
                else _spinDeg = nd;
            }

            // --- fisica de la goma ---
            // Lo que se ancla es la BASE: la punta queda libre y es la que se bambolea.
            float sc = Math.Max(0.4f, Math.Min(2.5f, _cfg.Scale));
            float L = Renderer.BodyLength(sc, _hardness);
            float seg = L / (_body.P.Length - 1);

            // La cadena va de la base hacia la punta, o sea al reves del angulo de reposo.
            // Girando, es esa direccion la que gira: la punta la persigue con inercia y se
            // arrastra como un latigo en vez de rotar rigido.
            float restDeg = (_spinning ? _spinDeg : Renderer.RestAngleDeg(_hardness)) + 180f;
            float restRad = restDeg * (float)Math.PI / 180f;

            // Colgando de la base, la gravedad hace mucho mas momento que cuando se
            // anclaba la punta: hace falta bastante mas rigidez para que no se desplome.
            //
            // Duro tiene que SER duro: el resorte se vuelve varias veces mas fuerte, se
            // amortigua mucho mas, y ademas casi no se le permite quebrar de un tramo al
            // siguiente. Con solo subir el resorte seguia pareciendo goma.
            float wob = Math.Max(0.15f, Math.Min(2.5f, _cfg.Wobble));
            float hard01 = Math.Max(0f, Math.Min(1f, _hardness));
            float stiffness = (520f + 4300f * hard01) / wob;
            float damping = (16f + 30f * hard01) / (0.6f + 0.4f * wob);
            float bend = MaxBend * (1f - 0.80f * hard01);   // de ~22 a ~4 grados

            // Cuerpo de referencia: el mismo cuerpo pero con el ancla clavada en el origen.
            // Como nunca se mueve, su punta marca exactamente donde cae la punta en reposo,
            // incluida la curva que le hace la gravedad. Restando ese offset, la punta del
            // dibujo queda justo sobre el hotspot del cursor cuando el mouse esta quieto
            // (que es cuando hace falta apuntar), y libre de bambolearse cuando se mueve.
            // El de referencia va sobreamortiguado a proposito: solo nos interesa su forma
            // final, y asi llega sin oscilar (si oscilara, haria oscilar al ancla del otro).
            float refDamping = 2.2f * (float)Math.Sqrt(stiffness);

            if (!_bodyReady)
            {
                // Lo asentamos de una, antes del primer frame: si arranca recto, se desploma
                // hasta su forma de reposo y eso se ve como un bamboleo que nadie provoco.
                _ref.Reset(PointF.Empty, restRad, seg);
                for (int i = 0; i < 260; i++)
                    _ref.Step(1f / 120f, PointF.Empty, restRad, seg, stiffness, refDamping, Gravity, bend);
            }

            _ref.Step(dt, PointF.Empty, restRad, seg, stiffness, refDamping, Gravity, bend);
            PointF refTip = _ref.P[_ref.P.Length - 1];

            // Al hacer clic, da una estocada corta hacia adelante, como si quisiera
            // apretar el boton con la punta.
            bool click = (Native.GetAsyncKeyState(Native.VK_LBUTTON) & 0x8000) != 0
                      || (Native.GetAsyncKeyState(Native.VK_RBUTTON) & 0x8000) != 0;
            if (click && !_prevClick && _cfg.ClickPush) { _pushing = true; _pushPhase = 0f; }
            _prevClick = click;

            float push = 0f;
            if (_pushing)
            {
                _pushPhase += dt / PushSeconds;
                if (_pushPhase >= 1f) { _pushing = false; _pushPhase = 0f; }
                else push = (float)Math.Sin(Math.PI * _pushPhase) * PushPixels * sc;
            }

            // Girando no compensamos: queremos que la punta barra el circulo bien ancho.
            float spinTarget = _spinning ? 1f : 0f;
            _spinBlend += (spinTarget - _spinBlend) * Math.Min(1f, dt * 4f);
            float comp = 1f - _spinBlend;

            // El empuje va en la direccion base -> punta, que es justo la del reposo.
            var anchor = new PointF(
                cp.X - refTip.X * comp + (float)Math.Cos(restRad) * push,
                cp.Y - refTip.Y * comp + (float)Math.Sin(restRad) * push);

            if (!_bodyReady)
            {
                _body.Reset(anchor, restRad, seg);
                _body.PullToRest(_ref, anchor, 1f);   // nace ya en reposo, quieto
                _bodyReady = true;
            }
            _body.Step(dt, anchor, restRad, seg, stiffness, damping, Gravity, bend);

            // Un dildo de verdad, quieto, esta quieto: se mueve cuando lo mueven y despues
            // se acomoda. Si el mouse no se movio y el rebote ya es imperceptible, lo
            // llevamos del todo a la forma de reposo en vez de dejarlo vibrando.
            _stillFrames = cursorQuieto ? _stillFrames + 1 : 0;
            if (!_spinning && _stillFrames > 2 && _body.MaxSpeed() < 26f)
                _body.PullToRest(_ref, anchor, Math.Min(1f, dt * 9f));

            // El Renderer quiere el eje de la punta hacia la base; la fisica lo da al reves.
            int n = _body.P.Length;
            if (_spineDraw == null || _spineDraw.Length != n) _spineDraw = new PointF[n];
            for (int i = 0; i < n; i++) _spineDraw[i] = _body.P[n - 1 - i];

            _state.Spine = _spineDraw;
            _state.Length = L;
            _state.Art = _art;
            _state.Hardness = _hardness;
            _state.Throb = _throb;
            _state.Parts = _parts;

            // --- babeo ---
            if (_cfg.DroolEnabled && _cfg.DroolAmount > 0.01f)
            {
                // Casi no babea quieto; cuanto mas rapido se mueve el mouse, mas chorrea.
                float rate = (0.09f + _speed / 450f) * _cfg.DroolAmount;
                if (_spinning) rate *= 2.4f;
                _dripCharge += dt * rate;
                if (_dripCharge >= 1f)
                {
                    _dripCharge = 0f;
                    PointF o = Renderer.DripOrigin(_state);
                    SpawnDrop(o.X, o.Y, _state.GlansHalf, L);
                }
            }
            else
            {
                _dripCharge = 0f;
                _parts.Clear();
            }
            UpdateParticles(dt, cp.X, cp.Y);
            _state.DripCharge = _cfg.DroolEnabled ? _dripCharge : 0f;

            // --- render ---
            // Si nada cambio desde el ultimo frame no hay para que repintar: con el mouse
            // quieto esto deja el consumo de CPU practicamente en cero.
            bool idle = !_spinning
                        && !_pushing
                        && _parts.Count == 0
                        && _body.MaxSpeed() < 2.5f          // la goma ya dejo de bambolearse
                        && cp.X == _lastDrawX && cp.Y == _lastDrawY
                        && Math.Abs(_hardness - _lastDrawHard) < 0.004f
                        && Math.Abs(_throb - _lastDrawThrob) < 0.012f
                        && Math.Abs(_state.DripCharge - _lastDrawDrip) < 0.02f;
            if (idle) return;

            int side = SideForScale();
            EnsureSurface(side, side);
            int ox = cp.X - side / 2;
            int oy = cp.Y - side / 2;

            _g.ResetTransform();
            _g.Clear(Color.Transparent);
            _g.TranslateTransform(-ox, -oy);     // dibujamos todo en coordenadas de pantalla
            Renderer.Draw(_g, _cfg, _state);

            Push(ox, oy);

            _blankPushed = false;
            _lastDrawX = cp.X; _lastDrawY = cp.Y;
            _lastDrawHard = _hardness; _lastDrawThrob = _throb; _lastDrawDrip = _state.DripCharge;
        }

        void SpawnDrop(float x, float y, float R, float L)
        {
            if (_parts.Count > 70) return;

            var p = new Particle();
            p.X = x + (float)(_rnd.NextDouble() - 0.5) * R * 0.5f;
            p.Y = y + R * 0.25f;
            // Nace con el ancho que tenia la gota colgando justo antes de soltarse
            // (ver DrawHangingDrip: ahi llega a 0,45 del semigrosor). Antes nacia a menos
            // de la mitad y se veia como si cambiara de gota al caer.
            p.R = R * (0.40f + (float)_rnd.NextDouble() * 0.10f);
            p.MaxLife = 0.40f + (float)_rnd.NextDouble() * 0.22f;
            p.Life = p.MaxLife;
            p.Wob = (float)(_rnd.NextDouble() * Math.PI * 2);

            // arrastra algo de la inercia del mouse
            p.VX = _mvx * 0.22f + (float)(_rnd.NextDouble() - 0.5) * 60f;
            p.VY = _mvy * 0.18f + 30f + (float)_rnd.NextDouble() * 70f;

            if (_spinning)
            {
                // sale despedida por la tangente del giro
                double rad = _spinDeg * Math.PI / 180.0;
                float tang = _cfg.SpinSpeed * L * 2.2f;
                p.VX += (float)(-Math.Sin(rad) * tang);
                p.VY += (float)(Math.Cos(rad) * tang);
            }

            _parts.Add(p);
        }

        void UpdateParticles(float dt, int cx, int cy)
        {
            float half = SideForScale() * 0.5f + 30f;
            for (int i = _parts.Count - 1; i >= 0; i--)
            {
                var p = _parts[i];
                p.VY += 1150f * dt;
                p.VX *= (1f - Math.Min(0.9f, dt * 1.6f));
                p.X += p.VX * dt;
                p.Y += p.VY * dt;
                p.Wob += dt * 11f;
                p.Life -= dt;

                // si murio o se fue de la ventana, fuera (no se veria de todos modos)
                if (p.Life <= 0f || Math.Abs(p.X - cx) > half || Math.Abs(p.Y - cy) > half)
                    _parts.RemoveAt(i);
            }
        }

        // ------------------------------------------------------------------ superficie DIB

        void EnsureSurface(int w, int h)
        {
            if (_surface != null && _surfW == w && _surfH == h) return;

            DisposeSurface();
            _surfW = w; _surfH = h;

            var bmi = new Native.BITMAPINFOHEADER();
            bmi.biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
            bmi.biWidth = w;
            bmi.biHeight = -h;           // negativo = top-down, igual que GDI+
            bmi.biPlanes = 1;
            bmi.biBitCount = 32;
            bmi.biCompression = 0;       // BI_RGB

            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            try
            {
                _hDib = Native.CreateDIBSection(screenDc, ref bmi, 0, out _bits, IntPtr.Zero, 0);
                _memDc = Native.CreateCompatibleDC(screenDc);
                _oldBmp = Native.SelectObject(_memDc, _hDib);
            }
            finally { Native.ReleaseDC(IntPtr.Zero, screenDc); }

            // Bitmap GDI+ apuntando directo a los bits del DIB: cero copias por frame.
            _surface = new Bitmap(w, h, w * 4, PixelFormat.Format32bppPArgb, _bits);
            _g = Graphics.FromImage(_surface);
        }

        void Push(int ox, int oy)
        {
            Native.GdiFlush();

            var dst = new Native.POINT { X = ox, Y = oy };
            var src = new Native.POINT { X = 0, Y = 0 };
            var size = new Native.SIZE { cx = _surfW, cy = _surfH };
            var bf = new Native.BLENDFUNCTION
            {
                BlendOp = Native.AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = (byte)(255f * Math.Max(0.15f, Math.Min(1f, _cfg.Alpha))),
                AlphaFormat = Native.AC_SRC_ALPHA
            };

            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            try
            {
                Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, _memDc, ref src, 0,
                    ref bf, Native.ULW_ALPHA);
            }
            finally { Native.ReleaseDC(IntPtr.Zero, screenDc); }
        }

        void DisposeSurface()
        {
            if (_g != null) { _g.Dispose(); _g = null; }
            if (_surface != null) { _surface.Dispose(); _surface = null; }
            if (_memDc != IntPtr.Zero)
            {
                if (_oldBmp != IntPtr.Zero) Native.SelectObject(_memDc, _oldBmp);
                Native.DeleteDC(_memDc);
                _memDc = IntPtr.Zero;
                _oldBmp = IntPtr.Zero;
            }
            if (_hDib != IntPtr.Zero) { Native.DeleteObject(_hDib); _hDib = IntPtr.Zero; }
            _bits = IntPtr.Zero;
            _surfW = 0; _surfH = 0;
        }

        // ------------------------------------------------------------------ cierre

        void Cleanup()
        {
            try { _timer.Stop(); } catch { }
            try { Native.UnregisterHotKey(Handle, HOTKEY_ID); } catch { }
            CursorHider.Restore();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
            if (_quitSignal != null) { _quitSignal.Close(); _quitSignal = null; }
            DisposeSurface();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _shuttingDown = true;
            Cleanup();
            base.OnFormClosing(e);
        }
    }
}
