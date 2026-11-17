using System;
using System.Runtime.InteropServices;

namespace GhostMacro
{
    /// <summary>Sistem geneli düşük seviye fare/klavye kancaları (WH_MOUSE_LL / WH_KEYBOARD_LL).</summary>
    internal sealed class InputHooks : IDisposable
    {
        public delegate void MouseHandler(int msg, Native.POINT pt, int wheelDelta, bool injected);
        /// <summary>true dönerse tuş yutulur (diğer uygulamalara gitmez).</summary>
        public delegate bool KeyHandler(int msg, int vk, bool injected);

        public event MouseHandler Mouse;
        public event KeyHandler Key;

        readonly Native.HookProc mouseProc, keyProc; // GC'den korumak için alan olarak tutulur
        IntPtr mouseHook = IntPtr.Zero, keyHook = IntPtr.Zero;

        public InputHooks()
        {
            mouseProc = MouseCallback;
            keyProc = KeyCallback;
        }

        public bool Installed => mouseHook != IntPtr.Zero;

        public void Install()
        {
            if (Installed) return;
            IntPtr h = Native.GetModuleHandle(null);
            mouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, mouseProc, h, 0);
            keyHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, keyProc, h, 0);
        }

        public void Uninstall()
        {
            if (mouseHook != IntPtr.Zero) Native.UnhookWindowsHookEx(mouseHook);
            if (keyHook != IntPtr.Zero) Native.UnhookWindowsHookEx(keyHook);
            mouseHook = keyHook = IntPtr.Zero;
        }

        IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && Mouse != null)
            {
                try
                {
                    var s = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
                    Mouse((int)wParam, s.pt, (short)((s.mouseData >> 16) & 0xFFFF), (s.flags & Native.LLMHF_INJECTED) != 0);
                }
                catch { }
            }
            return Native.CallNextHookEx(mouseHook, nCode, wParam, lParam);
        }

        IntPtr KeyCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && Key != null)
            {
                try
                {
                    var s = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
                    if (Key((int)wParam, (int)s.vkCode, (s.flags & Native.LLKHF_INJECTED) != 0))
                        return (IntPtr)1;
                }
                catch { }
            }
            return Native.CallNextHookEx(keyHook, nCode, wParam, lParam);
        }

        public void Dispose() => Uninstall();
    }
}
