/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006 - 2010 Daniel Garner and James Packer
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
using System.Collections.ObjectModel;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Xml;
using System.Diagnostics;

namespace XiboClient
{
    class FileCollector
    {
        private CacheManager _cacheManager;
        private RequiredFiles _requiredFiles;
        private XmlDocument _xml;

        public FileCollector(CacheManager cacheManager, string xmlString)
        {
            _cacheManager = cacheManager;

            // Load the XML file RF call
            _xml = new XmlDocument();
            _xml.LoadXml(xmlString);

            // Create a required files object
            _requiredFiles = new RequiredFiles();
            _requiredFiles.RequiredFilesXml = _xml;

            // Get the key for later use
            hardwareKey = new HardwareKey();

            // Make a new filelist collection
            _files = new Collection<RequiredFile>();

            // Create a webservice call
            xmdsFile = new XiboClient.xmds.xmds();

            // Start up the Xmds Service Object
            xmdsFile.Credentials = null;
            xmdsFile.Url = Properties.Settings.Default.XiboClient_xmds_xmds;
            xmdsFile.UseDefaultCredentials = false;

            // Hook onto the xmds file complete event
            xmdsFile.GetFileCompleted += new XiboClient.xmds.GetFileCompletedEventHandler(xmdsFile_GetFileCompleted);
        }

        /// <summary>
        /// Compares the xml file list with the files currently in the library
        /// Downloads any missing files
        /// For file types of Layout will fire a LayoutChanged event, giving the filename of the layout changed
        /// </summary>
        public void CompareAndCollect()
        {
            XmlNodeList fileNodes = _xml.SelectNodes("/files/file");

            //Inspect each file we have here
            foreach (XmlNode file in fileNodes)
            {
                XmlAttributeCollection attributes = file.Attributes;
                RequiredFile fileList = new RequiredFile();

                if (attributes["type"].Value == "layout")
                {
                    // Layout
                    string path = attributes["path"].Value;

                    // Does this file exist?
                    if (File.Exists(Properties.Settings.Default.LibraryPath + @"\" + path + ".xlf"))
                    {
                        // Calculate a MD5 for the current file
                        String md5 = _cacheManager.GetMD5(path + ".xlf");

                        System.Diagnostics.Debug.WriteLine(String.Format("Comparing current MD5 [{0}] with given MD5 [{1}]", md5, attributes["md5"].Value));

                        // Now we have the md5, compare it to the md5 in the xml
                        if (attributes["md5"].Value != md5)
                        {
                            // They are different
                            _cacheManager.Remove(path + ".xlf");

                            //TODO: This might be bad! Delete the old layout as it is wrong
                            try
                            {
                                File.Delete(Properties.Settings.Default.LibraryPath + @"\" + path + ".xlf");
                            }
                            catch (Exception ex)
                            {
                                Trace.WriteLine(new LogMessage("CompareAndCollect", "Unable to delete incorrect file because: " + ex.Message));
                            }

                            // Get the file and save it
                            fileList.chunkOffset = 0;
                            fileList.chunkSize = 0;
                            fileList.complete = false;
                            fileList.downloading = false;
                            fileList.path = path;
                            fileList.type = "layout";
                            fileList.md5 = attributes["md5"].Value;
                            fileList.retrys = 0;

                            _files.Add(fileList);
                        }
                        else
                        {
                            // The MD5 of the current file and the MD5 in RequiredFiles are the same.
                            // Therefore make sure this MD5 is in the CacheManager
                            _cacheManager.Add(path + ".xlf", md5);

                            _requiredFiles.MarkComplete(int.Parse(path), md5); 
                        }
                    }
                    else
                    {
                        // No - get the file and save it (no chunks)
                        fileList.chunkOffset = 0;
                        fileList.chunkSize = 0;
                        fileList.complete = false;
                        fileList.downloading = false;
                        fileList.path = path;
                        fileList.type = "layout";
                        fileList.md5 = attributes["md5"].Value;
                        fileList.retrys = 0;

                        _files.Add(fileList);
                    }
                }
                else if (attributes["type"].Value == "media")
                {
                    // Media
                    string path = attributes["path"].Value;

                    // Does this media exist?
                    if (File.Exists(Properties.Settings.Default.LibraryPath + @"\" + path))
                    {
                        String md5 = _cacheManager.GetMD5(path);

                        System.Diagnostics.Debug.WriteLine(String.Format("Comparing current MD5 [{0}] with given MD5 [{1}]", md5, attributes["md5"].Value));

                        // MD5 the file to make sure it is the same.
                        if (md5 != attributes["md5"].Value)
                        {
                            // File changed
                            _cacheManager.Remove(path);

                            // Delete the old media as it is wrong
                            try
                            {
                                File.Delete(Properties.Settings.Default.LibraryPath + @"\" + path);
                            }
                            catch (Exception ex)
                            {
                                Trace.WriteLine(new LogMessage("CompareAndCollect", "Unable to delete incorrect file because: " + ex.Message));
                            }

                            // Add to queue
                            fileList.chunkOffset = 0;
                            fileList.chunkSize = 512000;
                            fileList.complete = false;
                            fileList.downloading = false;
                            fileList.path = path;
                            fileList.type = "media";
                            fileList.size = int.Parse(attributes["size"].Value);
                            fileList.md5 = attributes["md5"].Value;
                            fileList.retrys = 0;

                            _files.Add(fileList);
                        }
                        else
                        {
                            // The MD5 of the current file and the MD5 in RequiredFiles are the same.
                            // Therefore make sure this MD5 is in the CacheManager
                            _cacheManager.Add(path, md5);

                            string[] filePart = path.Split('.');
                            _requiredFiles.MarkComplete(int.Parse(filePart[0]), md5); 
                        }
                    }
                    else
                    {
                        // No - Get it (async call - with chunks... through another class?)
                        fileList.chunkOffset = 0;
                        fileList.chunkSize = 512000;
                        fileList.complete = false;
                        fileList.downloading = false;
                        fileList.path = path;
                        fileList.type = "media";
                        fileList.size = int.Parse(attributes["size"].Value);
                        fileList.md5 = attributes["md5"].Value;
                        fileList.retrys = 0;

                        _files.Add(fileList);
                    }
                }
                else if (attributes["type"].Value == "blacklist")
                {
                    // Expect <file type="blacklist"><file id="" /></file>
                    XmlNodeList items = file.ChildNodes;

                    BlackList blackList = new BlackList();

                    try { blackList.Truncate(); }
                    catch { }
                    
                    if (items.Count > 0)
                    {
                        blackList.Add(items);

                        blackList.Dispose();
                        blackList = null;
                    }

                    items = null;
                }
                else
                {
                    //Ignore node
                }
            }

            Debug.WriteLine(String.Format("There are {0} files to get", _files.Count.ToString()));

            // Output a list of the files we need to get
            string debugMessage = "";

            foreach (RequiredFile fileToGet in _files)
                debugMessage += string.Format("File: {0}, Type: {1}, MD5: {2}. ", fileToGet.path, fileToGet.type, fileToGet.md5);

            Debug.WriteLine(debugMessage);

            // Report the files files back to XMDS
            _requiredFiles.ReportInventory();

            // Write Required Files
            _requiredFiles.WriteRequiredFiles();

            // Is there anything to get?
            if (_files.Count == 0)
            {
                CollectionComplete();
                return;
            }

            // Start with the first file
            _currentFile = 0;

            // Preload the first filelist
            _currentFileList = _files[_currentFile];

            // Get the first file
            GetFile();
        }

        void xmdsFile_GetFileCompleted(object sender, XiboClient.xmds.GetFileCompletedEventArgs e)
        {
            Debug.WriteLine("Get File Completed");

            try
            {
                // Success / Failure
                if (e.Error != null)
                {
                    // There was an error - what do we do?
                    if (e.Cancelled)
                    {
                        Debug.WriteLine("The GetFile Request has been cancelled.");
                    }
                    else
                    {
                        Debug.WriteLine("The GetFile Request is still active, cancelling.");

                        // Make sure we cancel the request
                        xmdsFile.CancelAsync(e.UserState);
                    }

                    // Log it
                    Trace.WriteLine(String.Format("Error From WebService Get File. File=[{1}], Error=[{0}], Try No [{2}]", e.Error.Message, _currentFileList.path, _currentFileList.retrys));

                    // Retry?
                    //TODO: What if we are disconnected from XMDS?
                    if (_currentFileList.retrys < 5)
                    {
                        // Increment the Retrys
                        _currentFileList.retrys++;

                        // Try again
                        GetFile();
                    }
                    else
                    {
                        // Delete the file
                        try
                        {
                            if (_currentFileList.type == "layout")
                                File.Delete(Properties.Settings.Default.LibraryPath + @"\" + _currentFileList.path + ".xlf");
                            else
                                File.Delete(Properties.Settings.Default.LibraryPath + @"\" + _currentFileList.path);
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine(new LogMessage("xmdsFile_GetFileCompleted", "Unable to delete the failed file: " + 
                                _currentFileList.path + " Message: " + ex.Message));
                        }

                        // Removed this blacklist code. Files that are not in the cachemanager will not be played on the client (and therefore
                        // we wont try to play a corrupt / partial file).
                        // If we blacklist here we will never try to get this file again, until the blacklist is cleared in XMDS
                        // Better to just skip it for now, and retry it once we have the required files list again

                        /*// Blacklist this file?
                        string[] mediaPath = _currentFileList.path.Split('.');
                        string mediaId = mediaPath[0];

                        BlackList blackList = new BlackList();
                        blackList.Add(mediaId, BlackListType.Single, String.Format("Max number of retrys failed. BlackListing for this display. Error: {0}", e.Error.Message));
                        */

                        // Move on
                        _currentFileList.complete = true;
                        _currentFile++;
                    }
                }
                else
                {
                    // Set the flag to indicate we have a connection to XMDS
                    Properties.Settings.Default.XmdsLastConnection = DateTime.Now;

                    // What file type were we getting
                    if (_currentFileList.type == "layout")
                    {
                        // Decode this byte[] into a string and stick it in the file.
                        string layoutXml = Encoding.UTF8.GetString(e.Result);
                       

                        // We know it is finished and that we need to write to a file
                        try
                        {
                            string fullPath = Properties.Settings.Default.LibraryPath + @"\" + _currentFileList.path + ".xlf";

                            StreamWriter sw = new StreamWriter(File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read));
                            sw.Write(layoutXml);
                            sw.Close();

                            // This file is complete
                            _currentFileList.complete = true;
                        }
                        catch (IOException ex)
                        {
                            //What do we do if we cant open the file stream?
                            System.Diagnostics.Debug.WriteLine(ex.Message, "FileCollector - GetFileCompleted");
                        }

                        // Check it
                        String md5sum = _cacheManager.GetMD5(_currentFileList.path + ".xlf");

                        System.Diagnostics.Debug.WriteLine(String.Format("Comparing MD5 of completed download [{0}] with given MD5 [{1}]", md5sum, _currentFileList.md5));

                        // TODO: What if the MD5 is different?
                        if (md5sum != _currentFileList.md5)
                        {
                            // Error
                            System.Diagnostics.Trace.WriteLine(new LogMessage("xmdsFile_GetFileCompleted", String.Format("Incorrect MD5 for file: {0}", _currentFileList.path)));
                        }
                        else
                        {
                            // Add to the CacheManager
                            _cacheManager.Add(_currentFileList.path + ".xlf", md5sum);

                            // Report this completion back to XMDS
                            _requiredFiles.MarkComplete(int.Parse(_currentFileList.path), md5sum);
                            _requiredFiles.ReportInventory();
                        }

                        // Fire a layout complete event
                        LayoutFileChanged(_currentFileList.path + ".xlf");

                        System.Diagnostics.Trace.WriteLine(String.Format("File downloaded: {0}", _currentFileList.path), "xmdsFile_GetFileCompleted");

                        _currentFile++;
                    }
                    else
                    {
                        // Need to write to the file - in append mode
                        FileStream fs = new FileStream(Properties.Settings.Default.LibraryPath + @"\" + _currentFileList.path, FileMode.Append, FileAccess.Write);

                        fs.Write(e.Result, 0, e.Result.Length);
                        fs.Close();
                        fs.Dispose();

                        // Increment the chunkOffset by the amount we just asked for
                        _currentFileList.chunkOffset = _currentFileList.chunkOffset + _currentFileList.chunkSize;

                        // Has the offset reached the total size?
                        if (_currentFileList.size > _currentFileList.chunkOffset)
                        {
                            int remaining = _currentFileList.size - _currentFileList.chunkOffset;
                            // There is still more to come
                            if (remaining < _currentFileList.chunkSize)
                            {
                                // Get the remaining
                                _currentFileList.chunkSize = remaining;
                            }
                        }
                        else
                        {
                            String md5sum = _cacheManager.GetMD5(_currentFileList.path);

                            System.Diagnostics.Debug.WriteLine(String.Format("Comparing MD5 of completed download [{0}] with given MD5 [{1}]", md5sum, _currentFileList.md5));

                            if (md5sum != _currentFileList.md5)
                            {
                                // We need to get this file again
                                try
                                {
                                    File.Delete(Properties.Settings.Default.LibraryPath + @"\" + _currentFileList.path);
                                }
                                catch (Exception ex)
                                {
                                    // Unable to delete incorrect file
                                    //TODO: Should we black list this file?
                                    System.Diagnostics.Debug.WriteLine(ex.Message);
                                }

                                // Reset the chunk offset (otherwise we will try to get this file again - but from the beginning (no so good)
                                _currentFileList.chunkOffset = 0;

                                System.Diagnostics.Trace.WriteLine(String.Format("Error getting file {0}, HASH failed. Starting again", _currentFileList.path));
                            }
                            else
                            {
                                // Add the MD5 to the CacheManager
                                _cacheManager.Add(_currentFileList.path, md5sum);

                                // This file is complete
                                _currentFileList.complete = true;

                                // Fire the File Complete event
                                MediaFileChanged(_currentFileList.path);

                                System.Diagnostics.Debug.WriteLine(string.Format("File downloaded: {0}", _currentFileList.path));

                                // Report this completion back to XMDS
                                string[] filePart = _currentFileList.path.Split('.');
                                _requiredFiles.MarkComplete(int.Parse(filePart[0]), md5sum);
                                _requiredFiles.ReportInventory();

                                // All the file has been recieved. Move on to the next file.
                                _currentFile++;
                            }
                        }
                    }

                    // Before we get the next file render any waiting events
                    System.Windows.Forms.Application.DoEvents();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("xmdsFile_GetFileCompleted", "Unhanded Exception when processing getFile response: " + ex.Message));

                // TODO: Blacklist the file?

                // Consider this file complete because we couldn't write it....
                _currentFileList.complete = true;
                _currentFile++;
            }

            // Get the next file
            GetFile();
        }

        /// <summary>
        /// Gets the files contained within FileList
        /// </summary>
        public void GetFile()
        {
            if (_currentFile > (_files.Count - 1))
            {
                System.Diagnostics.Debug.WriteLine(String.Format("Finished Receiving {0} files", _files.Count));

                // Clean up
                _files.Clear();
                xmdsFile.Dispose();
             
                // Write Required Files
                _requiredFiles.WriteRequiredFiles();

                // Finished getting this file list
                CollectionComplete();
            }
            else
            {
                // Get the current file into the currentfilelist if the current one is finished
                if (_currentFileList.complete)
                {
                    _currentFileList = _files[_currentFile];
                }

                System.Diagnostics.Debug.WriteLine(String.Format("Getting the file : {0} chunk : {1}", _currentFileList.path, _currentFileList.chunkOffset.ToString()));

                // Request the file
                xmdsFile.GetFileAsync(Properties.Settings.Default.ServerKey, hardwareKey.Key, _currentFileList.path, _currentFileList.type, _currentFileList.chunkOffset, _currentFileList.chunkSize, Properties.Settings.Default.Version);

                _currentFileList.downloading = true;
            }
        }

        [Serializable]
        private struct RequiredFile
        {
            public string path;
            public string type;
            public bool downloading;
            public bool complete;
            public int chunkOffset;
            public int chunkSize;
            public int size;
            public string md5;
            public int retrys;
        }

        private XmlDocument xml;
        private HardwareKey hardwareKey;
        private Collection<RequiredFile> _files;
        private int _currentFile;
        private RequiredFile _currentFileList;
        private xmds.xmds xmdsFile;

        public event LayoutFileChangedDelegate LayoutFileChanged;
        public delegate void LayoutFileChangedDelegate(string layoutPath);

        public event MediaFileChangedDelegate MediaFileChanged;
        public delegate void MediaFileChangedDelegate(string path);

        public event CollectionCompleteDelegate CollectionComplete;
        public delegate void CollectionCompleteDelegate();
    }
}
