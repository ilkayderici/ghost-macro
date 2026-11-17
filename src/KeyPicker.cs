using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace GhostMacro
{
    /// <summary>Ctrl / Shift / Alt / Win kutuları + açılır tuş listesi: tuş veya kombinasyon seçici.</summary>
    internal sealed class KeyPicker : FlowLayoutPanel
    {
        readonly CheckBox ctrl, shift, alt, win;
        readonly ComboBox key;
        readonly List<int> keys = new List<int>();
        public event EventHandler ValueChanged;

        public KeyPicker()
        {
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            WrapContents = false;
            Margin = new Padding(0, 2, 0, 2);

            ctrl = Mod("Ctrl");
            shift = Mod("Shift");
            alt = Mod("Alt");
            win = Mod("Win");

            key = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, MaxDropDownItems = 24, Margin = new Padding(4, 2, 0, 0) };
            key.Items.Add(L.T("(tuş yok)", "(no key)"));
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var vk in Candidates())
            {
                string n = KeyNames.KeyName(vk);
                if (string.IsNullOrWhiteSpace(n) || !seen.Add(n)) continue;
                keys.Add(vk);
                key.Items.Add(n);
            }
            key.SelectedIndex = 0;
            key.SelectedIndexChanged += (s, e) => ValueChanged?.Invoke(this, EventArgs.Empty);

            Controls.AddRange(new Control[]
            {
                ctrl, shift, alt, win,
                new Label { Text = "+", AutoSize = true, Margin = new Padding(2, 6, 0, 0) },
                key,
            });
        }

        CheckBox Mod(string text)
        {
            var c = new CheckBox { Text = text, AutoSize = true, Margin = new Padding(0, 4, 6, 0) };
            c.CheckedChanged += (s, e) => ValueChanged?.Invoke(this, EventArgs.Empty);
            return c;
        }

        static IEnumerable<int> Candidates()
        {
            for (int c = 0x41; c <= 0x5A; c++) yield return c;          // A-Z
            for (int c = 0x30; c <= 0x39; c++) yield return c;          // 0-9
            for (int f = 0x70; f <= 0x87; f++) yield return f;          // F1-F24
            foreach (var v in new[] { 0x0D, 0x1B, 0x20, 0x09, 0x08, 0x2E, 0x2D, 0x24, 0x23, 0x21, 0x22,
                                      0x25, 0x26, 0x27, 0x28, 0x2C, 0x13, 0x14, 0x90, 0x91, 0x5D })
                yield return v;
            for (int n = 0x60; n <= 0x69; n++) yield return n;          // Num0-Num9
            foreach (var v in new[] { 0x6A, 0x6B, 0x6D, 0x6E, 0x6F }) yield return v;
            // Noktalama tuşları (klavye düzenine göre: , . ö ç ş ğ ü ...)
            foreach (var v in new[] { 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0, 0xDB, 0xDC, 0xDD, 0xDE, 0xDF, 0xE2 })
                if ((Native.MapVirtualKey((uint)v, 2) & 0x7FFFFFFF) != 0) yield return v;
            // Medya / tarayıcı tuşları
            foreach (var v in new[] { 0xAD, 0xAE, 0xAF, 0xB0, 0xB1, 0xB2, 0xB3, 0xA6, 0xA7, 0xA8, 0xAC })
                yield return v;
        }

        public bool HasKey => key.SelectedIndex > 0;
        public bool IsEmpty => !HasKey && !ctrl.Checked && !shift.Checked && !alt.Checked && !win.Checked;

        public string Value
        {
            get
            {
                var v = new List<int>();
                if (ctrl.Checked) v.Add(KeyNames.VK_CONTROL);
                if (shift.Checked) v.Add(KeyNames.VK_SHIFT);
                if (alt.Checked) v.Add(KeyNames.VK_MENU);
                if (win.Checked) v.Add(KeyNames.VK_LWIN);
                if (HasKey) v.Add(keys[key.SelectedIndex - 1]);
                return v.Count == 0 ? "" : KeyNames.Format(v);
            }
            set
            {
                ctrl.Checked = shift.Checked = alt.Checked = win.Checked = false;
                key.SelectedIndex = 0;
                if (string.IsNullOrWhiteSpace(value) || !KeyNames.TryParse(value, out var vks, out _)) return;
                foreach (var vk in vks)
                {
                    switch (KeyNames.ModGroup(vk))
                    {
                        case KeyNames.VK_CONTROL: ctrl.Checked = true; break;
                        case KeyNames.VK_SHIFT: shift.Checked = true; break;
                        case KeyNames.VK_MENU: alt.Checked = true; break;
                        case KeyNames.VK_LWIN: win.Checked = true; break;
                        default:
                            int i = keys.IndexOf(vk);
                            if (i < 0)
                            {
                                keys.Add(vk);
                                key.Items.Add(KeyNames.KeyName(vk));
                                i = keys.Count - 1;
                            }
                            key.SelectedIndex = i + 1;
                            break;
                    }
                }
            }
        }
    }
}
