using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace GhostMacro
{
    internal sealed class PlayOptions
    {
        public int Repeat = 1;       // 0 = sonsuz
        public int RepeatGapMs;
        public int SpeedPct = 100;
        public int JitterPct;
        public int SmoothMs;
        public bool Human;           // insan gibi oynat
        public int StartIndex;
    }

    /// <summary>Adımları arka plan iş parçacığında oynatır.</summary>
    internal sealed class Player
    {
        public volatile int CurrentIndex = -1;
        public volatile int CurrentLoop;
        public volatile int LastX, LastY;
        public event Action<string> Finished;

        volatile bool running;
        public bool IsRunning => running;

        CancellationTokenSource cts;
        CancellationToken ct;
        PlayOptions opt;
        Stopwatch sw;
        double deadline;
        readonly Random rnd = new Random();
        readonly Humanizer hum;
        string stopReason;
        readonly HashSet<int> heldKeys = new HashSet<int>();
        readonly HashSet<MouseBtn> heldButtons = new HashSet<MouseBtn>();

        public Player() { hum = new Humanizer(rnd); }

        bool H => opt.Human;

        /// <summary>İnsan modunda [min,max] arası, değilse sabit süre.</summary>
        double T(double fixedMs, double min, double max) => H ? hum.Range(min, max) : fixedMs;

        /// <summary>İptal edilebilir kısa bekleme.</summary>
        void Nap(double ms)
        {
            if (ms <= 0) return;
            if (ms < 25) { Thread.Sleep((int)Math.Round(ms)); return; }
            if (ct.WaitHandle.WaitOne((int)Math.Round(ms))) throw new OperationCanceledException();
        }

        public void Start(List<Step> steps, PlayOptions o)
        {
            if (running) return;
            var list = steps.Select(s => s.Clone()).ToList();
            opt = o;
            cts = new CancellationTokenSource();
            ct = cts.Token;
            stopReason = null;
            var p = Input.CursorPos();
            LastX = p.X; LastY = p.Y;
            CurrentIndex = -1;
            CurrentLoop = 1;
            running = true;
            new Thread(() => Run(list)) { IsBackground = true, Name = "Player" }.Start();
        }

        public void Stop(string reason = null)
        {
            if (!running) return;
            if (stopReason == null) stopReason = reason ?? L.T("Durduruldu.", "Stopped.");
            try { cts?.Cancel(); } catch { }
        }

        void Run(List<Step> list)
        {
            string result;
            Native.timeBeginPeriod(1);
            try
            {
                sw = Stopwatch.StartNew();
                deadline = 0;
                for (int loop = 1; opt.Repeat <= 0 || loop <= opt.Repeat; loop++)
                {
                    CurrentLoop = loop;
                    for (int i = loop == 1 ? opt.StartIndex : 0; i < list.Count; i++)
                    {
                        CurrentIndex = i;
                        Exec(list[i]);
                    }
                    if (opt.Repeat <= 0 || loop < opt.Repeat)
                        Wait(Math.Max(1, Scale(opt.RepeatGapMs)));
                }
                result = L.T("Tamamlandı.", "Finished.");
            }
            catch (OperationCanceledException)
            {
                result = stopReason ?? L.T("Durduruldu.", "Stopped.");
            }
            catch (Exception ex)
            {
                result = L.T("Hata: ", "Error: ") + ex.Message;
            }
            finally
            {
                ReleaseHeld();
                Native.timeEndPeriod(1);
                running = false;
            }
            Finished?.Invoke(result);
        }

        void ReleaseHeld()
        {
            foreach (var vk in heldKeys.Reverse()) { try { Input.Key(vk, true); } catch { } }
            foreach (var b in heldButtons) { try { Input.Button(b, false); } catch { } }
            heldKeys.Clear();
            heldButtons.Clear();
        }

        double Scale(int ms)
        {
            double d = ms * 100.0 / Math.Max(1, opt.SpeedPct);
            if (H) d *= hum.DelayFactor(Math.Max(opt.JitterPct, 15));
            else if (opt.JitterPct > 0) d *= 1 + (rnd.NextDouble() * 2 - 1) * opt.JitterPct / 100.0;
            return Math.Max(0, d);
        }

        double Now => sw.Elapsed.TotalMilliseconds;

        /// <summary>Zaman çizelgesine göre bekler; küçük gecikme taşmalarını telafi eder.</summary>
        void Wait(double ms)
        {
            deadline = Math.Max(deadline, Now - 5) + Math.Max(0, ms);
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                double rem = deadline - Now;
                if (rem <= 0) break;
                if (rem > 2) ct.WaitHandle.WaitOne((int)Math.Min(rem - 1, 100));
                else Thread.Sleep(0);
            }
            ct.ThrowIfCancellationRequested();
        }

        void MoveTo(int x, int y)
        {
            LastX = x; LastY = y;
            Input.MoveTo(x, y);
        }

        void Glide(int x, int y, double ms)
        {
            var p = Input.CursorPos();
            double sx = p.X, sy = p.Y;
            int n = Math.Max(1, (int)(ms / 10));
            if (H)
            {
                foreach (var q in hum.Path(sx, sy, x, y, n)) { Wait(ms / n); MoveTo(q.X, q.Y); }
                return;
            }
            for (int k = 1; k <= n; k++)
            {
                Wait(ms / n);
                double t = (double)k / n;
                double e = t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;
                MoveTo((int)Math.Round(sx + (x - sx) * e), (int)Math.Round(sy + (y - sy) * e));
            }
        }

        void Down(MouseBtn b) { heldButtons.Add(b); Input.Button(b, true); }
        void Up(MouseBtn b) { Input.Button(b, false); heldButtons.Remove(b); }

        void Click(MouseBtn b)
        {
            Down(b);
            Nap(T(30, 55, 130));
            Up(b);
        }

        void Tap(int vk)
        {
            Input.Key(vk, false);
            Nap(T(10, 45, 110));
            Input.Key(vk, true);
        }

        void Exec(Step s)
        {
            double delay = Scale(s.DelayMs);
            bool positional = Step.HasPosition(s.Action) && !(s.AtCursor && Step.AllowsAtCursor(s.Action));
            // tam kayıttaki sık fare hareketleri olduğu gibi oynatılır
            bool rawMove = s.Action == StepAction.Move && s.DelayMs < 100;
            int tx = s.X, ty = s.Y;
            if (H && positional && !rawMove && s.Action != StepAction.MouseUp) hum.Offset(ref tx, ref ty, 3);

            double glide = 0;
            if (positional && !rawMove)
            {
                if (opt.SmoothMs > 0) glide = Math.Min(opt.SmoothMs, delay);
                else if (H)
                {
                    var c = Input.CursorPos();
                    double dist = Math.Sqrt((double)(tx - c.X) * (tx - c.X) + (double)(ty - c.Y) * (ty - c.Y));
                    if (dist >= 2) glide = Math.Min(Humanizer.MoveTime(dist), delay);
                }
            }

            if (H && !rawMove) delay += hum.Hesitation();
            Wait(delay - glide);

            if (positional)
            {
                if (glide > 0) Glide(tx, ty, glide);
                else MoveTo(tx, ty);
                if (s.Action != StepAction.Move) Nap(T(15, 20, 70));
            }

            switch (s.Action)
            {
                case StepAction.Click:
                    Click(s.Button);
                    break;
                case StepAction.DoubleClick:
                    Click(s.Button);
                    Nap(T(40, 60, 120));
                    Click(s.Button);
                    break;
                case StepAction.MouseDown:
                    Down(s.Button);
                    break;
                case StepAction.MouseUp:
                    Up(s.Button);
                    break;
                case StepAction.Drag:
                    Down(s.Button);
                    Nap(T(40, 60, 140));
                    Glide(s.X2, s.Y2, Math.Max(30, s.Amount * 100.0 / Math.Max(1, opt.SpeedPct)));
                    Nap(T(40, 50, 120));
                    Up(s.Button);
                    break;
                case StepAction.Wheel:
                    Input.Wheel(s.Amount);
                    break;
                case StepAction.KeyCombo:
                {
                    var vks = KeyNames.Parse(s.Keys);
                    foreach (var vk in vks) { heldKeys.Add(vk); Input.Key(vk, false); Nap(T(12, 25, 70)); }
                    Nap(T(25, 45, 110));
                    for (int k = vks.Count - 1; k >= 0; k--) { Input.Key(vks[k], true); heldKeys.Remove(vks[k]); Nap(T(8, 15, 50)); }
                    break;
                }
                case StepAction.KeyDown:
                    foreach (var vk in KeyNames.Parse(s.Keys)) { heldKeys.Add(vk); Input.Key(vk, false); Thread.Sleep(5); }
                    break;
                case StepAction.KeyUp:
                {
                    var vks = KeyNames.Parse(s.Keys);
                    for (int k = vks.Count - 1; k >= 0; k--) { Input.Key(vks[k], true); heldKeys.Remove(vks[k]); Thread.Sleep(5); }
                    break;
                }
                case StepAction.TypeText:
                    foreach (char c in s.Text ?? "")
                    {
                        ct.ThrowIfCancellationRequested();
                        if (c == '\r') continue;
                        if (c == '\n') Tap(0x0D);
                        else if (c == '\t') Tap(0x09);
                        else if (H) { Input.Char(c, false); Nap(hum.Range(35, 95)); Input.Char(c, true); }
                        else { Input.Char(c, false); Input.Char(c, true); }
                        Nap(T(10, 40, 180));
                    }
                    break;
            }
        }
    }
}
