using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using static GhostMacro.Native;

namespace GhostMacro
{
    /// <summary>SendInput ile fare/klavye olayı üretir.</summary>
    internal static class Input
    {
        static readonly int Size = Marshal.SizeOf(typeof(INPUT));

        static readonly HashSet<int> Extended = new HashSet<int>
        {
            0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x2C, 0x2D, 0x2E,
            0x5B, 0x5C, 0x5D, 0x6F, 0x90, 0xA3, 0xA5,
        };

        static void Send(INPUT i) => SendInput(1, new[] { i }, Size);

        public static Point CursorPos()
        {
            GetCursorPos(out var p);
            return new Point(p.X, p.Y);
        }

        public static void MoveTo(int x, int y)
        {
            int vx = GetSystemMetrics(SM_XVIRTUALSCREEN), vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int vw = Math.Max(2, GetSystemMetrics(SM_CXVIRTUALSCREEN)), vh = Math.Max(2, GetSystemMetrics(SM_CYVIRTUALSCREEN));
            var i = new INPUT { type = INPUT_MOUSE };
            i.U.mi.dx = (int)Math.Round((x - vx) * 65535.0 / (vw - 1));
            i.U.mi.dy = (int)Math.Round((y - vy) * 65535.0 / (vh - 1));
            i.U.mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK;
            Send(i);
            SetCursorPos(x, y); // yuvarlama kaymasını düzelt
        }

        public static void Button(MouseBtn b, bool down)
        {
            uint f;
            switch (b)
            {
                case MouseBtn.Right: f = down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP; break;
                case MouseBtn.Middle: f = down ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP; break;
                default: f = down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP; break;
            }
            var i = new INPUT { type = INPUT_MOUSE };
            i.U.mi.dwFlags = f;
            Send(i);
        }

        public static void Wheel(int notches)
        {
            var i = new INPUT { type = INPUT_MOUSE };
            i.U.mi.dwFlags = MOUSEEVENTF_WHEEL;
            i.U.mi.mouseData = unchecked((uint)(notches * 120));
            Send(i);
        }

        public static void Key(int vk, bool up)
        {
            var i = new INPUT { type = INPUT_KEYBOARD };
            i.U.ki.wVk = (ushort)vk;
            i.U.ki.wScan = (ushort)MapVirtualKey((uint)vk, 0);
            i.U.ki.dwFlags = (up ? KEYEVENTF_KEYUP : 0) | (Extended.Contains(vk) ? KEYEVENTF_EXTENDEDKEY : 0);
            Send(i);
        }

        public static void Char(char c, bool up)
        {
            var i = new INPUT { type = INPUT_KEYBOARD };
            i.U.ki.wVk = 0;
            i.U.ki.wScan = c;
            i.U.ki.dwFlags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0);
            Send(i);
        }
    }
}
