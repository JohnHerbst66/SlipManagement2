using System;
using System.IO;
using System.Windows.Forms;

namespace SlipManagement2
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string dbPath = System.IO.Path.Combine(
                System.AppDomain.CurrentDomain.BaseDirectory, "WeighbridgeData.db");

            if (!File.Exists(dbPath))
            {
                using (var setup = new FirstTimeSetupForm())
                {
                    if (setup.ShowDialog() != DialogResult.OK)
                        return; // user closed setup without completing — exit gracefully
                }
            }

            DatabaseManager.PerformStartupBackup();
            try
            {
                Application.Run(new Main());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Startup error:\n\n" + ex.GetType().Name + "\n" + ex.Message +
                    "\n\n" + ex.StackTrace,
                    "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
