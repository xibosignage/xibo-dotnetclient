/**
 * Copyright (C) 2023 Xibo Signage Ltd
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
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

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
        private RequiredFiles _requiredFiles;

        /// <summary>
        /// The Required File to download
        /// </summary>
        private RequiredFile _requiredFile;

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
        public FileAgent(RequiredFiles files, RequiredFile file)
        {
            _requiredFiles = files;
            _requiredFile = file;
        }

        /// <summary>
        /// Runs the agent
        /// </summary>
        public void Run()
        {
            Trace.WriteLine(new LogMessage("FileAgent - Run", "Thread Started"), LogType.Audit.ToString());

            // Set downloading to be true
            _requiredFile.Downloading = true;

            // Wait for the Semaphore lock to become available
            _fileDownloadLimit.WaitOne();

            try
            {
                Trace.WriteLine(new LogMessage("FileAgent - Run", "Thread alive and Lock Obtained"), LogType.Audit.ToString());

                if (_requiredFile.FileType == "resource")
                {
                    // Download using XMDS GetResource
                    using (xmds.xmds xmds = new xmds.xmds())
                    {
                        xmds.Credentials = null;
                        xmds.UseDefaultCredentials = true;
                        xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=getResource";

                        string result = xmds.GetResource(ApplicationSettings.Default.ServerKey, ApplicationSettings.Default.HardwareKey, _requiredFile.LayoutId, _requiredFile.RegionId, _requiredFile.MediaId);

                        // Write the result to disk
                        using (FileStream fileStream = File.Open(ApplicationSettings.Default.LibraryPath + @"\" + _requiredFile.SaveAs, FileMode.Create, FileAccess.Write, FileShare.Read))
                        {
                            using (StreamWriter sw = new StreamWriter(fileStream))
                            {
                                sw.Write(result);
                                sw.Close();
                            }
                        }

                        // File completed
                        _requiredFile.Downloading = false;
                        _requiredFile.Complete = true;
                    }
                }
                else if (_requiredFile.Http)
                {
                    // Download using HTTP and the rf.Path
                    using (WebClient wc = new WebClient())
                    {
                        wc.DownloadFile(_requiredFile.Path, ApplicationSettings.Default.LibraryPath + @"\" + _requiredFile.SaveAs);
                    }

                    // File completed
                    _requiredFile.Downloading = false;

                    // Check MD5
                    string md5 = CacheManager.Instance.GetMD5(_requiredFile.SaveAs);
                    if (_requiredFile.Md5 == md5)
                    {
                        // Mark it as complete
                        _requiredFiles.MarkComplete(_requiredFile.Id, _requiredFile.Md5);

                        // Add it to the cache manager
                        CacheManager.Instance.Add(_requiredFile.SaveAs, _requiredFile.Md5);

                        Trace.WriteLine(new LogMessage("FileAgent - Run", "File Downloaded Successfully. " + _requiredFile.SaveAs), LogType.Info.ToString());
                    }
                    else
                    {
                        // Just error - we will pick it up again the next time we download
                        Trace.WriteLine(new LogMessage("FileAgent - Run", "Downloaded file failed MD5 check. Calculated [" + md5 + "] & XMDS [ " + _requiredFile.Md5 + "] . " + _requiredFile.SaveAs), LogType.Info.ToString());
                    }
                }
                else
                {
                    // Download using XMDS GetFile/GetDependency
                    while (!_requiredFile.Complete)
                    {
                        byte[] getFileReturn;

                        // Call XMDS GetFile
                        using (xmds.xmds xmds = new xmds.xmds())
                        {
                            xmds.Credentials = null;
                            xmds.UseDefaultCredentials = false;

                            if (_requiredFile.FileType == "dependency")
                            {
                                xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=getDepencency";
                                getFileReturn = xmds.GetDependency(ApplicationSettings.Default.ServerKey, _hardwareKey, _requiredFile.DependencyFileType, _requiredFile.DependencyId, _requiredFile.ChunkOffset, _requiredFile.ChunkSize);
                            }
                            else
                            {
                                xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=getFile";
                                getFileReturn = xmds.GetFile(ApplicationSettings.Default.ServerKey, _hardwareKey, _requiredFile.Id, _requiredFile.FileType, _requiredFile.ChunkOffset, _requiredFile.ChunkSize);
                            }
                        }

                        // Set the flag to indicate we have a connection to XMDS
                        ApplicationSettings.Default.XmdsLastConnection = DateTime.Now;

                        if (_requiredFile.FileType == "layout")
                        {
                            // Decode this byte[] into a string and stick it in the file.
                            string layoutXml = Encoding.UTF8.GetString(getFileReturn);

                            // Full file is downloaded
                            using (FileStream fileStream = File.Open(ApplicationSettings.Default.LibraryPath + @"\" + _requiredFile.SaveAs, FileMode.Create, FileAccess.Write, FileShare.Read))
                            {
                                using (StreamWriter sw = new StreamWriter(fileStream))
                                {
                                    sw.Write(layoutXml);
                                    sw.Close();
                                }
                            }

                            _requiredFile.Complete = true;
                        }
                        else
                        {
                            // Dependency / Media file
                            // We're OK to use path for dependency as that will be the original file name
                            // Need to write to the file - in append mode
                            using (FileStream fs = new FileStream(ApplicationSettings.Default.LibraryPath + @"\" + _requiredFile.Path, FileMode.Append, FileAccess.Write))
                            {
                                fs.Write(getFileReturn, 0, getFileReturn.Length);
                                fs.Close();
                            }

                            // Increment the offset by the amount we just asked for
                            _requiredFile.ChunkOffset = _requiredFile.ChunkOffset + _requiredFile.ChunkSize;

                            // Has the offset reached the total size?
                            if (_requiredFile.Size > _requiredFile.ChunkOffset)
                            {
                                double remaining = _requiredFile.Size - _requiredFile.ChunkOffset;

                                // There is still more to come
                                if (remaining < _requiredFile.ChunkSize)
                                {
                                    // Get the remaining
                                    _requiredFile.ChunkSize = remaining;
                                }

                                // Part is complete
                                OnPartComplete(_requiredFile.Id);
                            }
                            else
                            {
                                // File complete
                                _requiredFile.Complete = true;
                            }
                        }

                        getFileReturn = null;
                    }

                    // File completed
                    _requiredFile.Downloading = false;

                    // Check MD5
                    string md5 = CacheManager.Instance.GetMD5(_requiredFile.SaveAs);
                    if (_requiredFile.Md5 == md5)
                    {
                        // Mark it as complete
                        _requiredFiles.MarkComplete(_requiredFile.Id, _requiredFile.Md5);

                        // Add it to the cache manager
                        CacheManager.Instance.Add(_requiredFile.SaveAs, _requiredFile.Md5);

                        Trace.WriteLine(new LogMessage("FileAgent - Run", "File Downloaded Successfully. " + _requiredFile.SaveAs), LogType.Info.ToString());
                    }
                    else
                    {
                        // Just error - we will pick it up again the next time we download
                        Trace.WriteLine(new LogMessage("FileAgent - Run", "Downloaded file failed MD5 check. Calculated [" + md5 + "] & XMDS [ " + _requiredFile.Md5 + "] . " + _requiredFile.SaveAs), LogType.Info.ToString());
                    }
                }

                // Inform the Player thread that a file has been modified.
                OnComplete(_requiredFile.Id, _requiredFile.FileType);
            }
            catch (WebException webEx)
            {
                // Remove from the cache manager
                CacheManager.Instance.Remove(_requiredFile.SaveAs);

                // Log this message, but dont abort the thread
                Trace.WriteLine(new LogMessage("FileAgent - Run", "Web Exception in Run: " + webEx.Message), LogType.Info.ToString());

                // Mark as not downloading
                _requiredFile.Downloading = false;
            }
            catch (Exception ex)
            {
                // Remove from the cache manager
                CacheManager.Instance.Remove(_requiredFile.SaveAs);

                // Log this message, but dont abort the thread
                Trace.WriteLine(new LogMessage("FileAgent - Run", "Exception in Run: " + ex.Message), LogType.Error.ToString());

                // Mark as not downloading
                _requiredFile.Downloading = false;
            }

            // Release the Semaphore
            Trace.WriteLine(new LogMessage("FileAgent - Run", "Releasing Lock"), LogType.Audit.ToString());

            _fileDownloadLimit.Release();
        }
    }
}
