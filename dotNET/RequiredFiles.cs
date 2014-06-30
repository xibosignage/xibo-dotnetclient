/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2011-14 Daniel Garner
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
using System.Windows.Forms;
using System.Xml.Serialization;
using XiboClient.Properties;

/// 17/02/12 Dan Enriched to also manage currently downloading files
/// 28/02/12 Dan Changed the way RequiredFiles are updated
/// 09/04/12 Dan Fixed problem with adding an existing file to the cache manager!
/// 16/FEB/14 Dan Changes to understand the new "resource" file type
namespace XiboClient
{
    public class RequiredFiles
    {
        private static object _locker = new object();

        private XmlDocument _requiredFilesXml;
        public Collection<RequiredFile> RequiredFileList;
        private xmds.xmds _report;

        /// <summary>
        /// Files needing download
        /// </summary>
        public int FilesDownloading
        {
            get
            {
                lock (_locker)
                {
                    int count = 0;

                    foreach (RequiredFile rf in RequiredFileList)
                    {
                        if (rf.Downloading)
                            count++;
                    }

                    return count;
                }
            }
        }

        /// <summary>
        /// The Current CacheManager for this Xibo Client
        /// </summary>
        public CacheManager CurrentCacheManager
        {
            get
            {
                return _cacheManager;
            }
            set
            {
                lock (_locker)
                    _cacheManager = value;
            }
        }
        private CacheManager _cacheManager;

        public RequiredFiles()
        {
            RequiredFileList = new Collection<RequiredFile>();

            // Create a webservice call
            _report = new XiboClient.xmds.xmds();

            // Start up the Xmds Service Object
            _report.Credentials = null;
            _report.Url = Properties.Settings.Default.XiboClient_xmds_xmds;
            _report.UseDefaultCredentials = false;
        }

        /// <summary>
        /// Set required files from the XML document
        /// </summary>
        private void SetRequiredFiles()
        {
            // Itterate through the RF XML and populate the RF collection
            XmlNodeList fileNodes = _requiredFilesXml.SelectNodes("/files/file");

            foreach (XmlNode file in fileNodes)
            {
                RequiredFile rf = new RequiredFile(); 
                
                XmlAttributeCollection attributes = file.Attributes;

                rf.FileType = attributes["type"].Value;
                rf.Downloading = false;
                rf.Complete = false;
                rf.LastChecked = DateTime.Now;
                rf.ChunkOffset = 0;
                rf.ChunkSize = 0;

                // Fill in some information that we already know
                if (rf.FileType == "media")
                {
                    string[] filePart = attributes["path"].Value.Split('.');
                    rf.Id = int.Parse(filePart[0]);
                    rf.Path = attributes["path"].Value;
                    rf.ChunkSize = 512000;
                }
                else if (rf.FileType == "layout")
                {
                    rf.Id = int.Parse(attributes["path"].Value);
                    rf.Path = attributes["path"].Value + ".xlf";
                    rf.ChunkSize = rf.Size;
                }
                else if (rf.FileType == "resource")
                {
                    // Do something special here. Check to see if the resource file already exists otherwise add to RF
                    try
                    {
                        // Set the ID to be some random number
                        rf.Id = int.Parse(attributes["id"].Value);
                        rf.LayoutId = int.Parse(attributes["layoutid"].Value);
                        rf.RegionId = attributes["regionid"].Value;
                        rf.MediaId = attributes["mediaid"].Value;
                        rf.Path = rf.MediaId + ".htm";
                        
                        // Set the size to something arbitary
                        rf.Size = 10000;

                        // Check to see if this has already been downloaded
                        if (File.Exists(Settings.Default.LibraryPath + @"\" + rf.MediaId + ".htm"))
                        {
                            // Has it expired?
                            int updated = 0;

                            try
                            {
                                updated = int.Parse(attributes["updated"].Value);
                            }
                            catch (Exception) {}

                            DateTime updatedDt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                            updatedDt = updatedDt.AddSeconds(updated);

                            if (File.GetLastWriteTimeUtc(Settings.Default.LibraryPath + @"\" + rf.MediaId + ".htm") > updatedDt)
                                rf.Complete = true;
                        }

                        // Add to the Rf Node
                        RequiredFileList.Add(rf);
                        continue;
                    }
                    catch
                    {
                        // Forget about this resource
                        continue;
                    }
                }
                else
                    continue;

                // This stuff only executes for Layout/Files items
                rf.Md5 = attributes["md5"].Value;
                rf.Size = int.Parse(attributes["size"].Value);

                Trace.WriteLine(new LogMessage("RequiredFiles - SetRequiredFiles", "Building required file node for " + rf.Id.ToString()), LogType.Audit.ToString());

                // Does this file already exist in the RF node? We might receive duplicates.
                bool found = false;

                foreach (RequiredFile existingRf in RequiredFileList)
                {
                    if (existingRf.Id == rf.Id && existingRf.FileType == rf.FileType)
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    Trace.WriteLine(new LogMessage("RequiredFiles - SetRequiredFiles", "Duplicate file detected, ignoring. FileId = " + rf.Id), LogType.Audit.ToString());
                    continue;
                }

                // Does this file exist?
                if (File.Exists(Settings.Default.LibraryPath + @"\" + rf.Path))
                {
                    // Compare MD5 of the file we currently have, to what we should have
                    if (rf.Md5 != _cacheManager.GetMD5(rf.Path))
                    {
                        Trace.WriteLine(new LogMessage("RequiredFiles - SetRequiredFiles", "MD5 different for existing file: " + rf.Path), LogType.Info.ToString());

                        // They are different
                        _cacheManager.Remove(rf.Path);

                        // TODO: Resume the file download under certain conditions. Make sure its not bigger than it should be. 
                        // Make sure it is fairly fresh
                        FileInfo info = new FileInfo(Settings.Default.LibraryPath + @"\" + rf.Path);

                        if (info.Length < rf.Size && info.LastWriteTime > DateTime.Now.AddDays(-1))
                        {
                            // Continue the file
                            rf.ChunkOffset = (int)info.Length;
                        }
                        else
                        {
                            // Delete the old file as it is wrong
                            try
                            {
                                File.Delete(Properties.Settings.Default.LibraryPath + @"\" + rf.Path);
                            }
                            catch (Exception ex)
                            {
                                Trace.WriteLine(new LogMessage("CompareAndCollect", "Unable to delete incorrect file because: " + ex.Message));
                            }
                        }
                    }
                    else
                    {
                        // The MD5 is equal - we already have an up to date version of this file.
                        rf.Complete = true;
                        _cacheManager.Add(rf.Path, rf.Md5);
                    }
                }
                else
                {
                    // File does not exist, therefore remove it from the cache manager (on the off chance that it is in there for some reason)
                    _cacheManager.Remove(rf.Path);
                }

                RequiredFileList.Add(rf);
            }
        }

        /// <summary>
        /// Required Files XML
        /// </summary>
        public XmlDocument RequiredFilesXml
        {
            set
            {
                lock (_locker)
                {
                    _requiredFilesXml = value;
                    SetRequiredFiles();
                }
            }
        }

        /// <summary>
        /// Get Required File using the ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public RequiredFile GetRequiredFile(int id, string fileType)
        {
            lock (_locker)
            {
                foreach (RequiredFile rf in RequiredFileList)
                {
                    if (rf.Id == id && rf.FileType == fileType)
                        return rf;
                }

                throw new FileNotFoundException("No required file found with ID: " + id.ToString() + " and type" + fileType);
            }
        }

        /// <summary>
        /// Get Required File using Path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public RequiredFile GetRequiredFile(string path)
        {
            lock (_locker)
            {
                foreach (RequiredFile rf in RequiredFileList)
                {
                    if (rf.Path == path)
                        return rf;
                }

                throw new FileNotFoundException("No required file found with Path: " + path);
            }
        }

        /// <summary>
        /// Mark a RequiredFile as complete
        /// </summary>
        /// <param name="id"></param>
        /// <param name="md5"></param>
        public void MarkComplete(int id, string md5)
        {
            lock (_locker)
            {
                for (int i = 0; i < RequiredFileList.Count; i++)
                {
                    if (RequiredFileList[i].Id == id)
                    {
                        RequiredFileList[i].Complete = true;
                        RequiredFileList[i].Md5 = md5;

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Mark a RequiredFile as incomplete
        /// </summary>
        /// <param name="id"></param>
        /// <param name="md5"></param>
        public void MarkIncomplete(int id, string md5)
        {
            lock (_locker)
            {
                for (int i = 0; i < RequiredFileList.Count; i++)
                {
                    if (RequiredFileList[i].Id == id)
                    {
                        RequiredFileList[i].Complete = false;
                        RequiredFileList[i].Md5 = md5;

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Writes Required Files to disk
        /// </summary>
        public void WriteRequiredFiles()
        {
            lock (_locker)
            {
                Debug.WriteLine(new LogMessage("RequiredFiles - WriteRequiredFiles", "About to Write RequiredFiles"), LogType.Info.ToString());

                try
                {
                    using (StreamWriter streamWriter = new StreamWriter(Application.UserAppDataPath + "\\" + Properties.Settings.Default.RequiredFilesFile))
                    {
                        XmlSerializer xmlSerializer = new XmlSerializer(typeof(RequiredFiles));

                        xmlSerializer.Serialize(streamWriter, this);
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(new LogMessage("RequiredFiles - WriteRequiredFiles", "Unable to write RequiredFiles to disk because: " + ex.Message));
                }
            }
        }

        /// <summary>
        /// Report Required Files to XMDS
        /// </summary>
        public void ReportInventory()
        {
            lock (_locker)
            {
                Trace.WriteLine(new LogMessage("RequiredFiles - ReportInventory", "Reporting Inventory"), LogType.Info.ToString());

                HardwareKey hardwareKey = new HardwareKey();

                // Build the XML required by media file
                string xml = "";

                foreach (RequiredFile rf in RequiredFileList)
                {
                    xml += string.Format("<file type=\"{0}\" id=\"{1}\" complete=\"{2}\" lastChecked=\"{3}\" md5=\"{4}\" />",
                        rf.FileType, rf.Id.ToString(), (rf.Complete) ? "1" : "0", rf.LastChecked.ToString(), rf.Md5);
                }

                xml = string.Format("<files clientType=\"windows\" clientVersion=\"{2}\" clientCode=\"{3}\" macAddress=\"{1}\">{0}</files>", xml, hardwareKey.MacAddress, Settings.Default.ClientVersion, Settings.Default.ClientCodeVersion.ToString());

                _report.MediaInventoryAsync(Properties.Settings.Default.Version, Properties.Settings.Default.ServerKey,
                    hardwareKey.Key, xml);
            }
        }

        /// <summary>
        /// Load Required Files from Disk
        /// </summary>
        /// <returns></returns>
        public static RequiredFiles LoadFromDisk()
        {
            lock (_locker)
            {
                using (FileStream fileStream = File.Open(Application.UserAppDataPath + "\\" + Properties.Settings.Default.RequiredFilesFile, FileMode.Open))
                {
                    XmlSerializer xmlSerializer = new XmlSerializer(typeof(RequiredFiles));

                    return (RequiredFiles)xmlSerializer.Deserialize(fileStream);
                }
            }
        }
    }

    public class RequiredFile
    {
        public string FileType;
        public int Id;
        public DateTime LastChecked;
        public string Md5;
        public string Path;

        public bool Downloading;
        public bool Complete;

        public int ChunkOffset;
        public int ChunkSize;
        public int Size;
        public int Retrys;

        // Resource nodes
        public int LayoutId;
        public string RegionId;
        public string MediaId;
    }
}
