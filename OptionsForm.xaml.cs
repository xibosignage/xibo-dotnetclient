using System.Diagnostics;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Windows;

namespace XiboClient
{
    /// <summary>
    /// Interaction logic for OptionsForm.xaml
    /// </summary>
    public partial class OptionsForm : Window
    {
        public OptionsForm()
        {
            InitializeComponent();
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
    }
}
