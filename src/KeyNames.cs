using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace GhostMacro
{
    /// <summary>"Ctrl+Shift+S" gibi metinleri sanal tuş kodlarına çevirir (ve tersi).</summary>
    internal static class KeyNames
    {
        public const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B;

        static readonly int[] ModOrder = { VK_CONTROL, VK_SHIFT, VK_MENU, VK_LWIN };

        public static int ModGroup(int vk)
        {
            switch (vk)
            {
                case 0x10: case 0xA0: case 0xA1: return VK_SHIFT;
                case 0x11: case 0xA2: case 0xA3: return VK_CONTROL;
                case 0x12: case 0xA4: case 0xA5: return VK_MENU;
                case 0x5B: case 0x5C: return VK_LWIN;
                default: return 0;
            }
        }

        static readonly Dictionary<string, int> Aliases = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["ctrl"] = VK_CONTROL, ["control"] = VK_CONTROL, ["ctl"] = VK_CONTROL, ["kontrol"] = VK_CONTROL,
            ["shift"] = VK_SHIFT,
            ["alt"] = VK_MENU,
            ["altgr"] = 0xA5,
            ["win"] = VK_LWIN, ["windows"] = VK_LWIN, ["super"] = VK_LWIN,
            ["enter"] = 0x0D, ["return"] = 0x0D,
            ["esc"] = 0x1B, ["escape"] = 0x1B,
            ["backspace"] = 0x08, ["back"] = 0x08, ["bksp"] = 0x08,
            ["space"] = 0x20, ["spacebar"] = 0x20, ["boşluk"] = 0x20, ["bosluk"] = 0x20,
            ["tab"] = 0x09,
            ["del"] = 0x2E, ["delete"] = 0x2E, ["sil"] = 0x2E,
            ["ins"] = 0x2D, ["insert"] = 0x2D,
            ["pgup"] = 0x21, ["pageup"] = 0x21, ["prior"] = 0x21,
            ["pgdn"] = 0x22, ["pagedown"] = 0x22, ["next"] = 0x22,
            ["home"] = 0x24, ["end"] = 0x23,
            ["left"] = 0x25, ["up"] = 0x26, ["right"] = 0x27, ["down"] = 0x28,
            ["sol"] = 0x25, ["yukarı"] = 0x26, ["yukari"] = 0x26, ["sağ"] = 0x27, ["sag"] = 0x27, ["aşağı"] = 0x28, ["asagi"] = 0x28,
            ["printscreen"] = 0x2C, ["prtsc"] = 0x2C, ["prtscr"] = 0x2C, ["print"] = 0x2C,
            ["capslock"] = 0x14, ["caps"] = 0x14,
            ["numlock"] = 0x90, ["scrolllock"] = 0x91,
            ["pause"] = 0x13, ["break"] = 0x13,
            ["apps"] = 0x5D, ["menu"] = 0x5D, ["contextmenu"] = 0x5D,
        };

        static readonly Dictionary<int, string> Names = new Dictionary<int, string>
        {
            [0x0D] = "Enter", [0x1B] = "Esc", [0x08] = "Backspace", [0x20] = "Space", [0x09] = "Tab",
            [0x2E] = "Delete", [0x2D] = "Insert", [0x21] = "PageUp", [0x22] = "PageDown", [0x24] = "Home", [0x23] = "End",
            [0x25] = "Left", [0x26] = "Up", [0x27] = "Right", [0x28] = "Down",
            [0x2C] = "PrintScreen", [0x14] = "CapsLock", [0x90] = "NumLock", [0x91] = "ScrollLock", [0x13] = "Pause",
            [0x5D] = "Apps", [0xA5] = "AltGr",
            [0x6A] = "NumMultiply", [0x6B] = "NumAdd", [0x6D] = "NumSubtract", [0x6E] = "NumDecimal", [0x6F] = "NumDivide",
        };

        public static string KeyName(int vk)
        {
            if (vk == 0xA5) return "AltGr";
            switch (ModGroup(vk))
            {
                case VK_CONTROL: return "Ctrl";
                case VK_SHIFT: return "Shift";
                case VK_MENU: return "Alt";
                case VK_LWIN: return "Win";
            }
            if (Names.TryGetValue(vk, out var n)) return n;
            if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x6F);
            if (vk >= 0x60 && vk <= 0x69) return "Num" + (vk - 0x60);
            if (vk >= 0x30 && vk <= 0x39) return ((char)vk).ToString();
            uint ch = Native.MapVirtualKey((uint)vk, 2) & 0x7FFFFFFF;
            if (ch > 0x20) return ((char)ch).ToString();
            if (vk >= 0x41 && vk <= 0x5A) return ((char)vk).ToString();
            return ((Keys)vk).ToString();
        }

        public static string Format(IEnumerable<int> vks) => string.Join("+", vks.Select(KeyName));

        static List<string> Tokenize(string text)
        {
            var tokens = new List<string>();
            var cur = new StringBuilder();
            foreach (char c in text ?? "")
            {
                if (c == '+' && cur.ToString().Trim().Length > 0) { tokens.Add(cur.ToString().Trim()); cur.Clear(); }
                else cur.Append(c);
            }
            if (cur.ToString().Trim().Length > 0) tokens.Add(cur.ToString().Trim());
            return tokens;
        }

        /// <param name="mods">Karakter klavyede Shift/AltGr gerektiriyorsa bu modifier'lar eklenir.</param>
        static int ParseToken(string k, List<int> mods)
        {
            if (Aliases.TryGetValue(k, out int v)) return v;
            if (k.Length == 1)
            {
                char c = k[0];
                if (c >= '0' && c <= '9') return c;
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) return char.ToUpperInvariant(c);
                // Klavye düzeninden bağımsız: numpad tuşları her düzende aynı karakteri yazar.
                switch (c)
                {
                    case '+': return 0x6B;
                    case '*': return 0x6A;
                    case '/': return 0x6F;
                }
                short r = Native.VkKeyScan(c);
                if (r != -1 && (r & 0xFF) != 0xFF)
                {
                    int flags = (r >> 8) & 0xFF;
                    if (mods != null)
                    {
                        if ((flags & 6) == 6) mods.Add(0xA5);        // Ctrl+Alt = AltGr
                        else
                        {
                            if ((flags & 2) != 0) mods.Add(VK_CONTROL);
                            if ((flags & 4) != 0) mods.Add(VK_MENU);
                        }
                        if ((flags & 1) != 0) mods.Add(VK_SHIFT);
                    }
                    return r & 0xFF;
                }
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) return char.ToUpperInvariant(c);
                return 0;
            }
            string l = k.ToLowerInvariant();
            if (l[0] == 'f' && int.TryParse(l.Substring(1), out int f) && f >= 1 && f <= 24) return 0x6F + f;
            if (l.StartsWith("num") && int.TryParse(l.Substring(3), out int d) && d >= 0 && d <= 9) return 0x60 + d;
            switch (l)
            {
                case "nummultiply": return 0x6A;
                case "numadd": return 0x6B;
                case "numsubtract": return 0x6D;
                case "numdecimal": return 0x6E;
                case "numdivide": return 0x6F;
            }
            if (Enum.TryParse(k, true, out Keys key))
            {
                int code = (int)key;
                if (code > 0 && code <= 0xFE) return code;
            }
            return 0;
        }

        public static bool TryParse(string text, out List<int> vks, out string error)
        {
            vks = new List<int>();
            error = null;
            var tokens = Tokenize(text);
            if (tokens.Count == 0) { error = L.T("Tuş girilmedi.", "No key given."); return false; }
            foreach (var t in tokens)
            {
                var mods = new List<int>();
                int vk = ParseToken(t, mods);
                if (vk == 0) { error = L.T($"\"{t}\" tanınmadı.", $"\"{t}\" not recognized."); return false; }
                foreach (int m in mods)
                    if (!vks.Any(x => x == m || (m != 0xA5 && ModGroup(x) == m))) vks.Add(m);
                vks.Add(vk);
            }
            return true;
        }

        public static List<int> Parse(string text)
        {
            if (!TryParse(text, out var vks, out var err)) throw new FormatException(err);
            return vks;
        }

        /// <summary>Basılı modifier'lar + tuş → "Ctrl+Shift+S"</summary>
        public static string Combo(IEnumerable<int> heldKeys, int vk)
        {
            var held = new HashSet<int>(heldKeys.Select(ModGroup));
            var list = ModOrder.Where(held.Contains).ToList();
            list.Add(vk);
            return Format(list);
        }
    }
}
