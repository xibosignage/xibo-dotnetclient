/**
 * Copyright (C) 2020 Xibo Signage Ltd
 *
 * Xibo - Digital Signage - http://www.xibo.org.uk
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
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using XiboClient.XmdsAgents;

namespace XiboClient
{
    /// <summary>
    /// Interaction logic for OptionsForm.xaml
    /// </summary>
    public partial class OptionsForm : Window
    {
        private xmds.xmds xmds;

        private HardwareKey hardwareKey;

        public OptionsForm()
        {
            InitializeComponent();

            // Create a new XMDS
            this.xmds = new xmds.xmds();
            this.xmds.RegisterDisplayCompleted += Xmds_RegisterDisplayCompleted;

            // Create a Hardware key
            this.hardwareKey = new HardwareKey();

            // Set the fields up with the current settings
            // Settings Tab
            textBoxCmsAddress.Text = ApplicationSettings.Default.ServerUri;
            textBoxCmsKey.Text = ApplicationSettings.Default.ServerKey;
            textBoxLibraryPath.Text = ApplicationSettings.Default.LibraryPath;
            textBoxHardwareKey.Text = ApplicationSettings.Default.HardwareKey;

            // Proxy Tab
            textBoxProxyUser.Text = ApplicationSettings.Default.ProxyUser;
            textBoxProxyPass.Password = ApplicationSettings.Default.ProxyPassword;
            textBoxProxyDomain.Text = ApplicationSettings.Default.ProxyDomain;

            // Appearance Tab
            textBoxSplashScreenReplacement.Text = ApplicationSettings.Default.SplashOverride;

            // Switch to TLS 2.1
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            // Populate the About tab
            textBoxLicence.AppendText(Properties.Resources.licence);
            labelPlayerVersion.Content = ApplicationSettings.Default.ClientVersion + " R" + ApplicationSettings.Default.ClientCodeVersion;
        }

        /// <summary>
        /// Sets up the global proxy
        /// </summary>
        public static void SetGlobalProxy()
        {
            Debug.WriteLine("[IN] Trying to detect a proxy.", "SetGlobalProxy");

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
            ServicePointManager.ServerCertificateValidationCallback += delegate (object sender, X509Certificate certificate, X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Button_Connect_Click(object sender, RoutedEventArgs e)
        {
            textBoxStatus.Clear();
            try
            {
                textBoxStatus.AppendText("Saving with CMS... Please wait...");
                buttonConnect.IsEnabled = false;

                // Simple settings
                ApplicationSettings.Default.ServerUri = textBoxCmsAddress.Text;
                ApplicationSettings.Default.ServerKey = textBoxCmsKey.Text;
                ApplicationSettings.Default.LibraryPath = textBoxLibraryPath.Text.TrimEnd('\\');
                ApplicationSettings.Default.HardwareKey = textBoxHardwareKey.Text;

                // Also tweak the address of the xmds1
                this.xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=registerDisplay";

                // Proxy Settings
                ApplicationSettings.Default.ProxyUser = textBoxProxyUser.Text;
                ApplicationSettings.Default.ProxyPassword = textBoxProxyPass.Password;
                ApplicationSettings.Default.ProxyDomain = textBoxProxyDomain.Text;

                // Change the default Proxy class
                SetGlobalProxy();

                // Client settings
                ApplicationSettings.Default.SplashOverride = textBoxSplashScreenReplacement.Text;

                // Commit these changes back to the user settings
                ApplicationSettings.Default.Save();

                // Call register
                this.xmds.RegisterDisplayAsync(
                    ApplicationSettings.Default.ServerKey,
                    ApplicationSettings.Default.HardwareKey,
                    ApplicationSettings.Default.DisplayName,
                    "windows",
                    ApplicationSettings.Default.ClientVersion,
                    ApplicationSettings.Default.ClientCodeVersion,
                    Environment.OSVersion.ToString(),
                    this.hardwareKey.MacAddress,
                    this.hardwareKey.Channel,
                    this.hardwareKey.getXmrPublicKey());
            }
            catch (Exception ex)
            {
                textBoxStatus.AppendText(ex.Message);
            }
        }

        /// <summary>
        /// Register Display has been completed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Xmds_RegisterDisplayCompleted(object sender, xmds.RegisterDisplayCompletedEventArgs e)
        {
            buttonConnect.IsEnabled = true;

            textBoxStatus.Clear();

            if (e.Error != null)
            {
                textBoxStatus.AppendText("Status" + Environment.NewLine);
                textBoxStatus.AppendText(e.Error.Message);

                Debug.WriteLine("Error returned from Call to XMDS Register Display.", "xmds1_RegisterDisplayCompleted");
                Debug.WriteLine(e.Error.Message, "xmds1_RegisterDisplayCompleted");
                Debug.WriteLine(e.Error.StackTrace, "xmds1_RegisterDisplayCompleted");
            }
            else
            {
                textBoxStatus.AppendText(RegisterAgent.ProcessRegisterXml(e.Result));
            }
        }

        private void Button_LibraryBrowse_Click(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
            dialog.Description = "Select a folder to store the Player's downloaded files.";
            dialog.UseDescriptionForTitle = true;

            if ((bool)dialog.ShowDialog(this))
            {
                textBoxLibraryPath.Text = dialog.SelectedPath;
            }
        }

        private void Button_SplashScreenReplacement_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();

            // Set filter for file extension and default file extension 
            dialog.DefaultExt = ".png";
            dialog.Filter = "JPEG Files (*.jpeg)|*.jpeg|PNG Files (*.png)|*.png|JPG Files (*.jpg)|*.jpg";


            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dialog.ShowDialog();

            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                // Open document 
                string filename = dialog.FileName;
                textBoxSplashScreenReplacement.Text = filename;
            }
        }

        private void Button_DisplayAdmin_Click(object sender, RoutedEventArgs e)
        {
            // open URL in separate instance of default browser
            try
            {
                Process.Start(ApplicationSettings.Default.ServerUri + @"/index.php?p=display");
            }
            catch
            {
                MessageBox.Show("No web browser installed");
            }
        }

        private void Button_LaunchPlayer_Click(object sender, RoutedEventArgs e)
        {
            Close();
            Process.Start(Process.GetCurrentProcess().MainModule.FileName);
        }
    }
}
