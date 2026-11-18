using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using static GhostMacro.Native;

namespace GhostMacro
{
    /// <summary>"--selftest out.txt" ile çalışan iç testler.</summary>
    internal static class SelfTest
    {
        static readonly StringBuilder log = new StringBuilder();
        static int fails;

        static void Check(string name, bool ok, string detail = "")
        {
            if (!ok) fails++;
            log.AppendLine((ok ? "PASS " : "FAIL ") + name + (detail.Length > 0 ? "  [" + detail + "]" : ""));
        }

        public static int Run(string outFile)
        {
            void T(string name, Action a)
            {
                try { a(); }
                catch (Exception ex) { Check(name + " (exception)", false, ex.GetType().Name + ": " + ex.Message); }
            }

            T("keynames", TestKeyNames);
            T("macro xml", TestXml);
            T("recorder", TestRecorder);
            T("keypicker", TestKeyPicker);
            T("player", TestPlayer);
            T("humanizer", TestHumanizer);

            log.AppendLine(fails == 0 ? "ALL PASS" : fails + " FAIL");
            File.WriteAllText(outFile, log.ToString(), Encoding.UTF8);
            return fails == 0 ? 0 : 1;
        }

        static void TestKeyNames()
        {
            var v = KeyNames.Parse("Ctrl+Shift+S");
            Check("parse Ctrl+Shift+S", v.Count == 3 && v[0] == 0x11 && v[1] == 0x10 && v[2] == 0x53, string.Join(",", v));
            Check("format", KeyNames.Format(v) == "Ctrl+Shift+S", KeyNames.Format(v));
            foreach (var s in new[] { "Win+R", "Alt+F4", "Enter", "Ctrl+Alt+Delete", "F12", "Num5", "Ctrl+Shift+Esc" })
            {
                var p = KeyNames.Parse(s);
                Check("roundtrip " + s, KeyNames.Format(p) == s, KeyNames.Format(p));
            }
            Check("bad token", !KeyNames.TryParse("Ctrl+Foo", out _, out _));
            Check("plus key", KeyNames.TryParse("Ctrl++", out var pl, out _) && pl.Count == 2 && pl[0] == 0x11 && pl[1] == 0x6B, pl == null ? "" : string.Join(",", pl));
            Check("star key", KeyNames.TryParse("*", out var st, out _) && st.Count == 1 && st[0] == 0x6A, st == null ? "" : string.Join(",", st));
            Check("plus roundtrip", KeyNames.Format(KeyNames.Parse("Ctrl++")) == "Ctrl+NumAdd", KeyNames.Format(KeyNames.Parse("Ctrl++")));
        }

        static void TestXml()
        {
            var path = Path.Combine(Path.GetTempPath(), "gm_selftest.gmacro");
            var steps = new List<Step>
            {
                new Step { Action = StepAction.Click, X = 10, Y = 20, DelayMs = 300, Note = "ğüşıöç İĞÜŞÖÇ" },
                new Step { Action = StepAction.TypeText, Text = "satır1\nsatır2", DelayMs = 0 },
                new Step { Action = StepAction.KeyCombo, Keys = "Ctrl+C" },
            };
            Store.SaveMacro(path, steps);
            var back = Store.LoadMacro(path);
            File.Delete(path);
            Check("xml count", back.Count == 3);
            Check("xml turkish", back[0].Note == steps[0].Note, back[0].Note);
            Check("xml newline", back[1].Text == "satır1\nsatır2", back[1].Text.Replace("\n", "\\n").Replace("\r", "\\r"));
            Check("xml fields", back[0].X == 10 && back[0].Y == 20 && back[0].DelayMs == 300 && back[2].Keys == "Ctrl+C");
        }

        static POINT P(int x, int y) => new POINT { X = x, Y = y };

        static void TestRecorder()
        {
            // tıklama
            var r = new Recorder();
            var list = new List<Step>();
            r.Begin(list);
            r.OnMouse(WM_LBUTTONDOWN, P(100, 100), 0, false);
            r.OnMouse(WM_LBUTTONUP, P(102, 101), 0, false);
            Thread.Sleep(GetDoubleClickTimeSafe() + 50);
            r.OnMouse(WM_LBUTTONDOWN, P(500, 300), 0, false);
            r.OnMouse(WM_LBUTTONUP, P(500, 300), 0, false);
            r.End();
            Check("rec click count", list.Count == 2, Dump(list));
            if (list.Count == 2)
            {
                Check("rec click 1", list[0].Action == StepAction.Click && list[0].X == 100 && list[0].Y == 100, Dump(list));
                Check("rec delay real", list[1].DelayMs >= 300, list[1].DelayMs.ToString());
            }

            // sürükle
            list = new List<Step>();
            r = new Recorder();
            r.Begin(list);
            r.OnMouse(WM_LBUTTONDOWN, P(100, 100), 0, false);
            r.OnMouse(WM_MOUSEMOVE, P(200, 150), 0, false);
            r.OnMouse(WM_LBUTTONUP, P(300, 200), 0, false);
            r.End();
            Check("rec drag", list.Count == 1 && list[0].Action == StepAction.Drag && list[0].X2 == 300 && list[0].Y2 == 200, Dump(list));

            // çift tık
            list = new List<Step>();
            r = new Recorder();
            r.Begin(list);
            r.OnMouse(WM_LBUTTONDOWN, P(50, 50), 0, false);
            r.OnMouse(WM_LBUTTONUP, P(50, 50), 0, false);
            r.OnMouse(WM_LBUTTONDOWN, P(51, 50), 0, false);
            r.OnMouse(WM_LBUTTONUP, P(51, 50), 0, false);
            r.End();
            Check("rec dblclick", list.Count == 1 && list[0].Action == StepAction.DoubleClick, Dump(list));

            // Ctrl+C
            list = new List<Step>();
            r = new Recorder();
            r.Begin(list);
            r.OnKey(WM_KEYDOWN, 0xA2, false);
            r.OnKey(WM_KEYDOWN, 0x43, false);
            r.OnKey(WM_KEYUP, 0x43, false);
            r.OnKey(WM_KEYUP, 0xA2, false);
            r.End();
            Check("rec ctrl+c", list.Count == 1 && list[0].Action == StepAction.KeyCombo && list[0].Keys == "Ctrl+C", Dump(list));

            // enjekte girdiler yok sayılır
            list = new List<Step>();
            r = new Recorder();
            r.Begin(list);
            r.OnMouse(WM_LBUTTONDOWN, P(1, 1), 0, true);
            r.OnMouse(WM_LBUTTONUP, P(1, 1), 0, true);
            r.End();
            Check("rec ignores injected", list.Count == 0, Dump(list));
        }

        static int GetDoubleClickTimeSafe() => (int)Math.Min(GetDoubleClickTime(), 1000);

        static string Dump(List<Step> l)
        {
            var sb = new StringBuilder();
            foreach (var s in l) sb.Append($"{s.Action}({s.X},{s.Y}->{s.X2},{s.Y2} k={s.Keys} d={s.DelayMs}) ");
            return sb.ToString();
        }

        static void TestKeyPicker()
        {
            using (var kp = new KeyPicker())
            {
                foreach (var s in new[] { "Ctrl+C", "Ctrl+Shift+Esc", "Win+R", "F5", "Ctrl+Shift" })
                {
                    kp.Value = s;
                    Check("keypicker " + s, kp.Value == s, kp.Value);
                }
            }
        }

        static void TestPlayer()
        {
            var p = new Player();
            var done = new ManualResetEventSlim();
            p.Finished += _ => done.Set();
            var steps = new List<Step>
            {
                new Step { Action = StepAction.Wait, DelayMs = 200 },
                new Step { Action = StepAction.Wait, DelayMs = 200 },
            };
            var sw = Stopwatch.StartNew();
            p.Start(steps, new PlayOptions { Repeat = 2, SpeedPct = 200 });
            bool ok = done.Wait(5000);
            sw.Stop();
            Check("player finished", ok);
            Check("player timing ~400ms", sw.ElapsedMilliseconds >= 350 && sw.ElapsedMilliseconds <= 700, sw.ElapsedMilliseconds + "ms");
        }

        static void TestHumanizer()
        {
            var h = new Humanizer(new Random(42));
            bool inRange = true;
            double sum = 0;
            for (int i = 0; i < 2000; i++)
            {
                double v = h.Range(50, 150);
                if (v < 50 || v > 150) inRange = false;
                sum += v;
            }
            Check("human range bounds", inRange);
            Check("human range mean", Math.Abs(sum / 2000 - 100) < 5, (sum / 2000).ToString("0.0"));

            bool facOk = true;
            for (int i = 0; i < 2000; i++) { double f = h.DelayFactor(20); if (f < 0.8 || f > 1.2) facOk = false; }
            Check("human delay factor", facOk);

            bool offOk = true;
            for (int i = 0; i < 500; i++)
            {
                int x = 100, y = 100;
                h.Offset(ref x, ref y, 3);
                if ((x - 100) * (x - 100) + (y - 100) * (y - 100) > 18) offOk = false;
            }
            Check("human click offset", offOk);

            bool endOk = true, curved = false, jumpOk = true;
            for (int i = 0; i < 200; i++)
            {
                var pts = h.Path(10, 20, 800, 500, 40);
                var last = pts[pts.Count - 1];
                if (pts.Count != 40 || last.X != 800 || last.Y != 500) endOk = false;
                var mid = pts[pts.Count / 2];
                // düz çizgiden uzaklık
                double d = Math.Abs((500 - 20) * mid.X - (800 - 10) * mid.Y + 800 * 20 - 500 * 10) / Math.Sqrt(480.0 * 480 + 790 * 790);
                if (d > 5) curved = true;
                int px = 10, py = 20;
                foreach (var q in pts)
                {
                    if (Math.Abs(q.X - px) > 200 || Math.Abs(q.Y - py) > 200) jumpOk = false;
                    px = q.X; py = q.Y;
                }
            }
            Check("human path ends on target", endOk);
            Check("human path curves", curved);
            Check("human path no jumps", jumpOk);
            var one = h.Path(5, 5, 5, 5, 10);
            Check("human path zero dist", one.Count == 1 && one[0].X == 5 && one[0].Y == 5);
        }
    }
}
