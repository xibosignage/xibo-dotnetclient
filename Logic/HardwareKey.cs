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
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Windows.Forms;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using System.IO;

namespace XiboClient
{
    class HardwareKey
    {
        private static object _locker = new object();

        private string _hardwareKey;
        private string _macAddress;
        private string _channel;

        public string Channel
        {
            get
            {
                if (String.IsNullOrEmpty(_channel))
                {
                    // Channel is based on the CMS URL, CMS Key and Hardware Key
                    _channel = Hashes.MD5(ApplicationSettings.Default.ServerUri + ApplicationSettings.Default.ServerKey + _hardwareKey);
                }

                return _channel;
            }
        }

        public void clearChannel()
        {
            _channel = null;
        }

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

            // Get the Mac Address
            _macAddress = GetMacAddress();

            // Get the key from the Settings
            _hardwareKey = ApplicationSettings.Default.HardwareKey;

            // Is the key empty?
            if (_hardwareKey == "")
            {
                try
                {
                    string systemDriveLetter = Path.GetPathRoot(Environment.SystemDirectory);

                    // Calculate the Hardware key from the CPUID and Volume Serial
                    _hardwareKey = Hashes.MD5(GetCPUId() + GetVolumeSerial(systemDriveLetter[0].ToString()) + _macAddress);
                }
                catch
                {
                    _hardwareKey = "Change for Unique Key";
                }

                // Store the key
                ApplicationSettings.Default.HardwareKey = _hardwareKey;
            }

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
                ApplicationSettings.Default.HardwareKey = _hardwareKey;
                ApplicationSettings.Default.Save();
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
        /// Finds the MAC address of the first operation NIC found.
        /// </summary>
        /// <returns>The MAC address.</returns>
        private string GetMacAddress()
        {
            string macAddresses = string.Empty;

            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus == OperationalStatus.Up)
                    {
                        macAddresses += BitConverter.ToString(nic.GetPhysicalAddress().GetAddressBytes()).Replace('-', ':');
                        break;
                    }
                }
            }
            catch
            {
                macAddresses = "00:00:00:00:00:00";
            }

            return macAddresses;
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

        /// <summary>
        /// Get the XMR public key
        /// </summary>
        /// <returns></returns>
        public AsymmetricCipherKeyPair getXmrKey()
        {
            const int PROVIDER_RSA_FULL = 1;
            CspParameters cspParams;
            cspParams = new CspParameters(PROVIDER_RSA_FULL);
            cspParams.KeyContainerName = Application.ProductName + "-" + Environment.UserName + "-" + "RsaKey";
            cspParams.Flags = CspProviderFlags.UseMachineKeyStore;
            cspParams.ProviderName = "Microsoft Strong Cryptographic Provider";

            using (RSACryptoServiceProvider provider = new RSACryptoServiceProvider(cspParams))
            {
                RSAParameters keyInfo = provider.ExportParameters(true);

                return DotNetUtilities.GetRsaKeyPair(keyInfo);
            }
        }

        public string getXmrPublicKey()
        {
            try
            {
                AsymmetricCipherKeyPair key = getXmrKey();

                using (TextWriter textWriter = new StringWriter())
                {
                    PemWriter writer = new PemWriter(textWriter);
                    writer.WriteObject(key.Public);
                    writer.Writer.Flush();

                    return textWriter.ToString();
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("HardwareKey - getXmrPublicKey", "Unable to get XMR public key. E = " + e.Message), LogType.Error.ToString());
                return null;
            }
        }
    }
}
