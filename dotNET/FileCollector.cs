/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2006,2007,2008 Daniel Garner and James Packer
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

namespace XiboClient
{
    class FileCollector
    {
        public FileCollector(string xmlString)
        {
            xml = new XmlDocument();

            try
            {
                xml.LoadXml(xmlString);
            }
            catch (Exception e)
            {
                //Log this error
                System.Diagnostics.Debug.WriteLine(e.Message);
            }

            // Get the key for later use
            hardwareKey = new HardwareKey();

            // Make a new filelist collection
            files = new Collection<FileList>();

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
            XmlNodeList fileNodes = xml.SelectNodes("/files/file");

            //Inspect each file we have here
            foreach (XmlNode file in fileNodes)
            {
                XmlAttributeCollection attributes = file.Attributes;
                FileList fileList = new FileList();

                if (attributes["type"].Value == "layout")
                {
                    // Layout
                    string path = attributes["path"].Value;

                    // Does this file exist?
                    if (File.Exists(Properties.Settings.Default.LibraryPath + @"\" + path + ".xlf"))
                    {
                        // Read the current layout into a string
                        String md5sum = "";
                        try
                        {
                            StreamReader sr = new StreamReader(File.Open(Properties.Settings.Default.LibraryPath + @"\" + path + ".xlf", FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

                            md5sum = Hashes.MD5(sr.ReadToEnd() + "\n");

                            sr.Close();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(String.Format("Error opening {0} for MD5 check", Properties.Settings.Default.LibraryPath + @"\" + path + ".xlf"), "FileCollector - CompareAndCollect");
                            System.Diagnostics.Debug.WriteLine(ex.Message);

                            break;
                        }

                        // Now we have the md5, compare it to the md5 in the xml
                        if (attributes["md5"].Value != md5sum)
                        {
                            // They are different 
                            // Get the file and save it
                            fileList.chunkOffset = 0;
                            fileList.chunkSize = 0;
                            fileList.complete = false;
                            fileList.downloading = false;
                            fileList.path = path;
                            fileList.type = "layout";
                            fileList.md5 = attributes["md5"].Value;
                            fileList.retrys = 0;

                            files.Add(fileList);
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

                        files.Add(fileList);
                    }
                }
                else if (attributes["type"].Value == "media")
                {
                    // Media
                    string path = attributes["path"].Value;

                    // Does this media exist?
                    if (!File.Exists(Properties.Settings.Default.LibraryPath + @"\" + path))
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

                        files.Add(fileList);
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

            System.Diagnostics.Debug.WriteLine(String.Format("There are {0} files to get", files.Count.ToString()));

            // Is there anything to get?
            if (files.Count == 0)
            {
                CollectionComplete();
                return;
            }

            // Start with the first file
            currentFile = 0;

            // Preload the first filelist
            currentFileList = files[currentFile];

            // Get the first file
            GetFile();
        }

        void xmdsFile_GetFileCompleted(object sender, XiboClient.xmds.GetFileCompletedEventArgs e)
        {
            // Expect new schedule XML
            if (e.Error != null)
            {
                //There was an error - what do we do?
                // Log it
                System.Diagnostics.Debug.WriteLine(e.Error.Message, "WS Error");

                System.Diagnostics.Trace.WriteLine(String.Format("Error From WebService Get File. File=[{1}], Error=[{0}], Try No [{2}]", e.Error.Message, currentFileList.path, currentFileList.retrys));

                // Retry?
                if (currentFileList.retrys < 5)
                {
                    // Increment the Retrys
                    currentFileList.retrys++;

                    // Try again
                    GetFile();
                }
                else
                {
                    // Blacklist this file
                    string[] mediaPath = currentFileList.path.Split('.');
                    string mediaId = mediaPath[0];

                    BlackList blackList = new BlackList();
                    blackList.Add(currentFileList.path, BlackListType.All, String.Format("Max number of retrys failed. BlackListing for all displays. Error {0}", e.Error.Message));

                    // Move on
                    currentFile++;
                }
            }
            else
            {
                // Set the flag to indicate we have a connection to XMDS
                Properties.Settings.Default.XmdsLastConnection = DateTime.Now;

                // What file type were we getting
                if (currentFileList.type == "layout")
                {  
                    // Decode this byte[] into a string and stick it in the file.
                    string layoutXml = Encoding.UTF8.GetString(e.Result);

                    // MD5 it to make sure it arrived ok
                    string md5sum = Hashes.MD5(layoutXml);

                    if (md5sum != currentFileList.md5)
                    {
                        // We need to get this file again
                    }

                    // We know it is finished and that we need to write to a file
                    try
                    {
                        string fullPath = Properties.Settings.Default.LibraryPath + @"\" + currentFileList.path + ".xlf";
                        
                        StreamWriter sw = new StreamWriter(File.Open(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read), Encoding.UTF8);
                        sw.Write(layoutXml);
                        sw.Close();

                        // This file is complete
                        currentFileList.complete = true;
                    }
                    catch (IOException ex)
                    {
                        //What do we do if we cant open the file stream?
                        System.Diagnostics.Debug.WriteLine(ex.Message, "FileCollector - GetFileCompleted");
                    }

                    // Fire a layout complete event
                    LayoutFileChanged(currentFileList.path + ".xlf");

                    System.Diagnostics.Trace.WriteLine(String.Format("File downloaded: {0}", currentFileList.path), "xmdsFile_GetFileCompleted");

                    currentFile++;
                }
                else
                {
                    // Need to write to the file - in append mode
                    FileStream fs = new FileStream(Properties.Settings.Default.LibraryPath + @"\" + currentFileList.path, FileMode.Append, FileAccess.Write);

                    fs.Write(e.Result, 0, e.Result.Length);
                    fs.Close();
                    fs.Dispose();

                    // Increment the chunkOffset by the amount we just asked for
                    currentFileList.chunkOffset = currentFileList.chunkOffset + currentFileList.chunkSize;

                    // Has the offset reached the total size?
                    if (currentFileList.size > currentFileList.chunkOffset)
                    {
                        int remaining = currentFileList.size - currentFileList.chunkOffset;
                        // There is still more to come
                        if (remaining < currentFileList.chunkSize)
                        {
                            // Get the remaining
                            currentFileList.chunkSize = remaining;
                        }
                    }
                    else
                    {
                        // Do we need to do some sort of MD5 here? To make sure we got what we should have
                        fs = new FileStream(Properties.Settings.Default.LibraryPath + @"\" + currentFileList.path, FileMode.Open, FileAccess.Read);

                        string md5sum = Hashes.MD5(fs);

                        if (md5sum != currentFileList.md5)
                        {
                            // We need to get this file again
                            try
                            {
                                File.Delete(Properties.Settings.Default.LibraryPath + @"\" + currentFileList.path);
                            }
                            catch (Exception ex)
                            {
                                // Unable to delete incorrect file
                                // Hopefully we will overwrite it
                                System.Diagnostics.Debug.WriteLine(ex.Message);
                            }

                            //Reset the chunk offset (otherwise we will try to get this file again - but from the beginning (no so good)
                            currentFileList.chunkOffset = 0;

                            System.Diagnostics.Trace.WriteLine(String.Format("Error getting file {0}, HASH failed. Starting again", currentFileList.path));
                        }
                        else
                        {
                            // This file is complete
                            currentFileList.complete = true;

                            // Fire the File Complete event
                            MediaFileChanged(currentFileList.path);

                            System.Diagnostics.Debug.WriteLine(string.Format("File downloaded: {0}", currentFileList.path));

                            // All the file has been recieved. Move on to the next file.
                            currentFile++;
                        }
                    }
                }

                // Before we get the next file render any waiting events
                System.Windows.Forms.Application.DoEvents();

                GetFile();
            }
        }

        /// <summary>
        /// Gets the files contained within FileList
        /// </summary>
        public void GetFile()
        {
            if (currentFile > (files.Count - 1))
            {
                System.Diagnostics.Debug.WriteLine(String.Format("Finished Recieving {0} files", files.Count));

                // Clean up
                files.Clear();
                xmdsFile.Dispose();                

                // Finished getting this file list
                CollectionComplete();
            }
            else
            {
                // Get the current file into the currentfilelist if the current one is finished
                if (currentFileList.complete)
                {
                    currentFileList = files[currentFile];
                }

                System.Diagnostics.Debug.WriteLine(String.Format("Getting the file : {0} chunk : {1}", currentFileList.path, currentFileList.chunkOffset.ToString()));

                // Request the file
                xmdsFile.GetFileAsync(Properties.Settings.Default.ServerKey, hardwareKey.Key, currentFileList.path, currentFileList.type, currentFileList.chunkOffset, currentFileList.chunkSize, Properties.Settings.Default.Version);

                currentFileList.downloading = true;
            }
        }

        [Serializable]
        private struct FileList
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
        private Collection<FileList> files;
        private int currentFile;
        private FileList currentFileList;
        private xmds.xmds xmdsFile;

        public event LayoutFileChangedDelegate LayoutFileChanged;
        public delegate void LayoutFileChangedDelegate(string layoutPath);

        public event MediaFileChangedDelegate MediaFileChanged;
        public delegate void MediaFileChangedDelegate(string path);

        public event CollectionCompleteDelegate CollectionComplete;
        public delegate void CollectionCompleteDelegate();
    }
}
