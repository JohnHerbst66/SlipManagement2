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

            // Ask DatabaseManager where the data lives rather than recomputing the path here.
            // Reading this property is also what performs the one-time move out of the program
            // folder, so it has to happen before anything decides whether setup is needed --
            // otherwise an upgraded install would see no database and offer First-Time Setup
            // on top of an operator's existing slips.
            string dbPath = DatabaseManager.DbPath;

            if (!File.Exists(dbPath))
            {
                using (var setup = new FirstTimeSetupForm())
                {
                    if (setup.ShowDialog() != DialogResult.OK)
                        return; // user closed setup without completing — exit gracefully
                }
            }

            DatabaseManager.PerformStartupBackup();

            // Licence is checked after the database is ready, never before. A licence problem must
            // not stop the operator reaching records that already exist, so this only decides
            // whether NEW slips can be created -- see the note at the top of Licence.cs.
            Licence.Check();
            if (!Licence.AllowsNewRecords)
            {
                using (var lic = new LicenceForm())
                    lic.ShowDialog();   // closing it is allowed; the program opens read-only
            }

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
