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
        string stopReason;
        readonly HashSet<int> heldKeys = new HashSet<int>();
        readonly HashSet<MouseBtn> heldButtons = new HashSet<MouseBtn>();

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
            if (opt.JitterPct > 0) d *= 1 + (rnd.NextDouble() * 2 - 1) * opt.JitterPct / 100.0;
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
            Thread.Sleep(30);
            Up(b);
        }

        void Tap(int vk)
        {
            Input.Key(vk, false);
            Thread.Sleep(10);
            Input.Key(vk, true);
        }

        void Exec(Step s)
        {
            double delay = Scale(s.DelayMs);
            bool positional = Step.HasPosition(s.Action) && !(s.AtCursor && Step.AllowsAtCursor(s.Action));
            double glide = 0;
            if (positional && opt.SmoothMs > 0 && !(s.Action == StepAction.Move && s.DelayMs < 100))
                glide = Math.Min(opt.SmoothMs, delay);

            Wait(delay - glide);

            if (positional)
            {
                if (glide > 0) Glide(s.X, s.Y, glide);
                else MoveTo(s.X, s.Y);
                if (s.Action != StepAction.Move) Thread.Sleep(15);
            }

            switch (s.Action)
            {
                case StepAction.Click:
                    Click(s.Button);
                    break;
                case StepAction.DoubleClick:
                    Click(s.Button);
                    Thread.Sleep(40);
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
                    Thread.Sleep(40);
                    Glide(s.X2, s.Y2, Math.Max(30, s.Amount * 100.0 / Math.Max(1, opt.SpeedPct)));
                    Thread.Sleep(40);
                    Up(s.Button);
                    break;
                case StepAction.Wheel:
                    Input.Wheel(s.Amount);
                    break;
                case StepAction.KeyCombo:
                {
                    var vks = KeyNames.Parse(s.Keys);
                    foreach (var vk in vks) { heldKeys.Add(vk); Input.Key(vk, false); Thread.Sleep(12); }
                    Thread.Sleep(25);
                    for (int k = vks.Count - 1; k >= 0; k--) { Input.Key(vks[k], true); heldKeys.Remove(vks[k]); Thread.Sleep(8); }
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
                        else { Input.Char(c, false); Input.Char(c, true); }
                        Thread.Sleep(10);
                    }
                    break;
            }
        }
    }
}
