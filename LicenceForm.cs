using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlipManagement2
{
    // Shown when the program is not licensed for this computer. Its job is to make the next step
    // obvious to someone who is not technical: here is your code, send it to this address, paste
    // back what comes returned.
    //
    // It can always be closed. Refusing to let the program open at all would mean an operator
    // whose motherboard failed could not reach their own slip history, and their records must
    // never be held behind a licence -- see the note at the top of Licence.cs.
    public class LicenceForm : Form
    {
        private TextBox _txtCode;
        private TextBox _txtLicence;

        public LicenceForm()
        {
            Text            = "Licence This Computer";
            Size            = new Size(760, 640);
            StartPosition   = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            BackColor       = Color.White;

            BuildUI();
        }

        private void BuildUI()
        {
            var lblTitle = new Label
            {
                Text      = "This copy is not yet licensed for this computer",
                Location  = new Point(20, 18),
                AutoSize  = true,
                Font      = new Font("Arial", 13, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 60, 110),
            };

            var lblWhy = new Label
            {
                Text = Explanation(),
                Location  = new Point(22, 52),
                Size      = new Size(700, 78),
                Font      = new Font("Arial", 9),
                ForeColor = Color.FromArgb(70, 70, 70),
            };

            // --- step 1 -----------------------------------------------------
            var lblStep1 = new Label
            {
                Text      = "1.  Send this code to " + LicenceContact,
                Location  = new Point(20, 140),
                AutoSize  = true,
                Font      = new Font("Arial", 10, FontStyle.Bold),
            };

            _txtCode = new TextBox
            {
                Text       = MachineFingerprint.MachineCode(),
                Location   = new Point(22, 166),
                Size       = new Size(560, 30),
                Font       = new Font("Consolas", 13, FontStyle.Bold),
                ReadOnly   = true,
                BackColor  = Color.FromArgb(245, 245, 235),
                TextAlign  = HorizontalAlignment.Center,
            };

            var btnCopy = new Button
            {
                Text      = "Copy",
                Location  = new Point(592, 165),
                Size      = new Size(126, 32),
                BackColor = Color.LightSteelBlue,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 9, FontStyle.Bold),
            };
            btnCopy.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(_txtCode.Text);
                    MessageBox.Show("Code copied. Paste it into an email.", "Copied",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch { }
            };

            var lblCodeNote = new Label
            {
                Text      = "This code describes this computer only. It contains nothing about your slips.",
                Location  = new Point(22, 200),
                AutoSize  = true,
                Font      = new Font("Arial", 8, FontStyle.Italic),
                ForeColor = Color.Gray,
            };

            // --- step 2 -----------------------------------------------------
            var lblStep2 = new Label
            {
                Text      = "2.  Paste the licence you are sent into the box below, then click Activate",
                Location  = new Point(20, 234),
                AutoSize  = true,
                Font      = new Font("Arial", 10, FontStyle.Bold),
            };

            _txtLicence = new TextBox
            {
                Location   = new Point(22, 260),
                Size       = new Size(696, 240),
                Multiline  = true,
                ScrollBars = ScrollBars.Vertical,
                Font       = new Font("Consolas", 9),
                WordWrap   = false,
            };

            var btnPaste = new Button
            {
                Text      = "Paste",
                Location  = new Point(22, 512),
                Size      = new Size(120, 36),
                BackColor = Color.Gainsboro,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 9, FontStyle.Bold),
            };
            btnPaste.Click += (s, e) =>
            {
                try { if (Clipboard.ContainsText()) _txtLicence.Text = Clipboard.GetText(); }
                catch { }
            };

            var btnActivate = new Button
            {
                Text      = "Activate",
                Location  = new Point(152, 512),
                Size      = new Size(160, 36),
                BackColor = Color.LightGreen,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 10, FontStyle.Bold),
            };
            btnActivate.Click += BtnActivate_Click;

            var btnLoadFile = new Button
            {
                Text      = "Open Licence File...",
                Location  = new Point(322, 512),
                Size      = new Size(180, 36),
                BackColor = Color.Gainsboro,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 9, FontStyle.Bold),
            };
            btnLoadFile.Click += BtnLoadFile_Click;

            var btnLater = new Button
            {
                Text      = "Continue Without Licence",
                Location  = new Point(512, 512),
                Size      = new Size(206, 36),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 9),
            };
            btnLater.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            var lblLater = new Label
            {
                Text = "Without a licence you can still open, search, print and export slips that already "
                     + "exist. Creating new slips is what needs the licence.",
                Location  = new Point(22, 556),
                Size      = new Size(700, 40),
                Font      = new Font("Arial", 8, FontStyle.Italic),
                ForeColor = Color.Gray,
            };

            Controls.AddRange(new Control[]
            {
                lblTitle, lblWhy,
                lblStep1, _txtCode, btnCopy, lblCodeNote,
                lblStep2, _txtLicence,
                btnPaste, btnActivate, btnLoadFile, btnLater, lblLater,
            });
        }

        public const string LicenceContact = "herbstjohn5@gmail.com";

        private static string Explanation()
        {
            switch (Licence.Status)
            {
                case Licence.State.OtherMachine:
                    return "The licence on this computer is genuine, but it was issued for a different "
                         + "machine. That usually means the program folder was copied from another PC, "
                         + "or that major hardware was replaced. Either way a new licence is needed for "
                         + "this computer, and it is free if this is a replacement machine.";
                case Licence.State.Unreadable:
                    return "There is a licence file on this computer, but it cannot be read. It may have "
                         + "been edited or only partly copied. Request a fresh one and paste it below.";
                default:
                    return "The program is licensed to one computer at one site. Send the code below and "
                         + "a licence will be returned to you. This is a one-off step and does not need "
                         + "an internet connection.";
            }
        }

        private void BtnActivate_Click(object sender, EventArgs e)
        {
            string text = _txtLicence.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("Paste the licence text into the box first.", "Nothing to Activate",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (Licence.Install(text, out string error))
            {
                MessageBox.Show(
                    "This computer is now licensed." + Environment.NewLine + Environment.NewLine +
                    "Licensed to: " + Licence.LicensedTo + Environment.NewLine +
                    (string.IsNullOrWhiteSpace(Licence.Site) ? "" : "Site: " + Licence.Site + Environment.NewLine) +
                    "Issued: " + Licence.IssuedOn,
                    "Licensed", MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            MessageBox.Show(error, "Licence Not Accepted", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void BtnLoadFile_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog
            {
                Title  = "Open the licence file you were sent",
                Filter = "Licence files (*.lic;*.txt)|*.lic;*.txt|All files (*.*)|*.*",
            })
            {
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                try { _txtLicence.Text = System.IO.File.ReadAllText(ofd.FileName); }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not read that file: " + ex.Message, "Open Failed",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }
}
