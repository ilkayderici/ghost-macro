using System;
using System.Collections.Generic;
using System.Diagnostics;
using static GhostMacro.Native;

namespace GhostMacro
{
    /// <summary>
    /// Kanca olaylarını adımlara çevirir.
    /// Tıklama modu: sadece tıklama / sürükleme / tekerlek / tuş kombinasyonları (fare yolu yok).
    /// Tam kayıt: her fare hareketi, bas/bırak ve tuş bas/bırak gerçek zamanlamayla.
    /// </summary>
    internal sealed class Recorder
    {
        public bool FullMode;
        public bool RecordKeys = true;
        public bool RealDelays = true;
        public int FixedDelay = 500;
        public Func<POINT, bool> IsOwnWindow = _ => false;
        public Func<int, bool> IsHotkey = _ => false;

        List<Step> target;
        int startIndex;
        Stopwatch sw;
        long lastT;

        bool hasDown;
        MouseBtn downBtn;
        int downX, downY, downDelay;
        long downT;
        readonly HashSet<MouseBtn> ignoredDown = new HashSet<MouseBtn>();
        long lastClickUpT, lastWheelT, lastMoveT;
        int wheelAccum;
        readonly HashSet<int> keysDown = new HashSet<int>();
        bool winAlone;

        public bool Active => sw != null;

        public void Begin(List<Step> list)
        {
            target = list;
            startIndex = list.Count;
            sw = Stopwatch.StartNew();
            lastT = 0;
            hasDown = false;
            ignoredDown.Clear();
            lastClickUpT = lastWheelT = lastMoveT = long.MinValue / 4;
            wheelAccum = 0;
            keysDown.Clear();
            winAlone = false;
        }

        public void End()
        {
            sw = null;
            target = null;
        }

        int TakeDelay(long t)
        {
            int d = (int)Math.Max(0, t - lastT);
            lastT = t;
            return FullMode || RealDelays ? d : FixedDelay;
        }

        Step Last => target.Count > startIndex ? target[target.Count - 1] : null;

        static MouseBtn BtnOf(int msg)
        {
            switch (msg)
            {
                case WM_RBUTTONDOWN: case WM_RBUTTONUP: return MouseBtn.Right;
                case WM_MBUTTONDOWN: case WM_MBUTTONUP: return MouseBtn.Middle;
                default: return MouseBtn.Left;
            }
        }

        public void OnMouse(int msg, POINT pt, int wheel, bool injected)
        {
            if (sw == null || injected) return;
            long t = sw.ElapsedMilliseconds;

            switch (msg)
            {
                case WM_MOUSEMOVE:
                    if (FullMode && t - lastMoveT >= 10)
                    {
                        lastMoveT = t;
                        target.Add(new Step { Action = StepAction.Move, X = pt.X, Y = pt.Y, DelayMs = TakeDelay(t) });
                    }
                    break;

                case WM_LBUTTONDOWN:
                case WM_RBUTTONDOWN:
                case WM_MBUTTONDOWN:
                {
                    var b = BtnOf(msg);
                    if (IsOwnWindow(pt)) { ignoredDown.Add(b); return; }
                    if (FullMode)
                    {
                        target.Add(new Step { Action = StepAction.MouseDown, Button = b, X = pt.X, Y = pt.Y, DelayMs = TakeDelay(t) });
                    }
                    else
                    {
                        hasDown = true; downBtn = b; downX = pt.X; downY = pt.Y; downT = t;
                        downDelay = TakeDelay(t);
                    }
                    break;
                }

                case WM_LBUTTONUP:
                case WM_RBUTTONUP:
                case WM_MBUTTONUP:
                {
                    var b = BtnOf(msg);
                    if (ignoredDown.Remove(b)) return;
                    if (FullMode)
                        target.Add(new Step { Action = StepAction.MouseUp, Button = b, X = pt.X, Y = pt.Y, DelayMs = TakeDelay(t) });
                    else if (hasDown && downBtn == b)
                    {
                        hasDown = false;
                        FinishClick(b, pt, t);
                    }
                    break;
                }

                case WM_MOUSEWHEEL:
                {
                    if (IsOwnWindow(pt)) return;
                    wheelAccum += wheel;
                    int n = wheelAccum / 120;
                    if (n == 0) return;
                    wheelAccum -= n * 120;
                    var last = Last;
                    if (!FullMode && last != null && last.Action == StepAction.Wheel &&
                        Math.Abs(last.X - pt.X) <= 5 && Math.Abs(last.Y - pt.Y) <= 5 && t - lastWheelT < 400)
                    {
                        last.Amount += n;
                        lastT = t;
                    }
                    else
                    {
                        target.Add(new Step { Action = StepAction.Wheel, X = pt.X, Y = pt.Y, Amount = n, DelayMs = TakeDelay(t) });
                    }
                    lastWheelT = t;
                    break;
                }
            }
        }

        void FinishClick(MouseBtn b, POINT up, long t)
        {
            int dist = Math.Max(Math.Abs(up.X - downX), Math.Abs(up.Y - downY));
            if (dist > 6)
            {
                target.Add(new Step
                {
                    Action = StepAction.Drag, Button = b, X = downX, Y = downY, X2 = up.X, Y2 = up.Y,
                    Amount = (int)Math.Min(10000, Math.Max(50, t - downT)), DelayMs = downDelay,
                });
                lastT = t;
                lastClickUpT = long.MinValue / 4;
                return;
            }

            var last = Last;
            if (last != null && last.Action == StepAction.Click && last.Button == b &&
                Math.Abs(last.X - downX) <= 4 && Math.Abs(last.Y - downY) <= 4 &&
                downT - lastClickUpT <= GetDoubleClickTime())
            {
                last.Action = StepAction.DoubleClick;
                lastClickUpT = long.MinValue / 4;
                return;
            }

            target.Add(new Step { Action = StepAction.Click, Button = b, X = downX, Y = downY, DelayMs = downDelay });
            lastClickUpT = t;
        }

        public void OnKey(int msg, int vk, bool injected)
        {
            if (sw == null || injected || !RecordKeys) return;
            if (IsHotkey(vk)) return;
            bool down = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
            long t = sw.ElapsedMilliseconds;

            if (FullMode)
            {
                if (down) keysDown.Add(vk); else keysDown.Remove(vk);
                target.Add(new Step
                {
                    Action = down ? StepAction.KeyDown : StepAction.KeyUp,
                    Keys = KeyNames.KeyName(vk),
                    DelayMs = TakeDelay(t),
                });
                return;
            }

            int group = KeyNames.ModGroup(vk);
            if (down)
            {
                if (keysDown.Contains(vk)) return; // otomatik tekrar
                keysDown.Add(vk);
                if (group != 0)
                {
                    winAlone = group == KeyNames.VK_LWIN && keysDown.Count == 1;
                    return;
                }
                winAlone = false;
                var held = new List<int>(keysDown);
                held.Remove(vk);
                target.Add(new Step { Action = StepAction.KeyCombo, Keys = KeyNames.Combo(held, vk), DelayMs = TakeDelay(t) });
            }
            else
            {
                keysDown.Remove(vk);
                if (group == KeyNames.VK_LWIN && winAlone)
                {
                    winAlone = false;
                    target.Add(new Step { Action = StepAction.KeyCombo, Keys = "Win", DelayMs = TakeDelay(t) });
                }
            }
        }
    }
}
