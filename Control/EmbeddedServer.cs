using EmbedIO;
using System;
using System.Diagnostics;
using System.Threading;
using XiboClient.Log;

namespace XiboClient.Control
{
    class EmbeddedServer
    {
        /// <summary>
        /// Manual Reset
        /// </summary>
        private ManualResetEvent _manualReset = new ManualResetEvent(false);

        public delegate void OnServerClosedDelegate();
        public event OnServerClosedDelegate OnServerClosed;

        /// <summary>
        /// Client Info Form
        /// </summary>
        public ClientInfo ClientInfoForm
        {
            set
            {
                _clientInfoForm = value;
            }
        }
        private ClientInfo _clientInfoForm;

        /// <summary>
        /// Stops the thread
        /// </summary>
        public void Stop()
        {
            _manualReset.Set();
        }

        /// <summary>
        /// Runs the agent
        /// </summary>
        public void Run()
        {
            try
            {
                // If we are restarting, reset
                _manualReset.Reset();

                using (WebServer server = CreateWebServer(ApplicationSettings.Default.EmbeddedServerAddress))
                {
                    server.RunAsync();

                    // Wait
                    _manualReset.WaitOne();
                }

                Trace.WriteLine(new LogMessage("EmbeddedServer - Run", "Server Stopped"), LogType.Info.ToString());
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("EmbeddedServer - Run", "Exception running server: " + e.Message), LogType.Error.ToString());
            }

            OnServerClosed?.Invoke();
        }

        /// <summary>
        /// Create WebServer
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private WebServer CreateWebServer(string url)
        {
            var server = new WebServer(o => o
                    .WithUrlPrefix(url)
                    .WithMode(HttpListenerMode.EmbedIO))
                .WithStaticFolder("/", ApplicationSettings.Default.LibraryPath, false);

            return server;
        }
    }
}
