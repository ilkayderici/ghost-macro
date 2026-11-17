using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace GhostMacro
{
    internal static class Program
    {
        /// <summary>Dil değişince uygulama kendini yeniden başlatır.</summary>
        public static bool RestartRequested;

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "--selftest")
                return SelfTest.Run(args[1]);

            var mx = new Mutex(false, @"Local\GhostMacro.SingleInstance");
            bool got;
            try { got = mx.WaitOne(args.Contains("--restart") ? 8000 : 0); }
            catch (AbandonedMutexException) { got = true; }
            if (!got)
            {
                MessageBox.Show("GhostMacro zaten çalışıyor.\nGhostMacro is already running.", "GhostMacro",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) =>
                    MessageBox.Show(e.Exception.Message, "GhostMacro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Run(new MainForm());
            }
            finally
            {
                mx.ReleaseMutex();
                mx.Dispose();
            }

            if (RestartRequested)
                Process.Start(Application.ExecutablePath, "--restart");
            return 0;
        }
    }
}
