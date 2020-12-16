/**
 * Copyright (C) 2020 Xibo Signage Ltd
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
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using XiboClient.Log;

namespace XiboClient
{
    public sealed class CacheManager
    {
        private static readonly Lazy<CacheManager>
            lazy =
            new Lazy<CacheManager>
            (() => new CacheManager());

        public static CacheManager Instance
            => lazy.Value;

        private readonly object _locker = new object();

        /// <summary>
        /// Files under cache management
        /// </summary>
        private Collection<Md5Resource> _files = new Collection<Md5Resource>();

        /// <summary>
        /// Unsafe items
        /// </summary>
        private Collection<UnsafeItem> _unsafeItems = new Collection<UnsafeItem>();


        private CacheManager()
        {
        }

        /// <summary>
        /// Sets the CacheManager
        /// </summary>
        public void SetCacheManager()
        {
            lock (_locker)
            {
                try
                {
                    // Deserialise a saved cache manager, and use its files to set our instance.
                    using (FileStream fileStream = File.Open(ApplicationSettings.Default.LibraryPath + @"\" + ApplicationSettings.Default.CacheManagerFile, FileMode.Open))
                    {
                        XmlSerializer xmlSerializer = new XmlSerializer(typeof(CacheManager));

                        CacheManager manager = (CacheManager)xmlSerializer.Deserialize(fileStream);

                        // Set its files on ourselves
                        Instance._files = manager._files;
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(new LogMessage("CacheManager", "Unable to reuse the Cache Manager because: " + ex.Message));
                }

                try
                {
                    Instance.Regenerate();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(new LogMessage("CacheManager", "Regenerate failed because: " + ex.Message));
                }
            }
        }

        /// <summary>
        /// Gets the MD5 for the given path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public string GetMD5(string path)
        {
            // Either we already have the MD5 stored
            foreach (Md5Resource file in _files)
            {
                if (file.path == path)
                {
                    // Check to see if this file has been modified since the MD5 cache
                    DateTime lastWrite = File.GetLastWriteTime(ApplicationSettings.Default.LibraryPath + @"\" + path);

                    if (lastWrite > file.cacheDate)
                    {
                        Trace.WriteLine(new LogMessage("GetMD5", path + " has been written to since cache, recalculating"), LogType.Audit.ToString());

                        // Get the MD5 again
                        string md5 = CalcMD5(path);

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
        private string CalcMD5(string path)
        {
            try
            {
                // Open the file and get the MD5
                using (FileStream md5Fs = new FileStream(ApplicationSettings.Default.LibraryPath + @"\" + path, FileMode.Open, FileAccess.Read))
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
        public void Add(string path, string md5)
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
        public void Remove(string path)
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
                    using (StreamWriter streamWriter = new StreamWriter(ApplicationSettings.Default.LibraryPath + @"\" + ApplicationSettings.Default.CacheManagerFile))
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
        public bool IsValidPath(string path)
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
                            return File.Exists(ApplicationSettings.Default.LibraryPath + @"\" + path);

                        try
                        {
                            // Check to see if this file has been deleted since the Cache Manager registered it
                            if (!File.Exists(ApplicationSettings.Default.LibraryPath + @"\" + path))
                                return false;

                            // Check to see if this file has been modified since the MD5 cache
                            // If it has then we assume invalid, otherwise its valid
                            DateTime lastWrite = File.GetLastWriteTime(ApplicationSettings.Default.LibraryPath + @"\" + path);

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
        /// Regenerate from Required Files
        /// </summary>
        public void Regenerate()
        {
            lock (_locker)
            {
                if (!File.Exists(ApplicationSettings.Default.LibraryPath + @"\" + ApplicationSettings.Default.RequiredFilesFile))
                    return;

                // Open the XML file and check each required file that isnt already there
                XmlDocument xml = new XmlDocument();
                xml.Load(ApplicationSettings.Default.LibraryPath + @"\" + ApplicationSettings.Default.RequiredFilesFile);

                XmlNodeList fileNodes = xml.SelectNodes("//RequiredFile/SaveAs");

                foreach (XmlNode file in fileNodes)
                {
                    string path = file.InnerText;

                    // Make sure every required file is correctly logged in the cache manager
                    // Leave the files that are not required in there to be analysed later
                    if (File.Exists(ApplicationSettings.Default.LibraryPath + @"\" + path))
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

        #region Unsafe List

        /// <summary>
        /// Add an unsafe item to the list
        /// </summary>
        /// <param name="type"></param>
        /// <param name="layoutId"></param>
        /// <param name="id"></param>
        /// <param name="reason"></param>
        public void AddUnsafeItem(UnsafeItemType type, int layoutId, string id, string reason)
        {
            AddUnsafeItem(type, layoutId, id, reason, 86400);
        }

        /// <summary>
        /// Add an unsafe item to the list
        /// </summary>
        /// <param name="type"></param>
        /// <param name="layoutId"></param>
        /// <param name="id"></param>
        /// <param name="reason"></param>
        /// <param name="ttl"></param>
        public void AddUnsafeItem(UnsafeItemType type, int layoutId, string id, string reason, int ttl)
        {
            if (ttl == 0)
            {
                ttl = 86400;
            }

            try
            {
                UnsafeItem item = _unsafeItems
                    .Where(i => i.Type == type && i.LayoutId == layoutId && i.Id == id)
                    .First();

                item.DateTime = DateTime.Now;
                item.Reason = reason;
            }
            catch
            {
                _unsafeItems.Add(new UnsafeItem
                {
                    DateTime = DateTime.Now,
                    Type = type,
                    LayoutId = layoutId,
                    Id = id,
                    Reason = reason,
                    Ttl = ttl
                });
            }

            ClientInfo.Instance.UpdateUnsafeList(UnsafeListAsString());
        }

        /// <summary>
        /// Remove a Layout from the unsafe items list
        /// </summary>
        /// <param name="layoutId"></param>
        public void RemoveUnsafeLayout(int layoutId)
        {
            foreach (UnsafeItem item in _unsafeItems)
            {
                if (item.LayoutId == layoutId)
                {
                    _unsafeItems.Remove(item);
                }
            }

            ClientInfo.Instance.UpdateUnsafeList(UnsafeListAsString());
        }

        /// <summary>
        /// Is the provided layout unsafe?
        /// </summary>
        /// <param name="layoutId">ID of the Layout to test</param>
        /// <returns></returns>
        public bool IsUnsafeLayout(int layoutId)
        {
            bool updateList = false;
            bool found = false;

            for (int i = _unsafeItems.Count - 1; i >= 0; i--)
            {
                UnsafeItem item = _unsafeItems[i];
                if (item.LayoutId == layoutId)
                {
                    // Test the Ttl
                    if (DateTime.Now > item.DateTime.AddSeconds(item.Ttl))
                    {
                        _unsafeItems.RemoveAt(i);
                        updateList = true;
                    }
                    else
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (updateList)
            {
                ClientInfo.Instance.UpdateUnsafeList(UnsafeListAsString());
            }

            return found;
        }

        /// <summary>
        /// Is the provided mediaId unsafe?
        /// </summary>
        /// <param name="mediaId"></param>
        /// <returns></returns>
        public bool IsUnsafeMedia(string mediaId)
        {
            bool updateList = false;
            bool found = false;

            for (int i = _unsafeItems.Count - 1; i >= 0; i--)
            {
                UnsafeItem item = _unsafeItems[i];
                if (item.Type == UnsafeItemType.Media &&  item.Id == mediaId)
                {
                    // Test the Ttl
                    if (DateTime.Now > item.DateTime.AddSeconds(item.Ttl))
                    {
                        _unsafeItems.RemoveAt(i);
                        updateList = true;
                    }
                    else
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (updateList)
            {
                ClientInfo.Instance.UpdateUnsafeList(UnsafeListAsString());
            }

            return found;
        }

        /// <summary>
        /// Get the unsafe list represented as a string
        /// </summary>
        /// <returns></returns>
        private string UnsafeListAsString()
        {
            string list = "";
            foreach (UnsafeItem item in _unsafeItems)
            {
                list += item.Type.ToString() + ": " + item.LayoutId + ", " + item.Reason + ", ttl: " + item.Ttl + Environment.NewLine;
            }
            return list;
        }

        #endregion
    }

    /// <summary>
    /// A resource file under cache management
    /// </summary>
    public struct Md5Resource
    {
        public string md5;
        public string path;
        public DateTime cacheDate;
    }

    /// <summary>
    /// An unsafe item entry
    /// </summary>
    public struct UnsafeItem
    {
        public DateTime DateTime { get; set; }
        public UnsafeItemType Type { get; set; }
        public int LayoutId { get; set; }
        public string Id { get; set; }
        public string Reason { get; set; }
        public int Ttl { get; set; }
    }

    /// <summary>
    /// Types of unsafe item
    /// </summary>
    public enum UnsafeItemType
    {
        Layout,
        Region,
        Widget,
        Media
    }
}
