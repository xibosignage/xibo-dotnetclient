/**
 * Copyright (C) 2019 Xibo Signage Ltd
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
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using XiboClient.Properties;
using System.Diagnostics;
using System.Xml;
using XiboClient.Log;
using System.IO;

/// 09/04/12 Dan Created

namespace XiboClient.XmdsAgents
{
    class LibraryAgent
    {
        private object _locker = new object();
        private bool _forceStop = false;
        private ManualResetEvent _manualReset = new ManualResetEvent(false);

        private List<string> _persistentFiles = new List<string>();

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
        /// Required Files Object
        /// </summary>
        private RequiredFiles _requiredFiles;

        public LibraryAgent()
        {
            _persistentFiles.Add("cacheManager.xml");
            _persistentFiles.Add("requiredFiles.xml");
            _persistentFiles.Add("schedule.xml");
            _persistentFiles.Add("status.json");
            _persistentFiles.Add("hardwarekey");
            _persistentFiles.Add("config.xml");
            _persistentFiles.Add("id_rsa");
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
            Trace.WriteLine(new LogMessage("LibraryAgent - Run", "Thread Started"), LogType.Info.ToString());

            while (!_forceStop)
            {
                lock (_locker)
                {
                    try
                    {
                        // If we are restarting, reset
                        _manualReset.Reset();

                        // Only do something if enabled
                        if (!ApplicationSettings.Default.EnableExpiredFileDeletion)
                        {
                            Trace.WriteLine(new LogMessage("LibraryAgent - Run", "Expired File Deletion Disabled"), LogType.Audit.ToString());
                            return;
                        }

                        // Test Date
                        DateTime testDate = DateTime.Now.AddDays(ApplicationSettings.Default.LibraryAgentInterval * -1);

                        // Get required files from disk
                        _requiredFiles = RequiredFiles.LoadFromDisk();

                        Trace.WriteLine(new LogMessage("LibraryAgent - Run", "Number of required files = " + _requiredFiles.RequiredFileList.Count), LogType.Audit.ToString());

                        // Build a list of files in the library
                        DirectoryInfo directory = new DirectoryInfo(ApplicationSettings.Default.LibraryPath);
                        
                        // Check each one and see if it is in required files
                        foreach (FileInfo fileInfo in directory.GetFiles())
                        {
                            // Never delete certain system files
                            // Also do not delete log/stat files as they are managed by their respective agents
                            if (_persistentFiles.Contains(fileInfo.Name) || 
                                fileInfo.Name.Contains(ApplicationSettings.Default.LogLocation) || 
                                fileInfo.Name.Contains(ApplicationSettings.Default.StatsLogFile)
                                )
                                continue;

                            // Delete files that were accessed over N days ago
                            try
                            {
                                RequiredFile file = _requiredFiles.GetRequiredFile(fileInfo.Name);
                            }
                            catch
                            {
                                // It is a bad idea to log in here - it can cause a build up of log files.
                                //Debug.WriteLine(new LogMessage("LibraryAgent - Run", fileInfo.Name + " is not in Required Files, testing last accessed date [" + fileInfo.LastAccessTime + "] is earlier than " + testDate), LogType.Audit.ToString());

                                // Not a required file
                                if (fileInfo.LastAccessTime < testDate)
                                {
                                    Trace.WriteLine(new LogMessage("LibraryAgent - Run", "Deleting old file: " + fileInfo.Name), LogType.Info.ToString());
                                    File.Delete(fileInfo.FullName);

                                    // Is this a HTZ file?
                                    if (fileInfo.Extension.ToLower() == ".htz")
                                    {
                                        // Also delete the extracted version of this file
                                        string pathToPackageFolder = Path.Combine(ApplicationSettings.Default.LibraryPath, "package_" + fileInfo.Name.Replace(fileInfo.Extension, ""));

                                        if (Directory.Exists(pathToPackageFolder))
                                        {
                                            Directory.Delete(pathToPackageFolder, true);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log this message, but dont abort the thread
                        Trace.WriteLine(new LogMessage("LibraryAgent - Run", "Exception in Run: " + ex.Message), LogType.Error.ToString());
                    }
                }

                // Sleep this thread for 15 minutes
                _manualReset.WaitOne(2700 * 1000);
            }

            Trace.WriteLine(new LogMessage("LibraryAgent - Run", "Thread Stopped"), LogType.Info.ToString());
        }
    }
}
