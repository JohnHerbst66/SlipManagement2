
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

       
    }
}
