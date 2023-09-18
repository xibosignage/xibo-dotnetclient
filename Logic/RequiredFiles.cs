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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

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
        /// Count of files missing
        /// </summary>
        public int FilesMissing
        {
            get
            {
                lock ( _locker)
                {
                    int count = 0;
                    foreach (RequiredFile rf in RequiredFileList)
                    {
                        if (!rf.Complete && !rf.IsWidgetData)
                        {
                            count++;
                        }
                    }

                    return count;
                }
            }
        }

        public RequiredFiles()
        {
            RequiredFileList = new Collection<RequiredFile>();

            // Create a webservice call
            _report = new xmds.xmds
            {
                Credentials = null,
                Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=mediaInventory",
                UseDefaultCredentials = false
            };
        }

        public void AssessAndAddRequiredFile(RequiredFile rf)
        {
            // Does this file exist?
            if (File.Exists(ApplicationSettings.Default.LibraryPath + @"\" + rf.SaveAs))
            {
                // Compare MD5 of the file we currently have, to what we should have
                if (rf.Md5 != CacheManager.Instance.GetMD5(rf.SaveAs))
                {
                    Trace.WriteLine(new LogMessage("RequiredFiles - SetRequiredFiles", "MD5 different for existing file: " + rf.SaveAs), LogType.Info.ToString());

                    // They are different
                    CacheManager.Instance.Remove(rf.SaveAs);

                    // TODO: Resume the file download under certain conditions. Make sure its not bigger than it should be. 
                    // Make sure it is fairly fresh
                    FileInfo info = new FileInfo(ApplicationSettings.Default.LibraryPath + @"\" + rf.SaveAs);

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
                            File.Delete(ApplicationSettings.Default.LibraryPath + @"\" + rf.SaveAs);
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
                    CacheManager.Instance.Add(rf.SaveAs, rf.Md5);
                }
            }
            else
            {
                // File does not exist, therefore remove it from the cache manager (on the off chance that it is in there for some reason)
                CacheManager.Instance.Remove(rf.SaveAs);
            }

            RequiredFileList.Add(rf);
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
                if (rf.FileType == "dependency")
                {
                    rf.DependencyId = attributes["id"].Value;
                    rf.DependencyFileType = attributes["fileType"].Value;
                    rf.Path = attributes["path"].Value;
                    rf.SaveAs = attributes["saveAs"].Value;
                    rf.Http = (attributes["download"].Value == "http");
                    rf.Size = double.Parse(attributes["size"].Value);
                    rf.ChunkSize = 512000;
                }
                else if (rf.FileType == "media")
                {
                    rf.Id = int.Parse(attributes["id"].Value);
                    rf.Path = attributes["path"].Value;
                    rf.SaveAs = (attributes["saveAs"] == null || string.IsNullOrEmpty(attributes["saveAs"].Value)) ? rf.Path : attributes["saveAs"].Value;
                    rf.Http = (attributes["download"].Value == "http");
                    rf.ChunkSize = 512000;
                }
                else if (rf.FileType == "layout")
                {
                    rf.Id = int.Parse(attributes["id"].Value);
                    rf.Path = attributes["path"].Value;
                    rf.Http = (attributes["download"].Value == "http");
                    rf.Size = double.Parse(attributes["size"].Value);

                    if (rf.Http)
                    {
                        rf.SaveAs = attributes["saveAs"].Value;
                    }
                    else
                    {
                        rf.Path = rf.Path + ".xlf";
                        rf.SaveAs = rf.Path;
                    }

                    rf.ChunkSize = rf.Size;

                    // See if we have a LayoutCode
                    if (attributes["code"] != null && !string.IsNullOrEmpty(attributes["code"].Value))
                    {
                        CacheManager.Instance.AddLayoutCode(rf.Id, attributes["code"].Value);
                    }
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
                        rf.SaveAs = rf.Path;

                        // Set the size to something arbitary
                        rf.Size = 10000;

                        // Check to see if this has already been downloaded
                        if (File.Exists(ApplicationSettings.Default.LibraryPath + @"\" + rf.MediaId + ".htm"))
                        {
                            // Has it expired?
                            int updated = 0;

                            try
                            {
                                updated = (attributes["updated"] != null) ? int.Parse(attributes["updated"].Value) : 0;
                            }
                            catch (Exception e)
                            {
                                Debug.WriteLine("Can't read Updated attribute from Resource node. e = " + e.Message, "RequiredFiles");
                            }

                            DateTime updatedDt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                            updatedDt = updatedDt.AddSeconds(updated);

                            DateTime fileUpdatedDt = File.GetLastWriteTimeUtc(ApplicationSettings.Default.LibraryPath + @"\" + rf.MediaId + ".htm");

                            if (fileUpdatedDt > updatedDt)
                            {
                                Debug.WriteLine("Resource node does not need updating. Current: " + fileUpdatedDt + ", XMDS: " + updatedDt + ", updated: " + updated, "RequiredFiles");
                                rf.Complete = true;
                            }
                            else
                            {
                                Debug.WriteLine("Resource node needs updating. Current: " + fileUpdatedDt + ", XMDS: " + updatedDt, "RequiredFiles");
                            }
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
                else if (rf.FileType == "widget")
                {
                    // Add and skip onward
                    rf.Id = int.Parse(attributes["id"].Value);
                    rf.UpdateInterval = attributes["updateInterval"] != null ? int.Parse(attributes["updateInterval"].Value) : 120;
                    RequiredFileList.Add(rf);
                    continue;
                }
                else
                {
                    continue;
                }

                // This stuff only executes for Dependencies/Layout/Files items
                rf.Md5 = attributes["md5"].Value;
                rf.Size = double.Parse(attributes["size"].Value);

                // Does this file already exist in the RF node? We might receive duplicates.
                bool found = false;

                foreach (RequiredFile existingRf in RequiredFileList)
                {
                    if (rf.FileType == "dependency" && rf.FileType == existingRf.FileType)
                    {
                        if (existingRf.DependencyId == rf.DependencyId && existingRf.DependencyFileType == rf.DependencyFileType)
                        {
                            found = true;
                            break;
                        }
                    }
                    else
                    {
                        if (existingRf.Id == rf.Id && existingRf.FileType == rf.FileType)
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (found)
                {
                    Trace.WriteLine(new LogMessage("RequiredFiles - SetRequiredFiles", "Duplicate file detected, ignoring. FileId = " + rf.Id), LogType.Audit.ToString());
                    continue;
                }

                AssessAndAddRequiredFile(rf);
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
                    if (rf.SaveAs == path)
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
                        // We're complete, so we should assume that we're not downloading anything
                        RequiredFileList[i].Downloading = false;

                        // Complete and store MD5
                        RequiredFileList[i].Complete = true;
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
                    using (StreamWriter streamWriter = new StreamWriter(ApplicationSettings.Default.LibraryPath + @"\" + ApplicationSettings.Default.RequiredFilesFile))
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
                    if (rf.FileType == "widget")
                    {
                        // We don't report media inventory for data
                        continue;
                    }
                    else if (rf.FileType == "dependency")
                    {
                        xml += string.Format("<file type=\"{0}\" id=\"{1}\" fileType=\"{2}\" complete=\"{3}\" lastChecked=\"{4}\" />",
                            rf.FileType,
                            rf.DependencyId,
                            rf.DependencyFileType,
                            (rf.Complete ? "1" : "0"),
                            rf.LastChecked.ToString()
                        );
                    }
                    else
                    {
                        xml += string.Format("<file type=\"{0}\" id=\"{1}\" complete=\"{2}\" lastChecked=\"{3}\" />",
                            rf.FileType,
                            rf.Id.ToString(),
                            (rf.Complete ? "1" : "0"),
                            rf.LastChecked.ToString()
                        );
                    }
                }

                xml = string.Format("<files>{0}</files>", xml);

                _report.MediaInventoryAsync(ApplicationSettings.Default.ServerKey, hardwareKey.Key, xml);
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
                using (FileStream fileStream = File.Open(ApplicationSettings.Default.LibraryPath + @"\" + ApplicationSettings.Default.RequiredFilesFile, FileMode.Open))
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
        public string SaveAs;

        public bool Downloading;
        public bool Complete;
        public bool Http;

        public double ChunkOffset;
        public double ChunkSize;
        public double Size;
        public int Retrys;

        // Resource nodes
        public int LayoutId;
        public string RegionId;
        public string MediaId;

        // Dependencies
        public string DependencyId;
        public string DependencyFileType;

        // Data
        public int UpdateInterval;

        public bool IsWidgetData
        {
            get
            {
                return FileType.Equals("widget", StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
