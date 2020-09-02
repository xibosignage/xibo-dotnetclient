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
using EmbedIO;
using EmbedIO.Files;
using EmbedIO.WebApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Documents;

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
            List<string> paths = new List<string>();
            paths.Add("/id_rsa");
            paths.Add("/hardwarekey");
            paths.Add("/cacheManager.xml");
            paths.Add("/config.xml");
            paths.Add("/requiredFiles.xml");
            paths.Add("/schedule.xml");
            paths.Add("/interrupt.json");
            paths.Add("/pop.db");
            paths.Add("/cef");

            var server = new WebServer(o => o
                    .WithUrlPrefix(url)
                    .WithMode(HttpListenerMode.EmbedIO))
                .WithWebApi("/info", m => m
                    .WithController<InfoController>())
                .WithModule(new RestrictiveFileModule("/", new FileSystemProvider(ApplicationSettings.Default.LibraryPath, false), paths), m => m
                    .ContentCaching = false);

            return server;
        }
    }
}
