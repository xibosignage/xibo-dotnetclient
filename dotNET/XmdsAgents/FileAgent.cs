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
using System.IO;

/// 17/02/12 Dan Created
/// 21/02/12 Dan Added OnComplete Delegate and Event
/// 28/02/12 Dan Added OnPartComplete Delegate and Event
/// 22/04/12 Dan Dispose of XMDS between each request

namespace XiboClient.XmdsAgents
{
    class FileAgent
    {
        /// <summary>
        /// OnComplete delegate
        /// </summary>
        /// <param name="fileId"></param>
        public delegate void OnCompleteDelegate(int fileId, string fileType);
        public event OnCompleteDelegate OnComplete;

        /// <summary>
        /// OnPartComplete delegate
        /// </summary>
        /// <param name="fileId"></param>
        public delegate void OnPartCompleteDelegate(int fileId);
        public event OnPartCompleteDelegate OnPartComplete;

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
        /// Required Files Object
        /// </summary>
        public RequiredFiles RequiredFiles
        {
            set
            {
                _requiredFiles = value;
            }
        }
        private RequiredFiles _requiredFiles;

        /// <summary>
        /// The ID of the required file this FileAgent is downloading
        /// </summary>
        public int RequiredFileId
        {
            set
            {
                _requiredFileId = value;
            }
        }
        private int _requiredFileId;

        /// <summary>
        /// The File Type
        /// </summary>
        public string RequiredFileType
        {
            set
            {
                _requiredFileType = value;
            }
        }
        private string _requiredFileType;

        /// <summary>
        /// File Download Limit Semaphore
        /// </summary>
        public Semaphore FileDownloadLimit
        {
            set
            {
                _fileDownloadLimit = value;
            }
        }
        private Semaphore _fileDownloadLimit;

        /// <summary>
        /// File Agent Responsible for downloading a single file
        /// </summary>
        public FileAgent()
        {
            
        }

        /// <summary>
        /// Runs the agent
        /// </summary>
        public void Run()
        {
            Trace.WriteLine(new LogMessage("FileAgent - Run", "Thread Started"), LogType.Info.ToString());

            // Get the required file id from the list of required files.
            RequiredFile file = _requiredFiles.GetRequiredFile(_requiredFileId, _requiredFileType);

            // Set downloading to be true
            file.Downloading = true;

            // Wait for the Semaphore lock to become available
            _fileDownloadLimit.WaitOne();

            try
            {
                Trace.WriteLine(new LogMessage("FileAgent - Run", "Thread alive and Lock Obtained"), LogType.Info.ToString());

                if (file.FileType == "resource")
                {
                    // Download using GetResource
                    using (xmds.xmds xmds = new xmds.xmds())
                    {
                        xmds.Credentials = null;
                        xmds.Url = Settings.Default.XiboClient_xmds_xmds;
                        xmds.UseDefaultCredentials = true;

                        string result = xmds.GetResource(Settings.Default.ServerKey, Settings.Default.hardwareKey, file.LayoutId, file.RegionId, file.MediaId, Settings.Default.Version);

                        // Write the result to disk
                        using (StreamWriter sw = new StreamWriter(File.Open(Settings.Default.LibraryPath + @"\" + file.Path, FileMode.Create, FileAccess.Write, FileShare.Read)))
                        {
                            sw.Write(result);
                            sw.Close();
                        }

                        // File completed
                        file.Downloading = false;
                        file.Complete = true;
                    }
                }
                else
                {
                    while (!file.Complete)
                    {
                        byte[] getFileReturn;

                        // Call XMDS GetFile
                        using (xmds.xmds xmds = new xmds.xmds())
                        {
                            xmds.Credentials = null;
                            xmds.Url = Settings.Default.XiboClient_xmds_xmds;
                            xmds.UseDefaultCredentials = false;

                            getFileReturn = xmds.GetFile(Settings.Default.ServerKey, _hardwareKey, file.Path, file.FileType, file.ChunkOffset, file.ChunkSize, Settings.Default.Version);
                        }

                        // Set the flag to indicate we have a connection to XMDS
                        Settings.Default.XmdsLastConnection = DateTime.Now;

                        if (file.FileType == "layout")
                        {
                            // Decode this byte[] into a string and stick it in the file.
                            string layoutXml = Encoding.UTF8.GetString(getFileReturn);

                            // Full file is downloaded
                            using (StreamWriter sw = new StreamWriter(File.Open(Settings.Default.LibraryPath + @"\" + file.Path, FileMode.Create, FileAccess.Write, FileShare.Read)))
                            {
                                sw.Write(layoutXml);
                                sw.Close();
                            }

                            file.Complete = true;
                        }
                        else
                        {
                            // Media file
                            // Need to write to the file - in append mode
                            using (FileStream fs = new FileStream(Settings.Default.LibraryPath + @"\" + file.Path, FileMode.Append, FileAccess.Write))
                            {
                                fs.Write(getFileReturn, 0, getFileReturn.Length);
                                fs.Close();
                            }

                            // Increment the offset by the amount we just asked for
                            file.ChunkOffset = file.ChunkOffset + file.ChunkSize;

                            // Has the offset reached the total size?
                            if (file.Size > file.ChunkOffset)
                            {
                                int remaining = file.Size - file.ChunkOffset;

                                // There is still more to come
                                if (remaining < file.ChunkSize)
                                {
                                    // Get the remaining
                                    file.ChunkSize = remaining;
                                }

                                // Part is complete
                                OnPartComplete(file.Id);
                            }
                            else
                            {
                                // File complete
                                file.Complete = true;
                            }
                        }

                        getFileReturn = null;
                    }

                    // File completed
                    file.Downloading = false;

                    // Check MD5
                    string md5 = _requiredFiles.CurrentCacheManager.GetMD5(file.Path);
                    if (file.Md5 == md5)
                    {
                        // Mark it as complete
                        _requiredFiles.MarkComplete(_requiredFileId, file.Md5);

                        // Add it to the cache manager
                        _requiredFiles.CurrentCacheManager.Add(file.Path, file.Md5);

                        Trace.WriteLine(new LogMessage("FileAgent - Run", "File Downloaded Successfully. " + file.Path), LogType.Info.ToString());
                    }
                    else
                    {
                        // Just error - we will pick it up again the next time we download
                        Trace.WriteLine(new LogMessage("FileAgent - Run", "Downloaded file failed MD5 check. Calculated [" + md5 + "] & XMDS [ " + file.Md5 + "] . " + file.Path), LogType.Error.ToString());
                    }
                }

                // Inform the Player thread that a file has been modified.
                OnComplete(file.Id, file.FileType);
            }
            catch (Exception ex)
            {
                // Log this message, but dont abort the thread
                Trace.WriteLine(new LogMessage("FileAgent - Run", "Exception in Run: " + ex.Message), LogType.Error.ToString());

                // Mark as not downloading
                file.Downloading = false;
            }

            // Release the Semaphore
            Trace.WriteLine(new LogMessage("FileAgent - Run", "Releasing Lock"), LogType.Info.ToString());

            _fileDownloadLimit.Release();
        }
    }
}
