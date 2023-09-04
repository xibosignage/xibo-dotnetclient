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
using System.Collections.Generic;

namespace XiboClient.Logic
{
    public sealed class MediaDictionary
    {
        private List<MediaOption> _options;

        public MediaDictionary()
        {
            _options = new List<MediaOption>();
        }

        public void Add(string name, string value)
        {
            MediaOption option = new MediaOption();
            option.Name = name;
            option.Value = value;

            _options.Add(option);
        }

        public void Replace(string name, string value)
        {
            int optIndex = _options.FindIndex(o => o.Name == name);
            if (optIndex > -1)
                _options.RemoveAt(optIndex);

            Add(name, value);
        }

        public void Clear()
        {
            _options.Clear();
        }

        public int Count
        {
            get
            {
                return _options.Count;
            }
        }

        public string Get(string name)
        {
            foreach (MediaOption option in _options)
            {
                if (option.Name == name)
                    return option.Value;
            }

            return string.Empty;
        }

        public string Get(string name, string def)
        {
            string value;

            try
            {
                value = Get(name);

                if (string.IsNullOrEmpty(value))
                    return def;

                return value;
            }
            catch
            {
                return def;
            }
        }

        public int Get(string name, int def)
        {
            string value;

            try
            {
                value = Get(name);

                if (string.IsNullOrEmpty(value))
                    return def;

                return int.Parse(value);
            }
            catch
            {
                return def;
            }
        }
    }
}
