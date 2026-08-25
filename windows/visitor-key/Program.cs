using System;
using System.Windows.Forms;

namespace LOKWOD.VisitorKey
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length > 0)
            {
                Environment.ExitCode = CommandLine.Run(args);
                return;
            }

            Application.Run(new VisitorKeyContext());
        }
    }
}
