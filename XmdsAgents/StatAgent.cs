using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
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

            // Assume no backlog when we first start out.
            bool isBacklog = false;
            int retryAfterSeconds = 0;
            int countBacklogBatches = 0;
            int processing = 0;

            while (!_forceStop)
            {
                lock (_locker)
                {
                    // What is out processing flag?
                    processing = (new Random()).Next(1, 1000);

                    try
                    {
                        // If we are restarting, reset
                        _manualReset.Reset();

                        // Reset backOff
                        retryAfterSeconds = 0;

                        // How many records do we have to send?
                        int recordsReady = StatManager.Instance.TotalReady();

                        // Does this mean we're in a backlog situation?
                        isBacklog = (recordsReady >= 500);

                        // Check to see if we have anything to send
                        if (StatManager.Instance.MarkRecordsForSend(processing, isBacklog))
                        {

                            HardwareKey key = new HardwareKey();

                            Trace.WriteLine(new LogMessage("StatAgent", "Run: Thread Woken and Lock Obtained, Key: " + processing), LogType.Audit.ToString());

                            using (xmds.xmds xmds = new xmds.xmds())
                            {
                                xmds.Credentials = null;
                                xmds.Url = ApplicationSettings.Default.XiboClient_xmds_xmds + "&method=submitStats";
                                xmds.UseDefaultCredentials = false;

                                // Get the starts we've marked to process as XML.
                                xmds.SubmitStats(ApplicationSettings.Default.ServerKey, key.Key, StatManager.Instance.GetXmlForSend(processing));
                            }

                            // Update the last send date to indicate we've just done so
                            StatManager.Instance.LastSendDate = DateTime.Now;

                            // Remove the ones we've just sent
                            StatManager.Instance.DeleteSent(processing);
                        }
                    }
                    catch (WebException webEx) when (webEx.Response is HttpWebResponse httpWebResponse && (int)httpWebResponse.StatusCode == 429)
                    {
                        // We got a backoff, so none of those records are processed.
                        StatManager.Instance.UnmarkRecordsForSend(processing);

                        // Get the header for how long we ought to wait
                        retryAfterSeconds = webEx.Response.Headers["Retry-After"] != null ? int.Parse(webEx.Response.Headers["Retry-After"]) : 120;

                        // Log it.
                        Trace.WriteLine(new LogMessage("StatAgent", "Run: 429 received, waiting for " + retryAfterSeconds + " seconds."), LogType.Info.ToString());
                    }
                    catch (WebException webEx)
                    {
                        // Increment the quantity of XMDS failures and bail out
                        ApplicationSettings.Default.IncrementXmdsErrorCount();

                        // Log this message, but dont abort the thread
                        Trace.WriteLine(new LogMessage("StatAgent", "Run: WebException in Run: " + webEx.Message), LogType.Info.ToString());

                        // Something went wrong sending those records, set them to be reprocessed
                        StatManager.Instance.UnmarkRecordsForSend(processing);
                    }
                    catch (Exception ex)
                    {
                        // Log this message, but dont abort the thread
                        Trace.WriteLine(new LogMessage("StatAgent", "Run: Exception in Run: " + ex.Message), LogType.Error.ToString());

                        // Something went wrong sending those records, set them to be reprocessed
                        StatManager.Instance.UnmarkRecordsForSend(processing);
                    }
                }

                // Process any impression urls
                StatManager.Instance.DispatchQueuedImpressUrls(processing);

                if (retryAfterSeconds > 0)
                {
                    // Sleep this thread until we've fulfilled our try after
                    _manualReset.WaitOne(retryAfterSeconds * 1000);
                }
                else if (isBacklog)
                {
                    // We've just completed a send in backlog mode, so add to batches.
                    countBacklogBatches++;

                    // How many batches have we sent without a cooldown?
                    if (countBacklogBatches > 2)
                    {
                        // Reset batches
                        countBacklogBatches = 0;

                        // Come back in 30 seconds
                        _manualReset.WaitOne(30000);
                    }
                    else
                    {
                        // Come back much more quickly (10 seconds)
                        _manualReset.WaitOne(10000);
                    }
                }
                else
                {
                    // Reset batches
                    countBacklogBatches = 0;

                    // Sleep this thread until the next collection interval
                    _manualReset.WaitOne((int)(ApplicationSettings.Default.CollectInterval * ApplicationSettings.Default.XmdsCollectionIntervalFactor() * 1000));
                }
            }

            Trace.WriteLine(new LogMessage("StatAgent", "Run: Thread Stopped"), LogType.Info.ToString());
        }
    }
}
