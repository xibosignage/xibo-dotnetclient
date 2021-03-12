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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ookii.Dialogs.Wpf;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using XiboClient.Stats;
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

        /// <summary>
        /// Constructor
        /// </summary>
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
            labelPlayerVersion.Content = ApplicationSettings.Default.ClientVersion;
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
                // Log policy errors
                if (sslPolicyErrors != System.Net.Security.SslPolicyErrors.None)
                {
                    Debug.WriteLine("ServerCertificateValidationCallback: SSL Policy Errors for " 
                        + certificate.Subject + ", " + certificate.Issuer 
                        + ". Errors: " + sslPolicyErrors.ToString(), "SetGlobalProxy");
                }

                return true;
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
            // State
            this.textBoxStatus.Clear();
            this.buttonConnect.IsEnabled = false;
            this.IsAuthCodeProcessing = false;
            this.buttonUseCode.IsEnabled = true;
            this.buttonLaunchPlayer.IsEnabled = true;
            this.textBoxStatus.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
            this.textBoxStatus.FontSize = 12;
            this.textBoxStatus.TextAlignment = TextAlignment.Left;

            // Try to create the SQLite database
            try
            {
                StatManager.Instance.InitDatabase();
            }
            catch
            {
                textBoxStatus.AppendText("There was a problem creating a local database. This will probably solve itself when the Player starts, but as a precaution you could press Connect again.");
            }

            try
            {
                textBoxStatus.AppendText("Saving with CMS... Please wait...");

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
                Process.Start(ApplicationSettings.Default.ServerUri + @"/display/view");
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

        private async void Button_UseCode_Click(object sender, RoutedEventArgs e)
        {
            this.IsAuthCodeProcessing = true;

            // Disable the button
            this.buttonUseCode.IsEnabled = false;
            this.buttonLaunchPlayer.IsEnabled = false;

            // Clear the text area
            this.textBoxStatus.Clear();

            // Save local elements
            ApplicationSettings.Default.LibraryPath = textBoxLibraryPath.Text.TrimEnd('\\');
            ApplicationSettings.Default.HardwareKey = textBoxHardwareKey.Text;

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

            // Try to create the SQLite database
            try
            {
                StatManager.Instance.InitDatabase();
            }
            catch
            {
                textBoxStatus.AppendText("There was a problem creating a local database. This will probably solve itself when the Player starts, but as a precaution you could press Use Code again.");
            }

            // Show the code in the status window, and disable the other buttons.
            try
            {
                JObject codes = await GenerateCode();

                this.UserCode = codes["user_code"].ToString();
                this.DeviceCode = codes["device_code"].ToString();

                this.textBoxStatus.Text = this.UserCode;
                this.textBoxStatus.FontFamily = new System.Windows.Media.FontFamily("Consolas");
                this.textBoxStatus.FontSize = 48;
                this.textBoxStatus.TextAlignment = TextAlignment.Center;

                // Start a timer
                AuthCodeTimer = new System.Timers.Timer();
                AuthCodeTimer.Elapsed += AuthCodeTimer_Elapsed;
                AuthCodeTimer.Interval = 10000;
                AuthCodeTimer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message, "Button_UseCode_Click");

                this.textBoxStatus.Text = "Unable to get a code, please configure manually.";
                this.buttonUseCode.IsEnabled = true;
                this.buttonLaunchPlayer.IsEnabled = true;
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
                    this.xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=registerDisplay";

                    var response = this.xmds.RegisterDisplay(
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

                    // Process the XML
                    RegisterAgent.ProcessRegisterXml(response);

                    // Launch
                    Dispatcher.Invoke(new System.Action(
                        () =>
                        {
                            Close();
                            Process.Start(Process.GetCurrentProcess().MainModule.FileName);
                        })
                        );
                }
                catch (Exception ex)
                {
                    this.textBoxStatus.Text = "Problem Claiming Code, please try again.";
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
                        writer.Formatting = Formatting.None;
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
