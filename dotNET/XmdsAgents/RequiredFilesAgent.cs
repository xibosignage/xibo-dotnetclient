/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006 - 2012 Daniel Garner
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
using System.Text;
using System.Threading;
using XiboClient.Properties;
using System.Diagnostics;
using System.Xml;
using XiboClient.Log;

/// 17/02/12 Dan Created
/// 20/02/12 Dan Added ClientInfo

namespace XiboClient.XmdsAgents
{
    class RequiredFilesAgent
    {
        public object _locker = new object();
        public bool forceStop = false;

        private RequiredFiles _requiredFiles;
        private Semaphore _fileDownloadLimit;

        /// <summary>
        /// Client Hardware key
        /// </summary>
        public string HardwareKey
        {
            set
            {
                _hardwareKey = value;
            }
        }
        private string _hardwareKey;

        /// <summary>
        /// The Current CacheManager for this Xibo Client
        /// </summary>
        public CacheManager CurrentCacheManager
        {
            set
            {
                _cacheManager = value;
            }
        }
        private CacheManager _cacheManager;

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
        /// Required Files Agent
        /// </summary>
        public RequiredFilesAgent()
        {
            _fileDownloadLimit = new Semaphore(Settings.Default.MaxConcurrentDownloads, Settings.Default.MaxConcurrentDownloads);
            _requiredFiles = new RequiredFiles();
        }

        /// <summary>
        /// Run Thread
        /// </summary>
        public void Run()
        {
            Trace.WriteLine(new LogMessage("RequiredFilesAgent - Run", "Thread Started"), LogType.Info.ToString());

            while (!forceStop)
            {
                lock (_locker)
                {
                    try
                    {
                        bool currentlyDownloading = false;

                        // See if we are downloading anything at the moment
                        foreach (RequiredFile fileToDownload in _requiredFiles.RequiredFileList)
                        {
                            if (fileToDownload.Downloading)
                                currentlyDownloading = true;
                        }

                        // If we are currently downloading something, we have to wait
                        if (currentlyDownloading)
                        {
                            _clientInfoForm.RequiredFilesStatus = "Waiting: Active Downloads";

                            Trace.WriteLine(new LogMessage("RequiredFilesAgent - Run", "Currently Downloading Files, skipping collect"), LogType.Info.ToString());
                        }
                        else
                        {
                            _clientInfoForm.RequiredFilesStatus = "Running: Requesting connection to Xibo Server";

                            using (xmds.xmds xmds = new xmds.xmds())
                            {
                                xmds.Credentials = null;
                                xmds.Url = Properties.Settings.Default.XiboClient_xmds_xmds;
                                xmds.UseDefaultCredentials = false;

                                // Get required files from XMDS
                                string requiredFilesXml = xmds.RequiredFiles(Settings.Default.ServerKey, _hardwareKey, Settings.Default.Version);

                                // Set the flag to indicate we have a connection to XMDS
                                Settings.Default.XmdsLastConnection = DateTime.Now;

                                _clientInfoForm.RequiredFilesStatus = "Running: Data received from Xibo Server";

                                // Load the XML file RF call
                                XmlDocument xml = new XmlDocument();
                                xml.LoadXml(requiredFilesXml);

                                // Create a required files object and set it to contain the RF returned this tick
                                _requiredFiles = new RequiredFiles();
                                _requiredFiles.CurrentCacheManager = _cacheManager;
                                _requiredFiles.RequiredFilesXml = xml;

                                List<Thread> threadsToStart = new List<Thread>();

                                // Required files now contains a list of files to download (this will be updated by the various worker threads)
                                foreach (RequiredFile fileToDownload in _requiredFiles.RequiredFileList)
                                {
                                    // Skip downloaded files
                                    if (fileToDownload.Complete)
                                        continue;

                                    // Spawn a thread to download this file.
                                    FileAgent fileAgent = new FileAgent();
                                    fileAgent.FileDownloadLimit = _fileDownloadLimit;
                                    fileAgent.HardwareKey = _hardwareKey;
                                    fileAgent.RequiredFiles = _requiredFiles;
                                    fileAgent.RequiredFileId = fileToDownload.Id;
                                    
                                    // Create the thread and add it to the list of threads to start
                                    Thread thread = new Thread(new ThreadStart(fileAgent.Run));
                                    thread.Name = "FileAgent_Id_" + fileToDownload.Id.ToString();
                                    threadsToStart.Add(thread);
                                }

                                // Start the threads after we have built them all - otherwise they will modify the collection we 
                                // are itterating over.
                                foreach (Thread thread in threadsToStart)
                                    thread.Start();

                                // Report what we are doing back to MediaInventory
                                _requiredFiles.ReportInventory();

                                // Write Required Files
                                _requiredFiles.WriteRequiredFiles();

                                // Write the Cache Manager to Disk
                                _cacheManager.WriteCacheManager();

                                _clientInfoForm.RequiredFilesStatus = string.Format("Sleeping: {0} files to download", threadsToStart.Count.ToString());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log this message, but dont abort the thread
                        Trace.WriteLine(new LogMessage("RequiredFilesAgent - Run", "Exception in Run: " + ex.Message), LogType.Error.ToString());

                        _clientInfoForm.RequiredFilesStatus = "Error: " + ex.Message;
                    }
                }

                // Sleep this thread until the next collection interval
                Thread.Sleep((int)Settings.Default.collectInterval * 1000);
            }
        }
    }
}
