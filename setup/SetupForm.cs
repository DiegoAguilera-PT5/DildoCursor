using System;
using System.Drawing;
using System.Windows.Forms;

namespace DildoCursorSetup
{
    /// <summary>La ventanita del instalador. Una sola pantalla, sin asistentes.</summary>
    internal sealed class SetupForm : Form
    {
        static readonly Color BG = Color.FromArgb(28, 28, 34);
        static readonly Color FG = Color.FromArgb(237, 237, 242);
        static readonly Color DIM = Color.FromArgb(162, 162, 176);
        static readonly Color ACC = Color.FromArgb(232, 106, 138);

        CheckBox _cbAuto, _cbLnk;

        public SetupForm()
        {
            Text = "Instalar " + Program.AppName;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = BG;
            ForeColor = FG;
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(440, 286);

            var title = new Label();
            title.Text = Program.AppName;
            title.Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold);
            title.ForeColor = ACC;
            title.AutoSize = true;
            title.Location = new Point(22, 20);
            Controls.Add(title);

            var sub = new Label();
            sub.Text = "Reemplaza el puntero de Windows por un dildo de goma\n" +
                       "con fisica: se bambolea, babea, se pone duro sobre los\n" +
                       "links y gira cuando algo carga.";
            sub.ForeColor = DIM;
            sub.AutoSize = false;
            sub.Size = new Size(400, 58);
            sub.Location = new Point(24, 56);
            Controls.Add(sub);

            var where = new Label();
            where.Text = "Se instala en:  " + Program.Dest;
            where.ForeColor = Color.FromArgb(130, 130, 146);
            where.AutoSize = false;
            where.Size = new Size(400, 18);
            where.Location = new Point(24, 118);
            Controls.Add(where);

            _cbAuto = new CheckBox();
            _cbAuto.Text = "Arrancar con Windows";
            _cbAuto.Checked = true;
            _cbAuto.ForeColor = FG;
            _cbAuto.AutoSize = true;
            _cbAuto.Location = new Point(24, 148);
            Controls.Add(_cbAuto);

            _cbLnk = new CheckBox();
            _cbLnk.Text = "Crear acceso directo en el menu inicio";
            _cbLnk.Checked = true;
            _cbLnk.ForeColor = FG;
            _cbLnk.AutoSize = true;
            _cbLnk.Location = new Point(24, 176);
            Controls.Add(_cbLnk);

            var hint = new Label();
            hint.Text = "Despues, Ctrl+Alt+P abre el panel de control.";
            hint.ForeColor = DIM;
            hint.AutoSize = true;
            hint.Location = new Point(24, 208);
            Controls.Add(hint);

            var ok = Button("Instalar", new Point(228, 238), ACC, Color.White);
            ok.Click += delegate
            {
                Enabled = false;
                bool done = Program.Install(_cbAuto.Checked, _cbLnk.Checked);
                if (done)
                    MessageBox.Show(this,
                        "Instalado y corriendo.\n\nCtrl+Alt+P abre el panel.\n" +
                        "Para quitarlo: Aplicaciones instaladas de Windows, o el panel.",
                        Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            };
            Controls.Add(ok);

            var cancel = Button("Cancelar", new Point(338, 238),
                                Color.FromArgb(58, 58, 70), FG);
            cancel.Click += delegate { Close(); };
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        static Button Button(string text, Point at, Color back, Color fore)
        {
            var b = new Button();
            b.Text = text;
            b.Location = at;
            b.Size = new Size(96, 30);
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = back;
            b.ForeColor = fore;
            b.FlatAppearance.BorderColor = Color.FromArgb(92, 92, 106);
            b.UseVisualStyleBackColor = false;
            return b;
        }
    }
}
