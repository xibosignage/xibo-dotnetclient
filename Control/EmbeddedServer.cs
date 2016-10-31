using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Modules;
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

                using (WebServer server = new WebServer(ApplicationSettings.Default.EmbeddedServerAddress))
                {
                    Dictionary<string, string> headers = new Dictionary<string, string>()
                    {
                        { Constants.HeaderCacheControl, "no-cache, no-store, must-revalidate" },
                        { Constants.HeaderPragma, "no-cache" },
                        { Constants.HeaderExpires, "0" }
                    };

                    server.RegisterModule(new StaticFilesModule(ApplicationSettings.Default.LibraryPath, headers));
                    server.Module<StaticFilesModule>().UseRamCache = true;
                    server.Module<StaticFilesModule>().DefaultExtension = ".html";

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

            if (OnServerClosed != null)
                OnServerClosed();
        }
    }
}
