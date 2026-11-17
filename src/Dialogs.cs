using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GhostMacro
{
    internal static class Dialogs
    {
        public static Form Make(string title) => new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.CenterParent,
            AutoScaleDimensions = new SizeF(96F, 96F),
            AutoScaleMode = AutoScaleMode.Dpi,
            Font = new Font("Segoe UI", 9F),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };

        public static Label Lbl(string text) =>
            new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 10, 3) };

        public static Button Btn(string text, DialogResult r = DialogResult.None) =>
            new Button { Text = text, DialogResult = r, AutoSize = true, Padding = new Padding(8, 1, 8, 1) };
    }

    internal static class AboutDialog
    {
        public const string RepoUrl = "https://github.com/ilkayderici/ghost-macro";
        public const string SiteUrl = "https://ilkayderici.com.tr";

        public static void OpenUrl(string url)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { }
        }

        static LinkLabel Link(string text, string url)
        {
            var l = new LinkLabel { Text = text, AutoSize = true, Margin = new Padding(3, 4, 3, 3) };
            l.LinkClicked += (s, e) => OpenUrl(url);
            return l;
        }

        public static void Show(IWin32Window owner)
        {
            using (var f = Dialogs.Make(L.T("Hakkında", "About")))
            {
                var t = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Padding = new Padding(16), Location = Point.Empty };
                var ver = typeof(AboutDialog).Assembly.GetName().Version;
                t.Controls.Add(new Label
                {
                    Text = "GhostMacro " + ver.ToString(3),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                    Margin = new Padding(3, 0, 3, 4),
                });
                t.Controls.Add(Dialogs.Lbl(L.T("Fare ve klavye makro kaydedici / oynatıcı.", "Mouse and keyboard macro recorder / player.")));
                t.Controls.Add(Dialogs.Lbl(L.T("© 2026 ilkay Derici. Tüm hakları saklıdır.", "© 2026 ilkay Derici. All rights reserved.")));
                t.Controls.Add(Link("GitHub: " + RepoUrl.Replace("https://", ""), RepoUrl));
                t.Controls.Add(Link("Web: " + SiteUrl.Replace("https://", ""), SiteUrl));
                var ok = Dialogs.Btn(L.T("Tamam", "OK"), DialogResult.OK);
                var bp = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
                bp.Controls.Add(ok);
                t.Controls.Add(bp);
                f.Controls.Add(t);
                f.AcceptButton = ok;
                f.CancelButton = ok;
                f.ShowDialog(owner);
            }
        }
    }

    internal static class NumberDialog
    {
        public static int? Ask(IWin32Window owner, string title, string label, int value, int min, int max)
        {
            using (var f = Dialogs.Make(title))
            {
                var t = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Padding = new Padding(12), Location = Point.Empty };
                var num = new NumericUpDown { Minimum = min, Maximum = max, Width = 120, Increment = 100 };
                num.Value = Math.Max(min, Math.Min(max, value));
                t.Controls.Add(Dialogs.Lbl(label));
                t.Controls.Add(num);
                var ok = Dialogs.Btn(L.T("Tamam", "OK"), DialogResult.OK);
                var cancel = Dialogs.Btn(L.T("İptal", "Cancel"), DialogResult.Cancel);
                var bp = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
                bp.Controls.Add(cancel);
                bp.Controls.Add(ok);
                t.Controls.Add(bp);
                t.SetColumnSpan(bp, 2);
                f.Controls.Add(t);
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                f.Shown += (s, e) => { num.Focus(); num.Select(0, num.Text.Length); };
                return f.ShowDialog(owner) == DialogResult.OK ? (int)num.Value : (int?)null;
            }
        }

        /// <summary>Saniye olarak sorar (ondalıklı), sonucu ms döner.</summary>
        public static int? AskSeconds(IWin32Window owner, string title, string label, int ms)
        {
            using (var f = Dialogs.Make(title))
            {
                var t = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, Padding = new Padding(12), Location = Point.Empty };
                var num = new NumericUpDown { Minimum = 0, Maximum = 86400, DecimalPlaces = 2, Increment = 0.5m, Width = 100 };
                num.Value = Math.Max(0m, Math.Min(86400m, ms / 1000m));
                t.Controls.Add(Dialogs.Lbl(label));
                t.Controls.Add(num);
                t.Controls.Add(Dialogs.Lbl(L.T("sn", "s")));
                var ok = Dialogs.Btn(L.T("Tamam", "OK"), DialogResult.OK);
                var cancel = Dialogs.Btn(L.T("İptal", "Cancel"), DialogResult.Cancel);
                var bp = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
                bp.Controls.Add(cancel);
                bp.Controls.Add(ok);
                t.Controls.Add(bp);
                t.SetColumnSpan(bp, 3);
                f.Controls.Add(t);
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                f.Shown += (s, e) => { num.Focus(); num.Select(0, num.Text.Length); };
                return f.ShowDialog(owner) == DialogResult.OK ? (int)Math.Round(num.Value * 1000m) : (int?)null;
            }
        }
    }

    internal static class HotkeyDialog
    {
        public static bool Edit(IWin32Window owner, AppSettings cfg)
        {
            using (var f = Dialogs.Make(L.T("Kısayol tuşları", "Hotkeys")))
            {
                var t = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(12), Location = Point.Empty };
                string[] names =
                {
                    L.T("Kaydı başlat / bitir", "Start / stop recording"),
                    L.T("Oynat", "Play"),
                    L.T("Durdur (oynatma / kayıt)", "Stop (playback / recording)"),
                    L.T("İmlecin olduğu yere tıklama adımı ekle", "Add a click step at the cursor"),
                };
                string[] values = { cfg.HkRecord, cfg.HkPlay, cfg.HkStop, cfg.HkCapture };
                var pickers = new KeyPicker[4];
                for (int i = 0; i < 4; i++)
                {
                    t.Controls.Add(Dialogs.Lbl(names[i] + ":"));
                    pickers[i] = new KeyPicker { Value = values[i] };
                    t.Controls.Add(pickers[i]);
                }

                var note = new Label
                {
                    AutoSize = true,
                    ForeColor = SystemColors.GrayText,
                    Margin = new Padding(3, 10, 3, 0),
                    Text = L.T(
                        "Kısayollar hangi pencere önde olursa olsun çalışır. Hiçbir şey seçilmezse o kısayol kapalıdır.\n" +
                        "Durdur tuşu yalnızca kayıt veya oynatma sırasında yakalanır; diğer zamanlarda normal çalışır.",
                        "Hotkeys work whichever window is in front. If nothing is selected, that hotkey is off.\n" +
                        "The stop key is only captured during recording or playback; otherwise it works normally."),
                };
                t.Controls.Add(note);
                t.SetColumnSpan(note, 2);

                var ok = Dialogs.Btn(L.T("Tamam", "OK"));
                var cancel = Dialogs.Btn(L.T("İptal", "Cancel"), DialogResult.Cancel);
                var def = Dialogs.Btn(L.T("Varsayılanlar", "Defaults"));
                def.Click += (s, e) =>
                {
                    pickers[0].Value = "F9";
                    pickers[1].Value = "F10";
                    pickers[2].Value = "F12";
                    pickers[3].Value = "F8";
                };
                ok.Click += (s, e) =>
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (!pickers[i].IsEmpty && !pickers[i].HasKey)
                        {
                            MessageBox.Show(f, L.T(
                                $"\"{names[i]}\" için listeden bir tuş da seçin (yalnız Ctrl / Shift / Alt / Win olmaz).",
                                $"Also pick a key from the list for \"{names[i]}\" (Ctrl / Shift / Alt / Win alone is not enough)."),
                                f.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                    var v = pickers.Select(p => p.Value).ToArray();
                    var used = v.Where(x => x.Length > 0).ToList();
                    if (used.Count != used.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                    {
                        MessageBox.Show(f, L.T("Aynı kısayol iki işe atanmış.", "The same hotkey is assigned twice."),
                            f.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    cfg.HkRecord = v[0];
                    cfg.HkPlay = v[1];
                    cfg.HkStop = v[2];
                    cfg.HkCapture = v[3];
                    f.DialogResult = DialogResult.OK;
                };

                var bp = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0) };
                bp.Controls.Add(cancel);
                bp.Controls.Add(ok);
                bp.Controls.Add(def);
                t.Controls.Add(bp);
                t.SetColumnSpan(bp, 2);
                f.Controls.Add(t);
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                return f.ShowDialog(owner) == DialogResult.OK;
            }
        }
    }
}
