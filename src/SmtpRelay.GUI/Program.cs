using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace SmtpRelay.GUI
{
    internal static class Program
    {
        // Hard-coded to match Installed Apps version
        public static string AppVersion => "1.4";

        // Where the service writes its logs
        public static string GetServiceLogDirectory()
        {
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "SMTP Relay", "service", "logs");
            return baseDir;
        }

        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unhandled exception starting GUI:\n\n{ex}",
                    "Startup Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
    }
}
