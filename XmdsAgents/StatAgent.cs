using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XiboClient.Stats;

namespace XiboClient.XmdsAgents
{
    class StatAgent
    {
        public static object _locker = new object();

        // Members to stop the thread
        private bool _forceStop = false;
        private ManualResetEvent _manualReset = new ManualResetEvent(false);

        /// <summary>
        /// Wake Up
        /// </summary>
        public void WakeUp()
        {
            _manualReset.Set();
        }

        /// <summary>
        /// Stops the thread
        /// </summary>
        public void Stop()
        {
            _forceStop = true;
            _manualReset.Set();
        }

        /// <summary>
        /// Runs the agent
        /// </summary>
        public void Run()
        {
            Trace.WriteLine(new LogMessage("StatAgent", "Run: Thread Started"), LogType.Info.ToString());

            while (!_forceStop)
            {
                lock (_locker)
                {
                    try
                    {
                        // If we are restarting, reset
                        _manualReset.Reset();

                        // Check to see if we have anything to send
                        if (StatManager.Instance.MarkRecordsForSend()) {

                            HardwareKey key = new HardwareKey();

                            Trace.WriteLine(new LogMessage("StatAgent", "Run: Thread Woken and Lock Obtained"), LogType.Audit.ToString());

                            using (xmds.xmds xmds = new xmds.xmds())
                            {
                                xmds.Credentials = null;
                                xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=submitStats";
                                xmds.UseDefaultCredentials = false;

                                // TODO - get the starts we've marked to process as XML.
                                xmds.SubmitStats(ApplicationSettings.Default.ServerKey, key.Key, StatManager.Instance.GetXmlForSend());
                            }
                        }
                    }
                    catch (WebException webEx)
                    {
                        // Increment the quantity of XMDS failures and bail out
                        ApplicationSettings.Default.IncrementXmdsErrorCount();

                        // Log this message, but dont abort the thread
                        Trace.WriteLine(new LogMessage("StatAgent", "Run: WebException in Run: " + webEx.Message), LogType.Info.ToString());
                    }
                    catch (Exception ex)
                    {
                        // Log this message, but dont abort the thread
                        Trace.WriteLine(new LogMessage("StatAgent", "Run: Exception in Run: " + ex.Message), LogType.Error.ToString());
                    }
                }

                // Sleep this thread until the next collection interval
                _manualReset.WaitOne((int)(ApplicationSettings.Default.CollectInterval * ApplicationSettings.Default.XmdsCollectionIntervalFactor() * 1000));
            }

            Trace.WriteLine(new LogMessage("StatAgent", "Run: Thread Stopped"), LogType.Info.ToString());
        }
    }
}
