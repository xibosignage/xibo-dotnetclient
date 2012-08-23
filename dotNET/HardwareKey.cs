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
using System.Runtime.InteropServices;
using System.Management;
using System.Text;
using System.Diagnostics;

namespace XiboClient
{
    class HardwareKey
    {
        private static object _locker = new object();

        private string _hardwareKey;
        private string _macAddress;

        public string MacAddress
        {
            get
            {
                return _macAddress;
            }
        }

        public HardwareKey()
        {
            Debug.WriteLine("[IN]", "HardwareKey");

            // Get the key from the Settings
            _hardwareKey = Properties.Settings.Default.hardwareKey;

            // Is the key empty?
            if (_hardwareKey == "")
            {
                try
                {
                    // Calculate the Hardware key from the CPUID and Volume Serial
                    _hardwareKey = Hashes.MD5(GetCPUId() + GetVolumeSerial("C"));
                }
                catch
                {
                    _hardwareKey = "Change for Unique Key";
                }

                // Store the key
                Properties.Settings.Default.hardwareKey = _hardwareKey;
            }

            // Get the Mac Address
            _macAddress = GetMACAddress();

            Debug.WriteLine("[OUT]", "HardwareKey");
        }

        /// <summary>
        /// Gets the hardware key
        /// </summary>
        public string Key
        {
            get 
            { 
                return this._hardwareKey; 
            }
        }

        /// <summary>
        /// Regenerates the hardware key
        /// </summary>
        public void Regenerate()
        {
            lock (_locker)
            {
                // Calculate the Hardware key from the CPUID and Volume Serial
                _hardwareKey = Hashes.MD5(GetCPUId() + GetVolumeSerial("C"));

                // Store the key
                Properties.Settings.Default.hardwareKey = _hardwareKey;
                Properties.Settings.Default.Save();
            }
        }

        /// <summary>
        /// return Volume Serial Number from hard drive
        /// </summary>
        /// <param name="strDriveLetter">[optional] Drive letter</param>
        /// <returns>[string] VolumeSerialNumber</returns>
        public string GetVolumeSerial(string strDriveLetter)
        {
            lock (_locker)
            {
                Debug.WriteLine("[IN]", "GetVolumeSerial");

                if (strDriveLetter == "" || strDriveLetter == null) strDriveLetter = "C";
                ManagementObject disk = new ManagementObject("win32_logicaldisk.deviceid=\"" + strDriveLetter + ":\"");
                disk.Get();

                System.Diagnostics.Debug.WriteLine("[OUT]", "GetVolumeSerial");

                return disk["VolumeSerialNumber"].ToString();
            }
        }

        /// <summary>
        /// Returns MAC Address from first Network Card in Computer
        /// </summary>
        /// <returns>[string] MAC Address</returns>
        public string GetMACAddress()
        {
            lock (_locker)
            {
                ManagementClass mc = new ManagementClass("Win32_NetworkAdapterConfiguration");
                ManagementObjectCollection moc = mc.GetInstances();
                string MACAddress = String.Empty;

                foreach (ManagementObject mo in moc)
                {
                    if (MACAddress == String.Empty)  // only return MAC Address from first card
                    {
                        if ((bool)mo["IPEnabled"] == true) MACAddress = mo["MacAddress"].ToString();
                    }
                    mo.Dispose();
                }

                return MACAddress;
            }
        }

        /// <summary>
        /// Return processorId from first CPU in machine
        /// </summary>
        /// <returns>[string] ProcessorId</returns>
        public string GetCPUId()
        {
            lock (_locker)
            {
                Debug.WriteLine("[IN]", "GetCPUId");

                string cpuInfo = String.Empty;
                string temp = String.Empty;
                ManagementClass mc = new ManagementClass("Win32_Processor");
                ManagementObjectCollection moc = mc.GetInstances();
                foreach (ManagementObject mo in moc)
                {
                    if (cpuInfo == String.Empty)
                    {   // only return cpuInfo from first CPU
                        cpuInfo = mo.Properties["ProcessorId"].Value.ToString();
                    }
                }

                Debug.WriteLine("[OUT]", "GetCPUId");

                return cpuInfo;
            }
        }
    }
}
