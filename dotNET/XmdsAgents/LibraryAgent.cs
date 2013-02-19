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
using System.IO;

/// 09/04/12 Dan Created

namespace XiboClient.XmdsAgents
{
    class LibraryAgent
    {
        private object _locker = new object();
        private bool _forceStop = false;
        private ManualResetEvent _manualReset = new ManualResetEvent(false);

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
                        if (!Settings.Default.EnableExpiredFileDeletion)
                            return;

                        // Get required files from disk
                        _requiredFiles = RequiredFiles.LoadFromDisk();

                        // Build a list of files in the library
                        DirectoryInfo directory = new DirectoryInfo(Settings.Default.LibraryPath);
                        
                        // Check each one and see if it is in required files
                        foreach (FileInfo fileInfo in directory.GetFiles())
                        {
                            // Delete files that were accessed over N days ago
                            try
                            {
                                RequiredFile file = _requiredFiles.GetRequiredFile(fileInfo.Name);
                            }
                            catch
                            {
                                // Not a required file
                                if (fileInfo.LastAccessTime < DateTime.Now.AddDays(Settings.Default.LibraryAgentInterval * -1))
                                {
                                    Trace.WriteLine(new LogMessage("LibraryAgent - Run", "Deleting old file: " + fileInfo.Name), LogType.Info.ToString());
                                    File.Delete(fileInfo.FullName);
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

                // Sleep this thread for 5 minutes
                _manualReset.WaitOne(900 * 1000);
            }

            Trace.WriteLine(new LogMessage("LibraryAgent - Run", "Thread Stopped"), LogType.Info.ToString());
        }
    }
}
