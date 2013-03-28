/*
 * Xibo - Digitial Signage - http://www.xibo.org.uk
 * Copyright (C) 2009 Daniel Garner
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
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Diagnostics;
using System.Xml;

namespace XiboClient
{
    public class CacheManager
    {
        public static object _locker = new object();
        public Collection<Md5Resource> _files;

        public CacheManager()
        {
            _files = new Collection<Md5Resource>();
        }

        /// <summary>
        /// Gets the MD5 for the given path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public String GetMD5(String path)
        {
            // Either we already have the MD5 stored
            foreach (Md5Resource file in _files)
            {
                if (file.path == path)
                {
                    // Check to see if this file has been modified since the MD5 cache
                    DateTime lastWrite = File.GetLastWriteTime(Properties.Settings.Default.LibraryPath + @"\" + path);

                    if (lastWrite > file.cacheDate)
                    {
                        Trace.WriteLine(new LogMessage("GetMD5", path + " has been written to since cache, recalculating"), LogType.Audit.ToString());

                        // Get the MD5 again
                        String md5 = CalcMD5(path);

                        // Store the new cacheDate AND the new MD5
                        Remove(path);

                        Add(path, md5);
                        
                        // Return the new MD5
                        return md5;
                    }

                    return file.md5;
                }
            }

            return CalcMD5(path);
        }

        /// <summary>
        /// Calculates the MD5 for a path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string CalcMD5(String path)
        {
            try
            {
                // Open the file and get the MD5
                using (FileStream md5Fs = new FileStream(Properties.Settings.Default.LibraryPath + @"\" + path, FileMode.Open, FileAccess.Read))
                {
                    return Hashes.MD5(md5Fs);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(new LogMessage("CalcMD5", "Unable to calc the MD5 because: " + ex.Message), LogType.Error.ToString());
                
                // Return a 0 MD5 which will immediately invalidate the file
                return "0";
            }
        }

        /// <summary>
        /// Adds a MD5 to the CacheManager
        /// </summary>
        /// <param name="path"></param>
        /// <param name="md5"></param>
        public void Add(String path, String md5)
        {
            lock (_locker)
            {
                // First check to see if this path is in the collection
                foreach (Md5Resource file in _files)
                {
                    if (file.path == path)
                        return;
                }

                // We need to generate the MD5 and store it for later
                Md5Resource md5Resource = new Md5Resource();

                md5Resource.path = path;
                md5Resource.md5 = md5;
                md5Resource.cacheDate = DateTime.Now;

                // Add the resource to the collection
                _files.Add(md5Resource);

                Debug.WriteLine(new LogMessage("Add", "Adding new MD5 to CacheManager"), LogType.Info.ToString());
            }
        }

        /// <summary>
        /// Removes the MD5 resource associated with the Path given
        /// </summary>
        /// <param name="path"></param>
        public void Remove(String path)
        {
            lock (_locker)
            {
                // Loop through all MD5s and remove any that match the path
                for (int i = 0; i < _files.Count; i++)
                {
                    Md5Resource file = _files[i];

                    if (file.path == path)
                    {
                        _files.Remove(file);

                        System.Diagnostics.Debug.WriteLine(new LogMessage("Remove", "Removing stale MD5 from the CacheManager"), LogType.Info.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Writes the CacheManager to disk
        /// </summary>
        public void WriteCacheManager()
        {
            lock (_locker)
            {
                Debug.WriteLine(new LogMessage("CacheManager - WriteCacheManager", "About to Write the Cache Manager"), LogType.Info.ToString());

                try
                {
                    using (StreamWriter streamWriter = new StreamWriter(Application.UserAppDataPath + "\\" + Properties.Settings.Default.CacheManagerFile))
                    {
                        XmlSerializer xmlSerializer = new XmlSerializer(typeof(CacheManager));

                        xmlSerializer.Serialize(streamWriter, this);
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(new LogMessage("MainForm_FormClosing", "Unable to write CacheManager to disk because: " + ex.Message));
                }
            }
        }

        /// <summary>
        /// Is the given URI a valid file?
        /// </summary>
        /// <param name="path"></param>
        /// <returns>True is it is and false if it isnt</returns>
        public bool IsValidPath(String path)
        {
            lock (_locker)
            {
                // Currently a path is valid if it is in the cache
                if (String.IsNullOrEmpty(path))
                    return false;

                // Search for this path
                foreach (Md5Resource file in _files)
                {
                    if (file.path == path)
                    {
                        // If we cached it over 2 minutes ago, then check the GetLastWriteTime
                        if (file.cacheDate > DateTime.Now.AddMinutes(-2))
                            return true;

                        try
                        {
                            // Check to see if this file has been deleted since the Cache Manager registered it
                            if (!File.Exists(Properties.Settings.Default.LibraryPath + @"\" + path))
                                return false;

                            // Check to see if this file has been modified since the MD5 cache
                            // If it has then we assume invalid, otherwise its valid
                            DateTime lastWrite = File.GetLastWriteTime(Properties.Settings.Default.LibraryPath + @"\" + path);

                            if (lastWrite <= file.cacheDate)
                                return true;
                            else
                                return false;
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine(new LogMessage("IsValid", "Unable to determine if the file is valid. Assuming not valid: " + ex.Message), LogType.Error.ToString());

                            // Assume invalid
                            return false;
                        }
                    }
                }

                // Reached the end of the cache and havent found the file.
                return false;
            }
        }

        /// <summary>
        /// Is the provided layout file a valid layout (has all media)
        /// </summary>
        /// <param name="layoutFile"></param>
        /// <returns></returns>
        public bool IsValidLayout(string layoutFile)
        {
            lock (_locker)
            {
                Debug.WriteLine("Checking if Layout " + layoutFile + " is valid");

                if (!IsValidPath(layoutFile))
                    return false;

                
                // Load the XLF, get all media ID's
                XmlDocument layoutXml = new XmlDocument();
                layoutXml.Load(Properties.Settings.Default.LibraryPath + @"\" + layoutFile);

                try
                {
                    XmlNodeList mediaNodes = layoutXml.SelectNodes("//media");

                    // Store some information about the validity of local video to decide if this layout should be valid or not.
                    int countInvalidLocalVideo = 0;

                    foreach (XmlNode media in mediaNodes)
                    {
                        // Is this a stored media type?
                        switch (media.Attributes["type"].Value)
                        {
                            case "video":
                            case "image":
                            case "flash":
                            case "ppt":

                                // Get the path and see if its 
                                if (!IsValidPath(GetUri(media)))
                                {
                                    Trace.WriteLine(new LogMessage("CacheManager - IsValidLayout", "Invalid Media: " + media.Attributes["id"].Value.ToString()), LogType.Audit.ToString());
                                    return false;
                                }

                                break;

                            case "localvideo":

                                // Check that the path they have specified is ok
                                if (!File.Exists(Uri.UnescapeDataString(GetUri(media)).Replace('+', ' ')))
                                {
                                    // Local video path does not exist
                                    Trace.WriteLine(new LogMessage("CacheManager - IsValidLayout", media.InnerText + " does not exist"), LogType.Error.ToString());
                                    countInvalidLocalVideo++;
                                }

                                break;

                            default:
                                continue;
                        }
                    }

                    // If the number of invalid local video elements is equal to the number of elements on the layout, then don't show
                    if (countInvalidLocalVideo == mediaNodes.Count)
                        return false;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(new LogMessage("CacheManager - IsValidLayout", "Exception checking media. " + ex.Message), LogType.Audit.ToString());
                    return false;
                }

                // Also check to see if there is a background image that needs to be downloaded
                try
                {
                    XmlNode layoutNode = layoutXml.SelectSingleNode("/layout");
                    XmlAttributeCollection layoutAttributes = layoutNode.Attributes;

                    if (layoutAttributes["background"] != null && !string.IsNullOrEmpty(layoutAttributes["background"].Value))
                    {
                        if (!IsValidPath(layoutAttributes["background"].Value))
                        {
                            Debug.WriteLine("Invalid background: " + layoutAttributes["background"].Value);
                            return false;
                        }
                    }
                }
                catch
                {
                    // We dont want a missing background attribute to stop this process
                    return true;
                }

                Debug.WriteLine("Layout " + layoutFile + " is valid");

                return true;
            }
        }

        /// <summary>
        /// Get the URI of this media item
        /// </summary>
        /// <param name="media"></param>
        /// <returns></returns>
        private string GetUri(XmlNode media)
        {
            XmlNode uriNode = media.SelectSingleNode(".//options/uri");
            return uriNode.InnerText;
        }

        /// <summary>
        /// Regenerate from Required Files
        /// </summary>
        public void Regenerate()
        {
            lock (_locker)
            {
                if (!File.Exists(Application.UserAppDataPath + "\\" + Properties.Settings.Default.RequiredFilesFile))
                    return;

                // Open the XML file and check each required file that isnt already there
                XmlDocument xml = new XmlDocument();
                xml.Load(Application.UserAppDataPath + "\\" + Properties.Settings.Default.RequiredFilesFile);

                XmlNodeList fileNodes = xml.SelectNodes("//RequiredFile/Path");

                foreach (XmlNode file in fileNodes)
                {
                    string path = file.InnerText;

                    // Make sure every required file is correctly logged in the cache manager
                    // Leave the files that are not required in there to be analysed later
                    if (File.Exists(Properties.Settings.Default.LibraryPath + @"\" + path))
                    {
                        // Add this file to the cache manager
                        Add(path, GetMD5(path));
                    }
                    else
                    {
                        // Remove this file from the cachemanager
                        Remove(path);
                    }
                }
            }
        }
    }

    public struct Md5Resource
    {
        public String md5;
        public String path;
        public DateTime cacheDate;
    }
}
