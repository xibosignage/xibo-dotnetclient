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
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace XiboClient
{
    class Hashes
    {
        /// <summary>
        /// Calculates a MD5 of this given FileStream
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static string MD5(FileStream fileStream)
        {
            try
            {
                MD5 md5 = new MD5CryptoServiceProvider();
                byte[] hash = md5.ComputeHash(fileStream);

                fileStream.Close();

                StringBuilder sb = new StringBuilder();
                foreach (byte a in hash)
                {
                    if (a < 16)
                        sb.Append("0" + a.ToString("x"));
                    else
                        sb.Append(a.ToString("x"));
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message, "Hashes");
                throw;
            }
        }

        /// <summary>
        /// Calculates a MD5 of this given FileStream
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static string MD5(Byte[] fileStream)
        {
            try
            {
                MD5 md5 = new MD5CryptoServiceProvider();
                byte[] hash = md5.ComputeHash(fileStream);

                StringBuilder sb = new StringBuilder();
                foreach (byte a in hash)
                {
                    if (a < 16)
                        sb.Append("0" + a.ToString("x"));
                    else
                        sb.Append(a.ToString("x"));
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message, "Hashes");
                throw;
            }
        }

        /// <summary>
        /// Calculates a MD5 of this given FileStream
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static string MD5(String fileString)
        {
            byte[] fileStream = Encoding.UTF8.GetBytes(fileString);

            try
            {
                MD5 md5 = new MD5CryptoServiceProvider();
                byte[] hash = md5.ComputeHash(fileStream);

                StringBuilder sb = new StringBuilder();
                foreach (byte a in hash)
                {
                    if (a < 16)
                        sb.Append("0" + a.ToString("x"));
                    else
                        sb.Append(a.ToString("x"));
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message, "Hashes");
                throw;
            }
        }
    }
}
