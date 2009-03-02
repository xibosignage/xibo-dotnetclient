/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006,2007,2008 Daniel Garner and James Packer
 *
 * This file is part of Xibo.
 *
 * Xibo is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * any later version. 
 *
 * Xibo is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with Xibo.  If not, see <http://www.gnu.org/licenses/>.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using System.Data;
using System.Drawing;
using System.Text;
using System.Net;
using System.Windows.Forms;

namespace XiboClient
{
    public partial class OptionForm : Form
    {
        public OptionForm()
        {
            System.Diagnostics.Debug.WriteLine("[IN]", "OptionForm");
            System.Diagnostics.Debug.WriteLine("Initialise Option Form Components", "OptionForm");

            System.Diagnostics.Debug.Flush();

            InitializeComponent();

            System.Diagnostics.Debug.WriteLine("Register some Event Handlers", "OptionForm");

            this.xmds1.RegisterDisplayCompleted += new XiboClient.xmds.RegisterDisplayCompletedEventHandler(xmds1_RegisterDisplayCompleted);
            
            // Bind some events to the settings fields
            textBoxXmdsUri.TextChanged += new EventHandler(setting_TextChanged);
            textBoxServerKey.TextChanged += new EventHandler(setting_TextChanged);
            textBoxLibraryPath.TextChanged += new EventHandler(setting_TextChanged);
            numericUpDownCollect.ValueChanged += new EventHandler(setting_TextChanged);

            // Bind some events to the proxy settings fields
            textBoxProxyUser.TextChanged += new EventHandler(proxySetting_TextChanged);
            maskedTextBoxProxyPass.TextChanged += new EventHandler(proxySetting_TextChanged);
            textBoxProxyDomain.TextChanged += new EventHandler(proxySetting_TextChanged);


            // Get the hardware key
            System.Diagnostics.Debug.WriteLine("Getting the Hardware Key", "OptionForm");
            hardwareKey = new HardwareKey();

            System.Diagnostics.Debug.WriteLine("Getting the Library Path", "OptionForm");
            if (Properties.Settings.Default.LibraryPath == "DEFAULT")
            {
                Properties.Settings.Default.LibraryPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\Xibo Library";
                Properties.Settings.Default.Save();
            }

            System.Diagnostics.Debug.WriteLine("Getting the display Name", "OptionForm");
            if (Properties.Settings.Default.displayName == "COMPUTERNAME")
            {
                Properties.Settings.Default.displayName = Environment.MachineName;
                Properties.Settings.Default.Save();
            }

            System.Diagnostics.Debug.WriteLine("About to call SetGlobalProxy", "OptionForm");
            OptionForm.SetGlobalProxy();

            System.Diagnostics.Debug.WriteLine("[OUT]", "OptionForm");
        }

        void proxySetting_TextChanged(object sender, EventArgs e)
        {
            buttonProxySave.Enabled = true;
        }

        /// <summary>
        /// Fired when a setting is changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void setting_TextChanged(object sender, EventArgs e)
        {
            //Set the button to be enabled
            buttonSaveSettings.Enabled = true;
        }

        void xmds1_RegisterDisplayCompleted(object sender, XiboClient.xmds.RegisterDisplayCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                textBoxResults.Text = e.Error.Message;
            }
            else
            {
                textBoxResults.Text = e.Result;
            }
        }

        private void buttonRegister_Click(object sender, EventArgs e)
        {
            textBoxResults.Text = "Sending Request";

            Properties.Settings.Default.displayName = textBoxDisplayName.Text;
            Properties.Settings.Default.Save();

            xmds1.RegisterDisplayAsync(Properties.Settings.Default.ServerKey, hardwareKey.Key, textBoxDisplayName.Text, Properties.Settings.Default.Version);
        }

       /// <summary>
       /// Save settings
       /// </summary>
       /// <param name="sender"></param>
       /// <param name="e"></param>
        private void buttonSaveSettings_Click(object sender, EventArgs e)
        {
            try
            {
                //Suck up the settings and save them
                Properties.Settings.Default.ServerKey = textBoxServerKey.Text;
                Properties.Settings.Default.LibraryPath = textBoxLibraryPath.Text.TrimEnd('\\');
                Properties.Settings.Default.serverURI = textBoxXmdsUri.Text;
                Properties.Settings.Default.collectInterval = numericUpDownCollect.Value;
                Properties.Settings.Default.powerpointEnabled = checkBoxPowerPoint.Checked;
                Properties.Settings.Default.statsEnabled = checkBoxStats.Checked;
                Properties.Settings.Default.auditEnabled = checkBoxAudit.Checked;
                Properties.Settings.Default.XiboClient_xmds_xmds = textBoxXmdsUri.Text.TrimEnd('/') + @"/xmds.php";
                buttonSaveSettings.Enabled = false;

                //Also tweak the address of the xmds1
                this.xmds1.Url = Properties.Settings.Default.XiboClient_xmds_xmds;

                labelXmdsUrl.Text = Properties.Settings.Default.XiboClient_xmds_xmds;

                //Commit these changes back to the user settings
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private HardwareKey hardwareKey;

        private void buttonLibrary_Click(object sender, EventArgs e)
        {
            // Set the dialog
            folderBrowserLibrary.SelectedPath = textBoxLibraryPath.Text;

            // Open the dialog
            if (folderBrowserLibrary.ShowDialog() == DialogResult.OK)
            {
                textBoxLibraryPath.Text = folderBrowserLibrary.SelectedPath;
            }
        }

        private void buttonReset_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Reset to Default Settings?", "Xibo: Are you sure", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                Properties.Settings.Default.Reset();
            }

            // Make sure the special settings are delt with
            Properties.Settings.Default.LibraryPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\Xibo Library";
            Properties.Settings.Default.displayName = Environment.MachineName;
            Properties.Settings.Default.Save();
        }

        private void onlineHelpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // open URL in separate instance of default browser
            try
            {
                System.Diagnostics.Process.Start("http://www.xibo.org.uk/manual");
            }
            catch
            {
                MessageBox.Show("No web browser installed");
            }
        }

        private void aboutToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            About about = new About();
            about.ShowDialog();
        }

        private void checkBoxPowerPoint_CheckedChanged(object sender, EventArgs e)
        {
            buttonSaveSettings.Enabled = true;

            if (checkBoxPowerPoint.Checked)
            {
                // PowerPoint enabled
                // check for it installed

                // enable the IE setting?
            }

            return;
        }

        private void checkBoxStats_CheckedChanged(object sender, EventArgs e)
        {
            buttonSaveSettings.Enabled = true;
        }

        private void checkBoxAudit_CheckedChanged(object sender, EventArgs e)
        {
            buttonSaveSettings.Enabled = true;
        }

        private void buttonDisplayAdmin_Click(object sender, EventArgs e)
        {
            // open URL in separate instance of default browser
            try
            {
                System.Diagnostics.Process.Start(Properties.Settings.Default.serverURI + @"/index.php?p=display");
            }
            catch
            {
                MessageBox.Show("No web browser installed");
            }
        }

        private void buttonProxySave_Click(object sender, EventArgs e)
        {
            try
            {
                // Sucks all the info and Writes to the settings
                buttonProxySave.Enabled = false;

                // Read all the settings
                Properties.Settings.Default.ProxyUser = textBoxProxyUser.Text;
                Properties.Settings.Default.ProxyPassword = maskedTextBoxProxyPass.Text;
                Properties.Settings.Default.ProxyDomain = textBoxProxyDomain.Text;

                // Save the settings
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            // Change the default Proxy class
            OptionForm.SetGlobalProxy();
        }

        /// <summary>
        /// Sets up the global proxy
        /// </summary>
        public static void SetGlobalProxy()
        {
            System.Diagnostics.Debug.WriteLine("[IN]", "SetGlobalProxy");

            System.Diagnostics.Debug.WriteLine("Trying to detect a proxy.", "SetGlobalProxy");
            
            if (Properties.Settings.Default.ProxyUser != "")
            {
                System.Diagnostics.Debug.WriteLine("Creating a network credential using the Proxy User.", "SetGlobalProxy");
                NetworkCredential nc = new NetworkCredential(Properties.Settings.Default.ProxyUser, Properties.Settings.Default.ProxyPassword);
                if (Properties.Settings.Default.ProxyDomain != "") nc.Domain = Properties.Settings.Default.ProxyDomain;

                WebRequest.DefaultWebProxy.Credentials = nc;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No Proxy.", "SetGlobalProxy");
                WebRequest.DefaultWebProxy.Credentials = null;
            }

            // What if the URL for XMDS has a SSL certificate?
            ServicePointManager.ServerCertificateValidationCallback += delegate(object sender, X509Certificate certificate, X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
            {
                System.Diagnostics.Debug.WriteLine("[IN]", "ServerCertificateValidationCallback");
                bool validationResult = false;

                System.Diagnostics.Debug.WriteLine(certificate.Subject);
                System.Diagnostics.Debug.WriteLine(certificate.Issuer);

                if (sslPolicyErrors != System.Net.Security.SslPolicyErrors.None)
                {
                    System.Diagnostics.Debug.WriteLine(sslPolicyErrors.ToString());
                }

                validationResult = true;

                System.Diagnostics.Debug.WriteLine("[OUT]", "ServerCertificateValidationCallback");
                return validationResult;
            };

            System.Diagnostics.Debug.WriteLine("[OUT]", "SetGlobalProxy");

            return;
        }
    }
}