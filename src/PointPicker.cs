using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GhostMacro
{
    /// <summary>Tüm ekranı kaplayan yarı saydam katman; tıklanan noktanın koordinatını verir.</summary>
    internal sealed class PointPicker : Form
    {
        public Point Picked { get; private set; }
        readonly TipForm tip = new TipForm();

        public PointPicker()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            Bounds = SystemInformation.VirtualScreen;
            BackColor = Color.Black;
            Opacity = 0.25;
            Cursor = Cursors.Cross;
            KeyPreview = true;

            MouseMove += (s, e) => UpdateTip();
            MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) { Picked = Cursor.Position; DialogResult = DialogResult.OK; }
                else DialogResult = DialogResult.Cancel;
            };
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel; };
            Shown += (s, e) => { tip.Show(this); UpdateTip(); Activate(); };
            FormClosed += (s, e) => tip.Close();
        }

        void UpdateTip()
        {
            var p = Cursor.Position;
            tip.SetText($"X: {p.X}    Y: {p.Y}\n" + L.T("Sol tık: seç  ·  Sağ tık / Esc: iptal", "Left click: pick  ·  Right click / Esc: cancel"));
            var scr = Screen.FromPoint(p).Bounds;
            int x = p.X + 22, y = p.Y + 22;
            if (x + tip.Width > scr.Right) x = p.X - 22 - tip.Width;
            if (y + tip.Height > scr.Bottom) y = p.Y - 22 - tip.Height;
            tip.Location = new Point(x, y);
        }

        /// <summary>Açık pencereleri gizler, ekrandan nokta seçtirir.</summary>
        public static Point? PickPoint()
        {
            var forms = Application.OpenForms.Cast<Form>().Where(f => f.Visible).ToList();
            var opacities = forms.Select(f => f.Opacity).ToList();
            foreach (var f in forms) f.Opacity = 0;
            Application.DoEvents();
            try
            {
                using (var p = new PointPicker())
                    return p.ShowDialog() == DialogResult.OK ? p.Picked : (Point?)null;
            }
            finally
            {
                for (int i = 0; i < forms.Count; i++)
                    if (!forms[i].IsDisposed) forms[i].Opacity = opacities[i];
            }
        }

        sealed class TipForm : Form
        {
            readonly Label lbl;

            public TipForm()
            {
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                TopMost = true;
                StartPosition = FormStartPosition.Manual;
                BackColor = Color.FromArgb(255, 255, 225);
                Opacity = 0.97;
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;
                lbl = new Label
                {
                    AutoSize = true,
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = Color.Black,
                    Padding = new Padding(8),
                    Location = Point.Empty,
                };
                Controls.Add(lbl);
            }

            public void SetText(string t) => lbl.Text = t;

            protected override bool ShowWithoutActivation => true;

            protected override CreateParams CreateParams
            {
                get
                {
                    var cp = base.CreateParams;
                    // NOACTIVATE | TOOLWINDOW | TRANSPARENT (tıklamalar alttaki katmana geçer)
                    cp.ExStyle |= 0x08000000 | 0x80 | 0x20;
                    return cp;
                }
            }
        }
    }
}
