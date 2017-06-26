using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;

namespace XiboClient.Logic
{
    public class Rs232Command
    {
        private Command _command;

        private SerialPort _port;
        private string _toSend = null;
        private bool _useHex = false;

        public Rs232Command(Command command)
        {
            _command = command;
        }

        /// <summary>
        /// Run the command
        /// throws an exception if we cannot open or write to the port
        /// </summary>
        public string Run()
        {
            string response = "";

            // Parse and configure the port
            parse();

            Trace.WriteLine(new LogMessage("Rs232Command - run", "Parsed command, will open port " + _port.PortName + " and write " + _toSend), LogType.Audit.ToString());

            // try to open the COM port
            if (!_port.IsOpen)
                _port.Open();

            try
            {
                // Write our data stream
                if (_useHex)
                {
                    byte[] bytes = _toSend.Split(' ').Select(s => Convert.ToByte(s, 16)).ToArray();
                    _port.Write(bytes, 0, bytes.Length);
                }
                else
                {
                    _port.Write(_toSend);
                }

                // Read
                if (_command.notifyStatus())
                {
                    _port.ReadTimeout = 5000;
                    response = _port.ReadLine();
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("CommandRs232 - run", e.Message), LogType.Error.ToString());
            }
            finally
            {
                // Close the port
                _port.Close();
            }

            return response;
        }

        /// <summary>
        /// Parses the command string
        /// </summary>
        private void parse()
        {
            if (!_command.CommandString.StartsWith("rs232"))
                throw new ArgumentException("Not a RS232 command");

            // Split the command string by "space"
            string[] command = _command.CommandString.Split('|');

            // The second part of the string is our comma separated connection string
            // Port,Baud,Data,Parity,Stop,Flow
            string[] connection = command[1].Split(',');

            _port = new SerialPort();
            _port.PortName = connection[0];
            _port.BaudRate = Convert.ToInt32(connection[1]);
            _port.DataBits = Convert.ToInt16(connection[2]);
            _port.Parity = (Parity)Enum.Parse(typeof(Parity), connection[3]);
            _port.StopBits = (StopBits)Enum.Parse(typeof(StopBits), connection[4]);
            _port.Handshake = (Handshake)Enum.Parse(typeof(Handshake), connection[5]);

            // Get the actual command to send
            _toSend = command[2];

            // Do we have a HEX bit?
            _useHex = (connection.Length >= 7 && connection[6] == "1");
        }
    }
}
