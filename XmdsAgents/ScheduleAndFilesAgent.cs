/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006 - 2015 Daniel Garner
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
using System.Net;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;

/// 17/02/12 Dan Created
/// 20/02/12 Dan Added ClientInfo
/// 27/02/12 Dan Updated to raise an event when a file has completed downloading

namespace XiboClient.XmdsAgents
{
    class ScheduleAndFilesAgent
    {
        private static object _locker = new object();
        private bool _forceStop = false;
        private ManualResetEvent _manualReset = new ManualResetEvent(false);

        /// <summary>
        /// OnComplete delegate
        /// </summary>
        /// <param name="fileId"></param>
        public delegate void OnCompleteDelegate(string path);
        public event OnCompleteDelegate OnComplete;

        public delegate void OnFullyProvisionedDelegate();
        public event OnFullyProvisionedDelegate OnFullyProvisioned;

        private RequiredFiles _requiredFiles;
        private Semaphore _fileDownloadLimit;

        /// <summary>
        /// Current Schedule Manager for this Xibo Client
        /// </summary>
        public ScheduleManager CurrentScheduleManager
        {
            set
            {
                _scheduleManager = value;
            }
        }
        private ScheduleManager _scheduleManager;

        /// <summary>
        /// Schedule File Location
        /// </summary>
        public string ScheduleLocation
        {
            set
            {
                _scheduleLocation = value;
            }
        }
        private string _scheduleLocation;

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
        public ScheduleAndFilesAgent()
        {
            int limit = (ApplicationSettings.Default.MaxConcurrentDownloads <= 0) ? 1 : ApplicationSettings.Default.MaxConcurrentDownloads;

            _fileDownloadLimit = new Semaphore(limit, limit);
            _requiredFiles = new RequiredFiles();
        }

        /// <summary>
        /// Wake Up
        /// </summary>
        public void WakeUp()
        {
            _manualReset.Set();
        }

        /// <summary>
        /// Stops the thread
        /// </summary>
        public void Stop()
        {
            _forceStop = true;
            _manualReset.Set();
        }

        /// <summary>
        /// Run Thread
        /// </summary>
        public void Run()
        {
            Trace.WriteLine(new LogMessage("RequiredFilesAgent - Run", "Thread Started"), LogType.Info.ToString());

            while (!_forceStop)
            {
                // If we are restarting, reset
                _manualReset.Reset();

                lock (_locker)
                {
                    // Run the schedule Agent thread
                    scheduleAgent();
                
                    if (ApplicationSettings.Default.InDownloadWindow)
                    {
                        try
                        {
                            int filesToDownload = _requiredFiles.FilesDownloading;

                            // If we are currently downloading something, we have to wait
                            if (filesToDownload > 0)
                            {
                                _clientInfoForm.RequiredFilesStatus = string.Format("Waiting: {0} Active Downloads", filesToDownload.ToString());

                                Trace.WriteLine(new LogMessage("RequiredFilesAgent - Run", "Currently Downloading Files, skipping collect"), LogType.Audit.ToString());
                            }
                            else
                            {
                                _clientInfoForm.RequiredFilesStatus = "Running: Requesting connection to Xibo Server";

                                using (xmds.xmds xmds = new xmds.xmds())
                                {
                                    xmds.Credentials = null;
                                    xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=requiredFiles";
                                    xmds.UseDefaultCredentials = false;

                                    // Get required files from XMDS
                                    string requiredFilesXml = xmds.RequiredFiles(ApplicationSettings.Default.ServerKey, _hardwareKey);

                                    // Set the flag to indicate we have a connection to XMDS
                                    ApplicationSettings.Default.XmdsLastConnection = DateTime.Now;

                                    _clientInfoForm.RequiredFilesStatus = "Running: Data received from Xibo Server";

                                    // Load the XML file RF call
                                    XmlDocument xml = new XmlDocument();
                                    xml.LoadXml(requiredFilesXml);

                                    // Create a required files object and set it to contain the RF returned this tick
                                    _requiredFiles = new RequiredFiles();
                                    _requiredFiles.CurrentCacheManager = _cacheManager;
                                    _requiredFiles.RequiredFilesXml = xml;

                                    // List of Threads to start
                                    // TODO: Track these threads so that we can abort them if the application closes
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
                                        fileAgent.RequiredFileType = fileToDownload.FileType;
                                        fileAgent.OnComplete += new FileAgent.OnCompleteDelegate(fileAgent_OnComplete);
                                        fileAgent.OnPartComplete += new FileAgent.OnPartCompleteDelegate(fileAgent_OnPartComplete);

                                        // Create the thread and add it to the list of threads to start
                                        Thread thread = new Thread(new ThreadStart(fileAgent.Run));
                                        thread.Name = "FileAgent_" + fileToDownload.FileType + "_Id_" + fileToDownload.Id.ToString();
                                        threadsToStart.Add(thread);
                                    }

                                    // Start the threads after we have built them all - otherwise they will modify the collection we 
                                    // are iterating over.
                                    foreach (Thread thread in threadsToStart)
                                        thread.Start();

                                    // Report what we are doing back to MediaInventory
                                    _requiredFiles.ReportInventory();

                                    // Write Required Files
                                    _requiredFiles.WriteRequiredFiles();

                                    // Write the Cache Manager to Disk
                                    _cacheManager.WriteCacheManager();

                                    // Report the storage usage
                                    reportStorage();

                                    // Set the status on the client info screen
                                    if (threadsToStart.Count == 0)
                                    {
                                        _clientInfoForm.RequiredFilesStatus = "Sleeping (inside download window)";
                                        
                                        // Raise an event to say we've completed
                                        if (OnFullyProvisioned != null)
                                            OnFullyProvisioned();
                                    }
                                    else
                                        _clientInfoForm.RequiredFilesStatus = string.Format("{0} files to download", threadsToStart.Count.ToString());

                                    _clientInfoForm.UpdateRequiredFiles(RequiredFilesString());
                                }
                            }
                        }
                        catch (WebException webEx)
                        {
                            // Increment the quantity of XMDS failures and bail out
                            ApplicationSettings.Default.IncrementXmdsErrorCount();

                            // Log this message, but dont abort the thread
                            Trace.WriteLine(new LogMessage("RequiredFilesAgent - Run", "WebException in Run: " + webEx.Message), LogType.Info.ToString());

                            _clientInfoForm.RequiredFilesStatus = "Error: " + webEx.Message;
                        }
                        catch (Exception ex)
                        {
                            // Log this message, but dont abort the thread
                            Trace.WriteLine(new LogMessage("RequiredFilesAgent - Run", "Exception in Run: " + ex.Message), LogType.Error.ToString());

                            _clientInfoForm.RequiredFilesStatus = "Error: " + ex.Message;
                        }
                    }
                    else
                    {
                        _clientInfoForm.RequiredFilesStatus = string.Format("Outside Download Window {0} - {1}", ApplicationSettings.Default.DownloadStartWindowTime.ToString("HH:mm", CultureInfo.InvariantCulture), ApplicationSettings.Default.DownloadEndWindowTime.ToString("HH:mm", CultureInfo.InvariantCulture));
                    }
                }

                // Sleep this thread until the next collection interval
                _manualReset.WaitOne((int)(ApplicationSettings.Default.CollectInterval * ApplicationSettings.Default.XmdsCollectionIntervalFactor() * 1000));
            }

            Trace.WriteLine(new LogMessage("RequiredFilesAgent - Run", "Thread Stopped"), LogType.Info.ToString());
        }

        /// <summary>
        /// String representation of the required files
        /// </summary>
        /// <returns></returns>
        private string RequiredFilesString()
        {
            string requiredFilesTextBox = "";

            foreach (RequiredFile requiredFile in _requiredFiles.RequiredFileList)
            {
                string percentComplete = (!requiredFile.Complete) ? Math.Round((((double)requiredFile.ChunkOffset / (double)requiredFile.Size) * 100), 1).ToString() : "100";
                requiredFilesTextBox = requiredFilesTextBox + requiredFile.FileType + ": " + requiredFile.SaveAs + ". (" + percentComplete + "%)" + Environment.NewLine;
            }

            return requiredFilesTextBox;
        }

        /// <summary>
        /// FileAgent OnPartComplete
        /// </summary>
        /// <param name="fileId"></param>
        void fileAgent_OnPartComplete(int fileId)
        {
            _clientInfoForm.UpdateRequiredFiles(RequiredFilesString());
        }

        /// <summary>
        /// FileAgent OnComplete
        /// </summary>
        /// <param name="fileId"></param>
        void fileAgent_OnComplete(int fileId, string fileType)
        {
            // Notify the player thread using another event (chained events? bad idea?)
            Trace.WriteLine(new LogMessage("RequiredFilesAgent - fileAgent_OnComplete", "FileId finished downloading" + fileId.ToString()));

            // Get the required file associated with this ID
            RequiredFile rf = _requiredFiles.GetRequiredFile(fileId, fileType);

            // Set the status on the client info screen
            if (_requiredFiles.FilesDownloading == 0)
            {
                _clientInfoForm.RequiredFilesStatus = "Sleeping";

                // If we are the last download thread to complete, then we should report media inventory and raise an event to say we've got everything
                _requiredFiles.ReportInventory();

                // Raise an event to say we've completed
                if (OnFullyProvisioned != null)
                    OnFullyProvisioned();
            }
            else
            {
                _clientInfoForm.RequiredFilesStatus = string.Format("{0} files to download", _requiredFiles.FilesDownloading.ToString());
            }

            // Update the RequiredFiles TextBox
            _clientInfoForm.UpdateRequiredFiles(RequiredFilesString());

            // Write the Cache Manager to Disk
            _cacheManager.WriteCacheManager();

            if (rf.FileType == "layout")
            {
                // Raise an event to say it is completed
                if (OnComplete != null)
                    OnComplete(rf.SaveAs);
            }
        }

        private void reportStorage()
        {
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);

            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                writer.Formatting = Newtonsoft.Json.Formatting.None;
                writer.WriteStartObject();
                writer.WritePropertyName("deviceName");
                writer.WriteValue(Environment.MachineName);

                // Use Drive Info
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (drive.IsReady && ApplicationSettings.Default.LibraryPath.Contains(drive.RootDirectory.FullName))
                    {
                        writer.WritePropertyName("availableSpace");
                        writer.WriteValue(drive.TotalFreeSpace);
                        writer.WritePropertyName("totalSpace");
                        writer.WriteValue(drive.TotalSize);
                        break;
                    }
                }
                
                writer.WriteEndObject();

                // Report
                using (xmds.xmds xmds = new xmds.xmds())
                {
                    xmds.Credentials = null;
                    xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=notifyStatus";
                    xmds.UseDefaultCredentials = false;
                    xmds.NotifyStatusAsync(ApplicationSettings.Default.ServerKey, ApplicationSettings.Default.HardwareKey, sb.ToString());
                }
            }
        }

        /// <summary>
        /// Schedule Agent
        /// </summary>
        private void scheduleAgent()
        {
            try
            {
                // If we are restarting, reset
                _manualReset.Reset();

                Trace.WriteLine(new LogMessage("ScheduleAgent - Run", "Thread Woken and Lock Obtained"), LogType.Audit.ToString());

                _clientInfoForm.ScheduleStatus = "Running: Get Data from Xibo Server";

                using (xmds.xmds xmds = new xmds.xmds())
                {
                    xmds.Credentials = null;
                    xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=schedule";
                    xmds.UseDefaultCredentials = false;

                    string scheduleXml = xmds.Schedule(ApplicationSettings.Default.ServerKey, _hardwareKey);

                    // Set the flag to indicate we have a connection to XMDS
                    ApplicationSettings.Default.XmdsLastConnection = DateTime.Now;

                    _clientInfoForm.ScheduleStatus = "Running: Data Received";

                    // Hash of the result
                    string md5NewSchedule = Hashes.MD5(scheduleXml);
                    string md5CurrentSchedule = Hashes.MD5(ScheduleManager.GetScheduleXmlString(_scheduleLocation));

                    // Compare the results of the HASH
                    if (md5CurrentSchedule != md5NewSchedule)
                    {
                        Trace.WriteLine(new LogMessage("Schedule Agent - Run", "Received new schedule"));

                        _clientInfoForm.ScheduleStatus = "Running: New Schedule Received";

                        // Write the result to the schedule xml location
                        ScheduleManager.WriteScheduleXmlToDisk(_scheduleLocation, scheduleXml);

                        // Indicate to the schedule manager that it should read the XML file
                        _scheduleManager.RefreshSchedule = true;
                    }

                    _clientInfoForm.ScheduleStatus = "Sleeping";
                }
            }
            catch (WebException webEx)
            {
                // Increment the quantity of XMDS failures and bail out
                ApplicationSettings.Default.IncrementXmdsErrorCount();

                // Log this message, but dont abort the thread
                Trace.WriteLine(new LogMessage("ScheduleAgent - Run", "WebException in Run: " + webEx.Message), LogType.Info.ToString());

                _clientInfoForm.ScheduleStatus = "Error: " + webEx.Message;
            }
            catch (Exception ex)
            {
                // Log this message, but dont abort the thread
                Trace.WriteLine(new LogMessage("ScheduleAgent - Run", "Exception in Run: " + ex.Message), LogType.Error.ToString());
                _clientInfoForm.ScheduleStatus = "Error. " + ex.Message;
            }
        }
    }
}
