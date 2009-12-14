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

namespace XiboClient
{
    public class CacheManager
    {
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
                        // Get the MD5 again
                        return CalcMD5(path);
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
            // Open the file and get the MD5
            using (FileStream md5Fs = new FileStream(Properties.Settings.Default.LibraryPath + @"\" + path, FileMode.Open, FileAccess.Read))
            {
                return Hashes.MD5(md5Fs);
            }
        }

        /// <summary>
        /// Adds a MD5 to the CacheManager
        /// </summary>
        /// <param name="path"></param>
        /// <param name="md5"></param>
        public void Add(String path, String md5)
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

            System.Diagnostics.Debug.WriteLine(new LogMessage("Add", "Adding new MD5 to CacheManager"), LogType.Info.ToString());
        }

        /// <summary>
        /// Removes the MD5 resource associated with the Path given
        /// </summary>
        /// <param name="path"></param>
        public void Remove(String path)
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

    public struct Md5Resource
    {
        public String md5;
        public String path;
        public DateTime cacheDate;
    }
}
