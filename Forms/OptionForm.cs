/*
 * Xibo - Digital Signage - http://xibo.org.uk
 * Copyright (C) 2020 Xibo Signage Ltd
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
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace XiboClient
{
    public partial class OptionForm : Form
    {
        private HardwareKey _hardwareKey;

        /// <summary>
        /// Are we in the process or using an auth code
        /// </summary>
        private bool IsAuthCodeProcessing = false;

        /// <summary>
        /// The User Code assigned to this device by the Auth Code process
        /// </summary>
        private string UserCode;

        /// <summary>
        /// The device code assigned to this device by the Auth Code process
        /// This is secret!
        /// </summary>
        private string DeviceCode;

        /// <summary>
        /// Auth Code Timer
        /// </summary>
        private System.Timers.Timer AuthCodeTimer;

        public OptionForm()
        {
            InitializeComponent();
            
            // Set the icon
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            // Hide unnecessary fields
            if (Application.ProductName != "Xibo")
            {
                label17.Hide();
                linkLabel1.Hide();
            }

            // Get a hardware key here, just in case we havent been able to get one before
            _hardwareKey = new HardwareKey();

            // XMDS completed event
            xmds1.RegisterDisplayCompleted += new XiboClient.xmds.RegisterDisplayCompletedEventHandler(xmds1_RegisterDisplayCompleted);

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

            // Switch to TLS 2.1
            System.Net.ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

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
            // State
            this.tbStatus.ResetText();
            this.buttonSaveSettings.Enabled = false;
            this.IsAuthCodeProcessing = false;
            this.buttonUseCode.Enabled = true;
            this.buttonLaunchPlayer.Enabled = true;
            this.tbStatus.Font = new Font("Arial", 12);
            this.tbStatus.TextAlign = HorizontalAlignment.Left;

            try
            {
                tbStatus.AppendText("Saving with CMS... Please wait...");

                // Simple settings
                ApplicationSettings.Default.ServerKey = textBoxServerKey.Text;
                ApplicationSettings.Default.LibraryPath = textBoxLibraryPath.Text.TrimEnd('\\');
                ApplicationSettings.Default.ServerUri = textBoxXmdsUri.Text;
                ApplicationSettings.Default.HardwareKey = tbHardwareKey.Text;

                // Also tweak the address of the xmds1
                xmds1.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=registerDisplay";

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
                xmds1.RegisterDisplayAsync(
                    ApplicationSettings.Default.ServerKey, 
                    ApplicationSettings.Default.HardwareKey, 
                    ApplicationSettings.Default.DisplayName, 
                    "windows", 
                    ApplicationSettings.Default.ClientVersion, 
                    ApplicationSettings.Default.ClientCodeVersion, 
                    Environment.OSVersion.ToString(), 
                    _hardwareKey.MacAddress,
                    _hardwareKey.Channel,
                    _hardwareKey.getXmrPublicKey());
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
                Process.Start(Properties.Resources.SupportUrl);
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
                System.Diagnostics.Process.Start(ApplicationSettings.Default.ServerUri + @"/display/view");
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

        private void Button_LaunchPlayer_Click(object sender, EventArgs e)
        {
            Close();
            Process.Start(Application.ExecutablePath);
        }

        private async void Button_UseCode_Click(object sender, EventArgs e)
        {
            this.IsAuthCodeProcessing = true;

            // Disable the button
            this.buttonUseCode.Enabled = false;
            this.buttonLaunchPlayer.Enabled = false;

            // Clear the text area
            this.tbStatus.ResetText();

            // Save local elements
            ApplicationSettings.Default.LibraryPath = textBoxLibraryPath.Text.TrimEnd('\\');
            ApplicationSettings.Default.HardwareKey = tbHardwareKey.Text;

            // Proxy Settings
            ApplicationSettings.Default.ProxyUser = textBoxProxyUser.Text;
            ApplicationSettings.Default.ProxyPassword = maskedTextBoxProxyPass.Text;
            ApplicationSettings.Default.ProxyDomain = textBoxProxyDomain.Text;

            // Change the default Proxy class
            SetGlobalProxy();

            // Client settings
            ApplicationSettings.Default.SplashOverride = splashOverride.Text;

            // Commit these changes back to the user settings
            ApplicationSettings.Default.Save();

            // Show the code in the status window, and disable the other buttons.
            try
            {
                JObject codes = await GenerateCode();

                this.UserCode = codes["user_code"].ToString();
                this.DeviceCode = codes["device_code"].ToString();

                this.tbStatus.Text = this.UserCode;
                this.tbStatus.Font = new Font("Consolas", 48);
                this.tbStatus.TextAlign = HorizontalAlignment.Center;

                // Start a timer
                AuthCodeTimer = new System.Timers.Timer();
                AuthCodeTimer.Elapsed += AuthCodeTimer_Elapsed;
                AuthCodeTimer.Interval = 10000;
                AuthCodeTimer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message, "Button_UseCode_Click");

                this.tbStatus.Text = "Unable to get a code, please configure manually.";
                this.buttonUseCode.Enabled = true;
                this.buttonLaunchPlayer.Enabled = true;
                this.IsAuthCodeProcessing = false;
            }
        }

        private async void AuthCodeTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            // If the button has been made visible again, then cancel the check
            if (!this.IsAuthCodeProcessing)
            {
                ((System.Timers.Timer)sender).Stop();
                ((System.Timers.Timer)sender).Elapsed -= AuthCodeTimer_Elapsed;
                ((System.Timers.Timer)sender).Dispose();

                this.UserCode = string.Empty;
                this.DeviceCode = string.Empty;
                return;
            }

            if (await CheckCode(this.UserCode, this.DeviceCode))
            {
                // We have great success
                ((System.Timers.Timer)sender).Stop();
                ((System.Timers.Timer)sender).Elapsed -= AuthCodeTimer_Elapsed;
                ((System.Timers.Timer)sender).Dispose();

                // The task has already set our config, all we need to do is call Register
                try
                {
                    // Assert the XMDS url
                    this.xmds1.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=registerDisplay";

                    var response = this.xmds1.RegisterDisplay(
                        ApplicationSettings.Default.ServerKey,
                        ApplicationSettings.Default.HardwareKey,
                        ApplicationSettings.Default.DisplayName,
                        "windows",
                        ApplicationSettings.Default.ClientVersion,
                        ApplicationSettings.Default.ClientCodeVersion,
                        Environment.OSVersion.ToString(),
                        this._hardwareKey.MacAddress,
                        this._hardwareKey.Channel,
                        this._hardwareKey.getXmrPublicKey());

                    // Process the XML
                    RegisterAgent.ProcessRegisterXml(response);

                    // Launch
                    BeginInvoke(new System.Action(
                        () =>
                        {
                            Close();
                            Process.Start(Process.GetCurrentProcess().MainModule.FileName);
                        })
                        );
                }
                catch (Exception ex)
                {
                    this.tbStatus.Text = "Problem Claiming Code, please try again.";
                    Trace.WriteLine(new LogMessage("OptionsForm", "AuthCodeTimer_Elapsed: Problem claiming code: " + ex.Message), LogType.Error.ToString());
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private static async Task<JObject> GenerateCode()
        {
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Post, "https://auth.signlicence.co.uk/generateCode"))
            {
                StringBuilder sb = new StringBuilder();
                using (StringWriter sw = new StringWriter(sb))
                {
                    using (JsonWriter writer = new JsonTextWriter(sw))
                    {
                        writer.Formatting = Newtonsoft.Json.Formatting.None;
                        writer.WriteStartObject();
                        writer.WritePropertyName("hardwareId");
                        writer.WriteValue(ApplicationSettings.Default.HardwareKey);
                        writer.WritePropertyName("type");
                        writer.WriteValue("windows");
                        writer.WritePropertyName("version");
                        writer.WriteValue("" + ApplicationSettings.Default.ClientCodeVersion);
                        writer.WriteEndObject();
                    }
                }

                using (var stringContent = new StringContent(sb.ToString(), Encoding.UTF8, "application/json"))
                {
                    request.Content = stringContent;

                    using (var response = await client
                        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                        .ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        var jsonString = await response.Content.ReadAsStringAsync();
                        var json = JsonConvert.DeserializeObject<JObject>(jsonString);

                        if (json.ContainsKey("message"))
                        {
                            throw new Exception("Request returned a message in the body, discard");
                        }

                        return json;
                    }
                }
            }
        }

        /// <summary>
        /// Check Code
        /// </summary>
        /// <returns></returns>
        private static async Task<bool> CheckCode(string UserCode, string DeviceCode)
        {
            Debug.WriteLine("Calling Auth Service", "CheckCode");

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Get, "https://auth.signlicence.co.uk/getDetails?user_code=" + UserCode + "&device_code=" + DeviceCode))
            {
                using (var response = await client
                        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                        .ConfigureAwait(false))
                {
                    try
                    {
                        response.EnsureSuccessStatusCode();
                        var jsonString = await response.Content.ReadAsStringAsync();
                        var json = JsonConvert.DeserializeObject<JObject>(jsonString);

                        if (json.ContainsKey("cmsAddress"))
                        {
                            // TODO: we are done! 
                            ApplicationSettings.Default.ServerUri = json["cmsAddress"].ToString();
                            ApplicationSettings.Default.ServerKey = json["cmsKey"].ToString();

                            // Returning true stops the task and launches the player
                            return true;
                        }
                        else
                        {
                            Debug.WriteLine(jsonString, "CheckCode");
                            return false;
                        }
                    }
                    catch
                    {
                        Debug.WriteLine("Non 200/300 status code", "CheckCode");
                        return false;
                    }
                }
            }
        }
    }
}