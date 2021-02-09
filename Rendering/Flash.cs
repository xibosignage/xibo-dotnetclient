/**
 * Copyright (C) 2021 Xibo Signage Ltd
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
using System.IO;
using System.Text;

namespace XiboClient.Rendering
{
    class Flash : WebIe
    {
        public Flash(MediaOptions options) : base(options)
        {
            // Set NativeOpen to true
            options.Dictionary.Replace("modeid", "1");

            // This openes a cached file
            string cacheFile = ApplicationSettings.Default.EmbeddedServerAddress + "flash_" + options.FileId + ".htm";
            options.Dictionary.Replace("uri", cacheFile);

            if (!File.Exists(cacheFile))
            {
                // Set the body
                string html = @"
                <html> 
                    <head>
                    </head>
                    <body>
                        <object classid='clsid:d27cdb6e-ae6d-11cf-96b8-444553540000' codebase='http://fpdownload.macromedia.com/pub/shockwave/cabs/flash/swflash.cab#version=7,0,0,0' width='{2}' height='{3}' id='analog_clock' align='middle'>
                            <param name='allowScriptAccess' value='sameDomain' />
                            <param name='movie' value='{1}' />
                            <param name='quality' value='high' />
                            <param name='bgcolor' value='#000' />
                            <param name='WMODE' value='transparent' />
                            <embed src='{1}' quality='high' wmode='transparent' bgcolor='#ffffff' width='{2}' height='{3}' name='analog_clock' align='middle' allowScriptAccess='sameDomain' type='application/x-shockwave-flash' pluginspage='http://www.macromedia.com/go/getflashplayer' />
                        </object>
                    </body>
                </html>
                ";
                html = string.Format(html, options.uri, options.uri, options.width.ToString(), options.height.ToString());

                html = this.MakeHtmlSubstitutions(html);

                // Save this file to disk
                using (FileStream stream = new FileStream(cacheFile, FileMode.Create))
                {
                    using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8))
                    {
                        writer.WriteLine(html);
                    }
                }
            }
        }

        /// <summary>
        /// Is this a native open widget
        /// </summary>
        /// <returns></returns>
        protected override bool IsNativeOpen()
        {
            return true;
        }
    }
}
