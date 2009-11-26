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
    class CacheManager
    {
        Collection<Md5Resource> _files;

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
                    return file.md5;
                }
            }

            // We need to generate the MD5 and store it for later
            Md5Resource md5Resource = new Md5Resource();

            md5Resource.path = path;

            using (FileStream md5Fs = new FileStream(Properties.Settings.Default.LibraryPath + @"\" + path, FileMode.Open, FileAccess.Read))
            {
                md5Resource.md5 = Hashes.MD5(md5Fs);
                _files.Add(md5Resource);

                return md5Resource.md5;
            }
        }
    }

    struct Md5Resource
    {
        public String md5;
        public String path;
    }
}
