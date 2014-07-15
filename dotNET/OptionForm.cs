/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006-2014 Daniel Garner
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
using XiboClient.Properties;
using System.Diagnostics;

namespace XiboClient
{
    public partial class OptionForm : Form
    {
        private HardwareKey _hardwareKey;


        public OptionForm()
        {
            InitializeComponent();

            System.Diagnostics.Debug.WriteLine("Initialise Option Form Components", "OptionForm");

            // Get a hardware key here, just in case we havent been able to get one before
            _hardwareKey = new HardwareKey();

            // XMDS completed event
            xmds1.RegisterDisplayCompleted += new XiboClient.xmds.RegisterDisplayCompletedEventHandler(xmds1_RegisterDisplayCompleted);
            
            // Library Path
            if (Properties.Settings.Default.LibraryPath == "DEFAULT")
            {
                Debug.WriteLine("Getting the Library Path", "OptionForm");
                Properties.Settings.Default.LibraryPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\Xibo Library";
                Properties.Settings.Default.Save();
            }

            // Computer name if the display name hasnt been set yet
            if (Settings.Default.displayName == "COMPUTERNAME")
            {
                Debug.WriteLine("Getting the display Name", "OptionForm");
                Settings.Default.displayName = Environment.MachineName;
                Settings.Default.Save();
            }

            // Set global proxy information
            OptionForm.SetGlobalProxy();

            // Settings Tab
            textBoxXmdsUri.Text = Settings.Default.serverURI;
            textBoxServerKey.Text = Settings.Default.ServerKey;
            textBoxLibraryPath.Text = Settings.Default.LibraryPath;
            tbHardwareKey.Text = Settings.Default.hardwareKey;
            numericUpDownCollect.Value = Settings.Default.collectInterval;
            checkBoxPowerPoint.Checked = Settings.Default.powerpointEnabled;
            checkBoxStats.Checked = Settings.Default.statsEnabled;
            nupScrollStepAmount.Value = Settings.Default.scrollStepAmount;

            // Register Tab
            labelXmdsUrl.Text = Settings.Default.XiboClient_xmds_xmds;
            textBoxDisplayName.Text = Settings.Default.displayName;

            // Proxy Tab
            textBoxProxyUser.Text = Settings.Default.ProxyUser;
            maskedTextBoxProxyPass.Text = Settings.Default.ProxyPassword;
            textBoxProxyDomain.Text = Settings.Default.ProxyDomain;

            // Appearance Tab
            clientWidth.Value = Settings.Default.sizeX;
            clientHeight.Value = Settings.Default.sizeY;
            offsetX.Value = Settings.Default.offsetX;
            offsetY.Value = Settings.Default.offsetY;
            cbExpireModifiedLayouts.Checked = Settings.Default.expireModifiedLayouts;
            enableMouseCb.Checked = Settings.Default.EnableMouse;
            splashOverride.Text = Settings.Default.SplashOverride;

            // Advanced Tab
            numericUpDownEmptyRegions.Value = Settings.Default.emptyLayoutDuration;
            doubleBufferingCheckBox.Checked = Settings.Default.DoubleBuffering;
            enableShellCommandsCb.Checked = Settings.Default.EnableShellCommands;
            shellCommandAllowList.Text = Settings.Default.ShellCommandAllowList;
            logLevel.Text = Settings.Default.LogLevel;
            maxConcurrentDownloads.Value = Settings.Default.MaxConcurrentDownloads;
            logToDiskLocation.Text = Settings.Default.LogToDiskLocation;
            showInTaskbar.Checked = Settings.Default.ShowInTaskbar;
            cursorStartPosition.Text = Settings.Default.CursorStartPosition;
            clientInfoHotKeyTextBox.Text = Settings.Default.ClientInformationKeyCode;
            clientInfoCtrlModifierCheckBox.Checked = Settings.Default.ClientInfomationCtrlKey;

            Debug.WriteLine("Loaded Options Form", "OptionForm");
        }

        /// <summary>
        /// Register display completed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void xmds1_RegisterDisplayCompleted(object sender, XiboClient.xmds.RegisterDisplayCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                textBoxResults.Text = e.Error.Message;

                System.Diagnostics.Debug.WriteLine("Error returned from Call to XMDS Register Display.", "xmds1_RegisterDisplayCompleted");
                System.Diagnostics.Debug.WriteLine(e.Error.Message, "xmds1_RegisterDisplayCompleted");
                System.Diagnostics.Debug.WriteLine(e.Error.StackTrace, "xmds1_RegisterDisplayCompleted");
            }
            else
            {
                textBoxResults.Text = e.Result;
            }
        }

        /// <summary>
        /// Register display clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonRegister_Click(object sender, EventArgs e)
        {
            // Make a new hardware key just in case we have changed it in the form.
            _hardwareKey = new HardwareKey();

            textBoxResults.Text = "Sending Request";

            Settings.Default.XiboClient_xmds_xmds = textBoxXmdsUri.Text.TrimEnd('/') + @"/xmds.php";
            xmds1.Url = Settings.Default.XiboClient_xmds_xmds;

            Properties.Settings.Default.displayName = textBoxDisplayName.Text;
            Properties.Settings.Default.Save();

            xmds1.RegisterDisplayAsync(Properties.Settings.Default.ServerKey, _hardwareKey.Key, textBoxDisplayName.Text, Properties.Settings.Default.Version);
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
                // Simple settings
                Settings.Default.ServerKey = textBoxServerKey.Text;
                Settings.Default.LibraryPath = textBoxLibraryPath.Text.TrimEnd('\\');
                Settings.Default.serverURI = textBoxXmdsUri.Text;
                Settings.Default.collectInterval = numericUpDownCollect.Value;
                Settings.Default.powerpointEnabled = checkBoxPowerPoint.Checked;
                Settings.Default.statsEnabled = checkBoxStats.Checked;
                Settings.Default.XiboClient_xmds_xmds = textBoxXmdsUri.Text.TrimEnd('/') + @"/xmds.php";
                Settings.Default.hardwareKey = tbHardwareKey.Text;
                Settings.Default.scrollStepAmount = nupScrollStepAmount.Value;
                Settings.Default.EnableMouse = enableMouseCb.Checked;
                Settings.Default.DoubleBuffering = doubleBufferingCheckBox.Checked;

                // Also tweak the address of the xmds1
                xmds1.Url = Properties.Settings.Default.XiboClient_xmds_xmds;
                labelXmdsUrl.Text = Properties.Settings.Default.XiboClient_xmds_xmds;

                // Proxy Settings
                Settings.Default.ProxyUser = textBoxProxyUser.Text;
                Settings.Default.ProxyPassword = maskedTextBoxProxyPass.Text;
                Settings.Default.ProxyDomain = textBoxProxyDomain.Text;

                // Change the default Proxy class
                OptionForm.SetGlobalProxy();

                // Client settings
                Settings.Default.sizeX = clientWidth.Value;
                Settings.Default.sizeY = clientHeight.Value;
                Settings.Default.offsetX = offsetX.Value;
                Settings.Default.offsetY = offsetY.Value;
                Settings.Default.SplashOverride = splashOverride.Text;

                // Advanced settings
                Settings.Default.expireModifiedLayouts = cbExpireModifiedLayouts.Checked;
                Settings.Default.emptyLayoutDuration = numericUpDownEmptyRegions.Value;
                Settings.Default.EnableShellCommands = enableShellCommandsCb.Checked;
                Settings.Default.ShellCommandAllowList = shellCommandAllowList.Text;
                Settings.Default.MaxConcurrentDownloads = Convert.ToInt32(maxConcurrentDownloads.Value);
                Settings.Default.LogLevel = logLevel.Text;
                Settings.Default.LogToDiskLocation = logToDiskLocation.Text;
                Settings.Default.ShowInTaskbar = showInTaskbar.Checked;
                Settings.Default.CursorStartPosition = cursorStartPosition.Text;
                Settings.Default.ClientInformationKeyCode = clientInfoHotKeyTextBox.Text;
                Settings.Default.ClientInfomationCtrlKey = clientInfoCtrlModifierCheckBox.Checked;

                // Commit these changes back to the user settings
                Settings.Default.Save();
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

        private void splashButtonBrowse_Click(object sender, EventArgs e)
        {
            // Set the dialog
            splashScreenOverride.FileName = splashOverride.Text;

            if (splashScreenOverride.ShowDialog() == DialogResult.OK)
            {
                splashOverride.Text = splashScreenOverride.FileName;
            }
        }

        private void onlineHelpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // open URL in separate instance of default browser
            try
            {
                System.Diagnostics.Process.Start("http://xibo.org.uk/manual");
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

        /// <summary>
        /// Sets up the global proxy
        /// </summary>
        public static void SetGlobalProxy()
        {
            Debug.WriteLine("[IN]", "SetGlobalProxy");

            Debug.WriteLine("Trying to detect a proxy.", "SetGlobalProxy");
            
            if (Properties.Settings.Default.ProxyUser != "")
            {
                // disable expect100Continue
                ServicePointManager.Expect100Continue = false;

                Debug.WriteLine("Creating a network credential using the Proxy User.", "SetGlobalProxy");

                NetworkCredential nc = new NetworkCredential(Settings.Default.ProxyUser, Settings.Default.ProxyPassword);

                if (Properties.Settings.Default.ProxyDomain != "") 
                    nc.Domain = Properties.Settings.Default.ProxyDomain;

                WebRequest.DefaultWebProxy.Credentials = nc;
            }
            else
            {
                Debug.WriteLine("No Proxy.", "SetGlobalProxy");
                WebRequest.DefaultWebProxy.Credentials = null;
            }

            // What if the URL for XMDS has a SSL certificate?
            ServicePointManager.ServerCertificateValidationCallback += delegate(object sender, X509Certificate certificate, X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
            {
                Debug.WriteLine("[IN]", "ServerCertificateValidationCallback");
                bool validationResult = false;

                Debug.WriteLine(certificate.Subject);
                Debug.WriteLine(certificate.Issuer);

                if (sslPolicyErrors != System.Net.Security.SslPolicyErrors.None)
                {
                    Debug.WriteLine(sslPolicyErrors.ToString());
                }

                validationResult = true;

                Debug.WriteLine("[OUT]", "ServerCertificateValidationCallback");
                return validationResult;
            };

            Debug.WriteLine("[OUT]", "SetGlobalProxy");

            return;
        }
    }
}