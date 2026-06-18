
using System;
using System.Windows.Forms;

namespace SlipManagement2 // ⚠️ Change this to match your exact project namespace!
{
    public partial class Main : Form
    {
        // Global sequential counter tracking variable for the automated Slip ID allocation
        public static int NextSlipId = 1;

        public Main()
        {
            InitializeComponent();
            // ⭐ Initialize our local database workspace automatically
            DatabaseManager.InitializeDatabase();
           
            // 2. ⭐ THE FIX: Determine the next true sequential ID directly from the SQLite records
            Main.NextSlipId = DatabaseManager.GetNextSequentialSlipId();

            // 3. Populate your pending tiles onto your flpSlips panel shelf
            DatabaseManager.LoadSavedSlipsToDashboard(this.flpSlips);
        }
        private void Main_Load(object sender, EventArgs e)
        {
            // Los hierdie leeg vir nou, dit maak net die fout reg!
        }



        // Open the creation input screen when the user clicks btnCreate
        private void btnCreate_Click(object sender, EventArgs e)
        {
            CreateSlip popUp = new CreateSlip();
            popUp.ShowDialog();
        }

        private void btnOpenSettings_Click(object sender, EventArgs e)
        {
           PrinterSettingsForm options = new PrinterSettingsForm();
           options.ShowDialog();

        }

        private void btnSlipHistory_Click(object sender, EventArgs e)
        
        {
            // Opens your freshly unified, error-free history vault form
            SlipsHistoryForm historyPage = new SlipsHistoryForm();
            historyPage.ShowDialog();
        }

        

        private void btnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void btnCustomizeSlips_Click(object sender, EventArgs e)
        {
            CustomizeSlipsForm options = new CustomizeSlipsForm();
            options.ShowDialog();
        }
    }
}
