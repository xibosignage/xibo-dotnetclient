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
using System.Xml;
using XiboClient.XmdsAgents;

namespace XiboClient
{
    public partial class OptionForm : Form
    {
        private HardwareKey _hardwareKey;


        public OptionForm()
        {
            InitializeComponent();

            Debug.WriteLine("Initialise Option Form Components", "OptionForm");

            // Get a hardware key here, just in case we havent been able to get one before
            _hardwareKey = new HardwareKey();

            // XMDS completed event
            xmds1.RegisterDisplayCompleted += new XiboClient.xmds.RegisterDisplayCompletedEventHandler(xmds1_RegisterDisplayCompleted);
            
            // Library Path
            if (ApplicationSettings.Default.LibraryPath == "DEFAULT")
            {
                Debug.WriteLine("Getting the Library Path", "OptionForm");
                ApplicationSettings.Default.LibraryPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\Xibo Library";
                ApplicationSettings.Default.Save();
            }

            // Computer name if the display name hasnt been set yet
            if (ApplicationSettings.Default.DisplayName == "COMPUTERNAME")
            {
                Debug.WriteLine("Getting the display Name", "OptionForm");
                ApplicationSettings.Default.DisplayName = Environment.MachineName;
                ApplicationSettings.Default.Save();
            }

            // Set global proxy information
            OptionForm.SetGlobalProxy();

            // Settings Tab
            textBoxXmdsUri.Text = ApplicationSettings.Default.ServerUri;
            textBoxServerKey.Text = ApplicationSettings.Default.ServerKey;
            textBoxLibraryPath.Text = ApplicationSettings.Default.LibraryPath;
            tbHardwareKey.Text = ApplicationSettings.Default.HardwareKey;

            // Proxy Tab
            textBoxProxyUser.Text = ApplicationSettings.Default.ProxyUser;
            maskedTextBoxProxyPass.Text = ApplicationSettings.Default.ProxyPassword;
            textBoxProxyDomain.Text = ApplicationSettings.Default.ProxyDomain;

            // Appearance Tab
            splashOverride.Text = ApplicationSettings.Default.SplashOverride;

            Debug.WriteLine("Loaded Options Form", "OptionForm");
        }

        /// <summary>
        /// Register display completed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void xmds1_RegisterDisplayCompleted(object sender, XiboClient.xmds.RegisterDisplayCompletedEventArgs e)
        {
            tbStatus.ResetText();

            if (e.Error != null)
            {
                tbStatus.AppendText("Status" + Environment.NewLine);
                tbStatus.AppendText(e.Error.Message);

                Debug.WriteLine("Error returned from Call to XMDS Register Display.", "xmds1_RegisterDisplayCompleted");
                Debug.WriteLine(e.Error.Message, "xmds1_RegisterDisplayCompleted");
                Debug.WriteLine(e.Error.StackTrace, "xmds1_RegisterDisplayCompleted");
            }
            else
            {
                tbStatus.AppendText(RegisterAgent.ProcessRegisterXml(e.Result));
            }
        }

       /// <summary>
       /// Save settings
       /// </summary>
       /// <param name="sender"></param>
       /// <param name="e"></param>
        private void buttonSaveSettings_Click(object sender, EventArgs e)
        {
            tbStatus.ResetText();
            try
            {
                tbStatus.AppendText("Saving with CMS... Please wait...");

                // Make sure our XMDS location is correct
                ApplicationSettings.Default.XiboClient_xmds_xmds = textBoxXmdsUri.Text.TrimEnd('/') + @"/xmds.php?v=" + ApplicationSettings.Default.Version;
            
                // Simple settings
                ApplicationSettings.Default.ServerKey = textBoxServerKey.Text;
                ApplicationSettings.Default.LibraryPath = textBoxLibraryPath.Text.TrimEnd('\\');
                ApplicationSettings.Default.ServerUri = textBoxXmdsUri.Text;
                ApplicationSettings.Default.HardwareKey = tbHardwareKey.Text;

                // Also tweak the address of the xmds1
                xmds1.Url = ApplicationSettings.Default.XiboClient_xmds_xmds;

                // Proxy Settings
                ApplicationSettings.Default.ProxyUser = textBoxProxyUser.Text;
                ApplicationSettings.Default.ProxyPassword = maskedTextBoxProxyPass.Text;
                ApplicationSettings.Default.ProxyDomain = textBoxProxyDomain.Text;

                // Change the default Proxy class
                OptionForm.SetGlobalProxy();

                // Client settings
                ApplicationSettings.Default.SplashOverride = splashOverride.Text;

                // Commit these changes back to the user settings
                ApplicationSettings.Default.Save();

                // Call register
                xmds1.RegisterDisplayAsync(ApplicationSettings.Default.ServerKey, ApplicationSettings.Default.HardwareKey, ApplicationSettings.Default.DisplayName, "windows", ApplicationSettings.Default.ClientVersion, ApplicationSettings.Default.ClientCodeVersion, Environment.OSVersion.ToString(), _hardwareKey.MacAddress, ApplicationSettings.Default.Version);
            }
            catch (Exception ex)
            {
                tbStatus.AppendText(ex.Message);
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
                Process.Start("http://xibo.org.uk/manual");
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
                System.Diagnostics.Process.Start(ApplicationSettings.Default.ServerUri + @"/index.php?p=display");
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
            
            if (ApplicationSettings.Default.ProxyUser != "")
            {
                // disable expect100Continue
                ServicePointManager.Expect100Continue = false;

                Debug.WriteLine("Creating a network credential using the Proxy User.", "SetGlobalProxy");

                NetworkCredential nc = new NetworkCredential(ApplicationSettings.Default.ProxyUser, ApplicationSettings.Default.ProxyPassword);

                if (ApplicationSettings.Default.ProxyDomain != "") 
                    nc.Domain = ApplicationSettings.Default.ProxyDomain;

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

        private void buttonExit_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Close();
            Process.Start(Application.ExecutablePath);
        }
    }
}