using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LOKWOD.VisitorKey
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                Environment.ExitCode = CommandLine.Run(args);
                return;
            }

            using var instance = new Mutex(true, @"Local\LOKWOD.VisitorKey", out bool firstInstance);
            if (!firstInstance)
            {
                AppLog.Write("A second Visitor Key instance was prevented from starting.");
                return;
            }

            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (_, eventArgs) => AppLog.Write("Unhandled Windows Forms exception.", eventArgs.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) => AppLog.Write("Unhandled application exception.", eventArgs.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
            {
                AppLog.Write("Unobserved background-task exception.", eventArgs.Exception);
                eventArgs.SetObserved();
            };

            Application.Run(new VisitorKeyContext());
        }
    }
}
