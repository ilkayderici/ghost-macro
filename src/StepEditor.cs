using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GhostMacro
{
    /// <summary>Tek bir adımı düzenleme penceresi.</summary>
    internal sealed class StepEditor : Form
    {
        readonly Step step;
        readonly ComboBox cmbAction, cmbButton;
        readonly NumericUpDown numX, numY, numX2, numY2, numAmount, numDelay;
        readonly CheckBox chkAtCursor;
        readonly KeyPicker keyPicker;
        readonly TextBox txtText, txtNote;
        readonly Label lblPos, lblAmount;
        readonly Control posPanel;
        readonly TableLayoutPanel table;
        readonly Dictionary<string, Control[]> rows = new Dictionary<string, Control[]>();

        public static bool Edit(IWin32Window owner, Step s, string title)
        {
            using (var f = new StepEditor(s, title))
                return f.ShowDialog(owner) == DialogResult.OK;
        }

        StepEditor(Step s, string title)
        {
            step = s;
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F);
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            table = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12),
                Location = Point.Empty,
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            cmbAction = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280, MaxDropDownItems = 14 };
            foreach (var a in Step.All) cmbAction.Items.Add(Step.ActionName(a));
            AddRow("action", Lbl(L.T("İşlem:", "Action:")), cmbAction);

            cmbButton = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
            cmbButton.Items.AddRange(new object[] { Step.ButtonName(MouseBtn.Left), Step.ButtonName(MouseBtn.Right), Step.ButtonName(MouseBtn.Middle) });
            AddRow("button", Lbl(L.T("Fare tuşu:", "Mouse button:")), cmbButton);

            chkAtCursor = new CheckBox
            {
                Text = L.T("Konum kullanma — imleç o an neredeyse orada", "Ignore position — use wherever the cursor is"),
                AutoSize = true,
            };
            AddRow("atcursor", Lbl(""), chkAtCursor);

            numX = Coord(); numY = Coord();
            lblPos = Lbl(L.T("Konum:", "Position:"));
            posPanel = PosPanel(numX, numY);
            AddRow("pos", lblPos, posPanel);

            numX2 = Coord(); numY2 = Coord();
            AddRow("pos2", Lbl(L.T("Bitiş konumu:", "End position:")), PosPanel(numX2, numY2));

            numAmount = new NumericUpDown { Minimum = -100000, Maximum = 100000, Width = 100 };
            lblAmount = Lbl("");
            AddRow("amount", lblAmount, numAmount);

            keyPicker = new KeyPicker();
            var hint = new Label
            {
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(3, 4, 3, 0),
                Text = L.T(
                    "Kombinasyon için kutuları işaretleyip listeden tuşu seçin. Örn: Ctrl + C, Ctrl+Shift + Esc, Win + R.\n" +
                    "Basılı tut / bırak adımlarında yalnız Ctrl, Shift... işaretleyip tuş seçmeyebilirsiniz.",
                    "For a combination tick the boxes and pick the key from the list. E.g. Ctrl + C, Ctrl+Shift + Esc, Win + R.\n" +
                    "For key down / up steps you may tick only Ctrl, Shift... without picking a key."),
            };
            var keysPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            keysPanel.Controls.AddRange(new Control[] { keyPicker, hint });
            AddRow("keys", Lbl(L.T("Tuş(lar):", "Key(s):")), keysPanel);

            txtText = new TextBox { Multiline = true, AcceptsReturn = true, AcceptsTab = true, ScrollBars = ScrollBars.Vertical, Width = 380, Height = 100 };
            AddRow("text", Lbl(L.T("Yazılacak metin:", "Text to type:")), txtText);

            numDelay = new NumericUpDown { Minimum = 0, Maximum = 86400, DecimalPlaces = 2, Increment = 0.5m, Width = 90 };
            var delayPanel = Flow(numDelay, new Label
            {
                Text = L.T("sn  — bu adımdan ÖNCE beklenecek süre", "s  — wait time BEFORE this step"),
                AutoSize = true,
                Margin = new Padding(3, 6, 3, 0),
            });
            AddRow("delay", Lbl(L.T("Önce bekle:", "Wait before:")), delayPanel);

            txtNote = new TextBox { Width = 380 };
            AddRow("note", Lbl(L.T("Not:", "Note:")), txtNote);

            var ok = Dialogs.Btn(L.T("Tamam", "OK"));
            ok.Click += OnOk;
            var cancel = Dialogs.Btn(L.T("İptal", "Cancel"), DialogResult.Cancel);
            var bp = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
            bp.Controls.Add(cancel);
            bp.Controls.Add(ok);
            table.Controls.Add(bp);
            table.SetColumnSpan(bp, 2);

            Controls.Add(table);
            AcceptButton = ok;
            CancelButton = cancel;

            // değerleri yükle
            cmbAction.SelectedIndex = Array.IndexOf(Step.All, s.Action);
            cmbButton.SelectedIndex = (int)s.Button;
            chkAtCursor.Checked = s.AtCursor;
            SetNum(numX, s.X); SetNum(numY, s.Y); SetNum(numX2, s.X2); SetNum(numY2, s.Y2);
            SetNum(numAmount, s.Amount);
            numDelay.Value = Math.Max(0m, Math.Min(86400m, s.DelayMs / 1000m));
            keyPicker.Value = s.Keys ?? "";
            txtText.Text = (s.Text ?? "").Replace("\n", "\r\n");
            txtNote.Text = s.Note ?? "";

            cmbAction.SelectedIndexChanged += (o, e) => UpdateRows();
            chkAtCursor.CheckedChanged += (o, e) => UpdateRows();
            UpdateRows();
            ResumeLayout(false);
            PerformLayout();

            Shown += (o, e) =>
            {
                var a = CurrentAction;
                if (a == StepAction.TypeText) txtText.Focus();
                else if (!Step.IsKeyAction(a)) { numDelay.Focus(); numDelay.Select(0, numDelay.Text.Length); }
            };
        }

        StepAction CurrentAction => Step.All[Math.Max(0, cmbAction.SelectedIndex)];

        static Label Lbl(string text) =>
            new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(3, 7, 8, 3) };

        static NumericUpDown Coord() => new NumericUpDown { Minimum = -30000, Maximum = 30000, Width = 80 };

        static void SetNum(NumericUpDown n, int v) => n.Value = Math.Max(n.Minimum, Math.Min(n.Maximum, v));

        static FlowLayoutPanel Flow(params Control[] cs)
        {
            var f = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            f.Controls.AddRange(cs);
            return f;
        }

        Control PosPanel(NumericUpDown nx, NumericUpDown ny)
        {
            var pick = new Button { Text = L.T("Ekrandan seç...", "Pick on screen..."), AutoSize = true };
            pick.Click += (o, e) =>
            {
                var p = PointPicker.PickPoint();
                if (p.HasValue) { SetNum(nx, p.Value.X); SetNum(ny, p.Value.Y); }
            };
            Label Small(string t) => new Label { Text = t, AutoSize = true, Margin = new Padding(3, 6, 0, 0) };
            return Flow(Small("X"), nx, Small("Y"), ny, pick);
        }

        void AddRow(string key, Control label, Control c)
        {
            table.Controls.Add(label);
            table.Controls.Add(c);
            rows[key] = new[] { label, c };
        }

        void SetRow(string key, bool visible)
        {
            foreach (var c in rows[key]) c.Visible = visible;
        }

        void UpdateRows()
        {
            var a = CurrentAction;
            SetRow("button", Step.HasButton(a));
            SetRow("atcursor", Step.AllowsAtCursor(a));
            SetRow("pos", Step.HasPosition(a));
            SetRow("pos2", a == StepAction.Drag);
            SetRow("amount", a == StepAction.Wheel || a == StepAction.Drag);
            SetRow("keys", Step.IsKeyAction(a));
            SetRow("text", a == StepAction.TypeText);
            posPanel.Enabled = !(Step.AllowsAtCursor(a) && chkAtCursor.Checked);
            lblPos.Text = a == StepAction.Drag ? L.T("Başlangıç konumu:", "Start position:") : L.T("Konum:", "Position:");

            if (a == StepAction.Wheel)
            {
                lblAmount.Text = L.T("Çentik (+ yukarı, − aşağı):", "Notches (+ up, − down):");
                numAmount.Minimum = -100; numAmount.Maximum = 100;
                if (numAmount.Value == 0) numAmount.Value = -3;
            }
            else if (a == StepAction.Drag)
            {
                lblAmount.Text = L.T("Sürükleme süresi (ms):", "Drag duration (ms):");
                numAmount.Minimum = 0; numAmount.Maximum = 60000;
                if (numAmount.Value < 50) numAmount.Value = 300;
            }
        }

        void OnOk(object sender, EventArgs e)
        {
            var a = CurrentAction;
            string keys = keyPicker.Value;
            if (Step.IsKeyAction(a) && keys.Length == 0)
            {
                MessageBox.Show(this, L.T("Bir tuş veya kombinasyon seçin.", "Pick a key or combination."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                keyPicker.Focus();
                return;
            }
            if (a == StepAction.KeyCombo && !keyPicker.HasKey && keys.Split('+').Length > 1)
            {
                // Sadece modifier'lar: Ctrl+Shift gibi — izin verilir (ör. dil değiştirme)
            }
            if (a == StepAction.TypeText && txtText.Text.Length == 0)
            {
                MessageBox.Show(this, L.T("Yazılacak metni girin.", "Enter the text to type."),
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtText.Focus();
                return;
            }

            step.Action = a;
            step.Button = (MouseBtn)Math.Max(0, cmbButton.SelectedIndex);
            step.AtCursor = Step.AllowsAtCursor(a) && chkAtCursor.Checked;
            step.X = (int)numX.Value; step.Y = (int)numY.Value;
            step.X2 = (int)numX2.Value; step.Y2 = (int)numY2.Value;
            step.Amount = (int)numAmount.Value;
            step.Keys = Step.IsKeyAction(a) ? keys : "";
            step.Text = a == StepAction.TypeText ? txtText.Text.Replace("\r\n", "\n") : "";
            step.DelayMs = (int)Math.Round(numDelay.Value * 1000m);
            step.Note = txtNote.Text;
            DialogResult = DialogResult.OK;
        }
    }
}
