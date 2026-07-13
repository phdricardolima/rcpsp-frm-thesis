using System;
using WinFormsApplication = System.Windows.Forms.Application;

namespace RCPSP.Standalone
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            WinFormsApplication.EnableVisualStyles();
            WinFormsApplication.SetCompatibleTextRenderingDefault(false);

            string initialFile = args != null && args.Length > 0 ? args[0] : null;
            WinFormsApplication.Run(new FormSingleFileBaseline(initialFile));
        }
    }
}
