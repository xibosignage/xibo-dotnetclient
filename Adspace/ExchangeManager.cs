/**
 * Copyright (C) 2022 Xibo Signage Ltd
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
using Flurl;
using Flurl.Http;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto.Engines;
using Swan;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.EnterpriseServices;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using XiboClient.Log;

namespace XiboClient.Adspace
{
    class ExchangeManager
    {
        private readonly string AdspaceUrl = @"https://exchange.xibo-adspace.com/vast/device";

        // State
        private readonly object buffetLock = new object();
        private bool isActive;
        private bool isNewPrefetchAdded = false;
        private DateTime lastFillDate;
        private DateTime lastPrefetchDate;
        private List<string> prefetchUrls = new List<string>();
        private List<Ad> adBuffet = new List<Ad>();
        private Dictionary<string, int> lastUnwrapRateLimits = new Dictionary<string, int>();
        private Dictionary<string, DateTime> lastUnwrapDates = new Dictionary<string, DateTime>();

        public int ShareOfVoice { get; private set; } = 0;
        public int AverageAdDuration { get; private set; } = 0;

        public ExchangeManager()
        {
            lastFillDate = DateTime.Now.AddYears(-1);
            lastPrefetchDate = DateTime.Now.AddHours(1);
        }

        /// <summary>
        /// Is an ad available to show?
        /// </summary>
        public bool IsAdAvailable
        {
            get
            {
                return CountAvailableAds > 0;
            }
        }

        /// <summary>
        /// Count of availabe ads
        /// </summary>
        public int CountAvailableAds
        {
            get
            {
                return adBuffet.Count;
            }
        }

        /// <summary>
        /// Set adspace as active or inactive
        /// </summary>
        /// <param name="active"></param>
        public void SetActive(bool active)
        {
            if (isActive != active && active)
            {
                // Transitioning to active
                if (prefetchUrls.Count > 0)
                {
                    Task.Factory.StartNew(() => Prefetch());
                }
            }
            else if (isActive != active)
            {
                // Transitioning to inactive
                adBuffet.Clear();
            }

            // Store the new state
            isActive = active;
        }

        /// <summary>
        /// Called to configure the exchange manager for the next set of playbacks
        /// </summary>
        public void Configure()
        {
            // If our last fill date is really old, clear out ads and refresh
            if (lastFillDate < DateTime.Now.AddHours(-1))
            {
                lock (buffetLock)
                {
                    adBuffet.Clear();
                }
            }

            // Should we configure
            if (IsAdAvailable && ShareOfVoice > 0)
            {
                Trace.WriteLine(new LogMessage("ExchangeManager", "Configure: we do not need to configure this time around"), LogType.Audit.ToString());
                return;
            }

            // No ads available, but we still want to throttle checking for new ones.
            if (lastFillDate < DateTime.Now.AddMinutes(-3))
            {
                // Fill our ad buffet
                Fill(true);
            }

            // Should we also prefetch?
            if (lastPrefetchDate < DateTime.Now.AddHours(-24))
            {
                Task.Factory.StartNew(() => Prefetch());
            }
        }

        /// <summary>
        /// Get an ad for the specified width/height
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public Ad GetAd(double width, double height)
        {
            if (!IsAdAvailable)
            {
                throw new AdspaceNoAdException();
            }

            Ad ad;
            try
            {
                ad = GetAvailableAd();
            }
            catch
            {
                Trace.WriteLine(new LogMessage("ExchangeManager", "GetAd: no available ad returned while unwrapping"), LogType.Error.ToString());
                throw new AdspaceNoAdException("No ad returned");
            }

            // Check geo fence
            if (!ad.IsGeoActive(ClientInfo.Instance.CurrentGeoLocation))
            {
                ReportError(ad.ErrorUrls, 408);
                adBuffet.Remove(ad);
                throw new AdspaceNoAdException("Outside geofence");
            }

            // Determine type
            if (ad.Type.StartsWith("video"))
            {
                ad.XiboType = "video";
            }
            else if (ad.Type.StartsWith("image"))
            {
                ad.XiboType = "image";
            }
            else
            {
                ReportError(ad.ErrorUrls, 200);
                adBuffet.Remove(ad);
                throw new AdspaceNoAdException("Type not recognised");
            }

            // Determine Size
            if (width / height != ad.AspectRatio)
            {
                ReportError(ad.ErrorUrls, 203);
                adBuffet.Remove(ad);
                throw new AdspaceNoAdException("Dimensions invalid");
            }

            // TODO: check fault status

            // Check to see if the file is already there, and if not, download it.
            if (!CacheManager.Instance.IsValidPath(ad.GetFileName()))
            {
                Task.Factory.StartNew(() => ad.Download());

                // Don't show it this time
                adBuffet.Remove(ad);
                throw new AdspaceNoAdException("Creative pending download");
            }

            // We've converted it into a play
            adBuffet.Remove(ad);

            return ad;
        }

        /// <summary>
        /// Get an available ad
        /// </summary>
        /// <returns></returns>
        /// <exception cref="AdspaceNoAdException"></exception>
        private Ad GetAvailableAd()
        {
            if (adBuffet.Count > 0)
            {
                bool isResolveRequested = false;
                Ad chosenAd = null;
                foreach (Ad ad in adBuffet)
                {
                    if (ad.IsWrapper && !ad.IsWrapperResolved)
                    {
                        // Resolve one
                        // we only want to do this one time per getAvailableAd, and only if that
                        // ad isn't already resolving (no need to fire loads of events which won't
                        // do anything).
                        if (!isResolveRequested && !ad.IsWrapperResolving)
                        {
                            isResolveRequested = true;
                        }
                        continue;
                    }

                    // Not a wrapper or a wrapper already resolved.
                    chosenAd = ad;
                    break;
                }

                if (isResolveRequested)
                {
                    Trace.WriteLine(new LogMessage("ExchangeManager", "GetAvailableAd: will call to unwrap an ad, there are " + CountAvailableAds), LogType.Info.ToString());
                    Task.Factory.StartNew(() => UnwrapAds());
                }

                if (chosenAd != null)
                {
                    return chosenAd;
                }
            }
            throw new AdspaceNoAdException();
        }

        /// <summary>
        /// Prefetch any resources which might play
        /// </summary>
        public void Prefetch()
        {           
            foreach (string url in prefetchUrls)
            {
                try
                {
                    // Do we have a simple URL or a URL with a key
                    string resolvedUrl = url;
                    string urlProp = null;
                    string idProp = null;
                    if (url.Contains("||"))
                    {
                        // Split the URL
                        string[] splits = url.Split(new string[] { "||" }, StringSplitOptions.None);
                        resolvedUrl = splits[0];
                        urlProp = splits[1];
                        idProp = splits[2];
                    }

                    // We either expect a list of strings or a list of objects.
                    if (urlProp != null && idProp != null)
                    {
                        // Expect an array of objects.
                        var result = resolvedUrl.GetJsonAsync<List<JObject>>().Result;

                        // Download each one
                        foreach (JObject creative in result)
                        {
                            string fetchUrl = creative.GetValue(urlProp).ToString();
                            string fileName = "axe_" + creative.GetValue(idProp).ToString();
                            if (!CacheManager.Instance.IsValidPath(fileName))
                            {
                                // We should download it.
                                new Url(fetchUrl).DownloadFileAsync(ApplicationSettings.Default.LibraryPath, fileName).ContinueWith(t =>
                                {
                                    CacheManager.Instance.Add(fileName, CacheManager.Instance.GetMD5(fileName));
                                },
                                TaskContinuationOptions.OnlyOnRanToCompletion);
                            }
                        }
                    }
                    else
                    {
                        // Get a JSON string from the URL.
                        var result = url.GetJsonAsync<List<string>>().Result;

                        // Download each one
                        foreach (string fetchUrl in result)
                        {
                            string fileName = "axe_" + fetchUrl.Split('/').Last();
                            if (!CacheManager.Instance.IsValidPath(fileName))
                            {
                                // We should download it.
                                new Url(fetchUrl).DownloadFileAsync(ApplicationSettings.Default.LibraryPath, fileName).ContinueWith(t =>
                                {
                                    CacheManager.Instance.Add(fileName, CacheManager.Instance.GetMD5(fileName));
                                },
                                TaskContinuationOptions.OnlyOnRanToCompletion);
                            }
                        }
                    }
                } 
                catch (Exception e)
                {
                    Trace.WriteLine(new LogMessage("ExchangeManager", "Prefetch: failed to call prefetch. e = " + e.Message.ToString()), LogType.Error.ToString());
                }
            }
        }

        /// <summary>
        /// Fill the ad buffet
        /// </summary>
        /// <param name="force"></param>
        private void Fill(bool force)
        {
            lastFillDate = DateTime.Now;

            if (!force && ShareOfVoice > 0 && CountAvailableAds > 0)
            {
                // We have ads and we aren't forcing a fill
                return;
            }

            // Make a URL request
            var url = new Url(AdspaceUrl);
            url = url.AppendPathSegment("request")
                .AppendPathSegment(ApplicationSettings.Default.HardwareKey)
                .SetQueryParam("ownerKey", ApplicationSettings.Default.ServerUri);

            if (ClientInfo.Instance.CurrentGeoLocation != null && !ClientInfo.Instance.CurrentGeoLocation.IsUnknown)
            {
                url.SetQueryParam("lat", ClientInfo.Instance.CurrentGeoLocation.Latitude)
                    .SetQueryParam("lng", ClientInfo.Instance.CurrentGeoLocation.Longitude);
            }

            // Request new ads and then lock the buffet while we update it
            List<Ad> newAds = Request(url);
            lock (buffetLock)
            {
                adBuffet.AddRange(newAds);
            }

            // Did we add any new prefetch URLs?
            if (isNewPrefetchAdded)
            {
                Task.Factory.StartNew(() => Prefetch());
                isNewPrefetchAdded = false;
            }
        }

        /// <summary>
        /// Request new ads
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private List<Ad> Request(Url url)
        {
            return Request(url, null);
        }

        private List<Ad> Request(string url, Ad wrappedAd)
        {
            if (wrappedAd != null)
            {
                if (ClientInfo.Instance.CurrentGeoLocation != null && !ClientInfo.Instance.CurrentGeoLocation.IsUnknown)
                {
                    url = url
                        .Replace("[LAT]", "" + ClientInfo.Instance.CurrentGeoLocation.Latitude)
                        .Replace("[LNG]", "" + ClientInfo.Instance.CurrentGeoLocation.Longitude);
                }
                else
                {
                    url = url
                        .Replace("[LAT]", "")
                        .Replace("[LNG]", "");
                }
            }

            return Request(new Url(url), wrappedAd);
        }

        /// <summary>
        /// Request new ads
        /// </summary>
        /// <param name="url"></param>
        /// <param name="wrappedAd">If we have a wrapper ad we can resolve it by passing it here.</param>
        /// <returns></returns>
        private List<Ad> Request(Url url, Ad wrappedAd)
        {
            LogMessage.Trace("ExchangeManager", "Request", url.ToString());

            // Track the last time we unwrap an ad for each partner.
            if (wrappedAd != null)
            {
                SetUnwrapLast(wrappedAd.WrapperPartner);
            }

            // Make a request for new ads
            List<Ad> buffet = new List<Ad>();
            try
            {
                IFlurlResponse response = null;
                if (wrappedAd != null && wrappedAd.WrapperHttpMethod == "POST")
                {
                    response = url.WithTimeout(10).PostAsync().Result;
                }
                else
                {
                    response = url.WithTimeout(10).GetAsync().Result;
                }
                var body = response.GetStringAsync().Result;

                if (string.IsNullOrEmpty(body))
                {
                    throw new Exception("Empty body");
                }

                // If we are a wrapped ad, then we should attempt to resolve that ad.
                // If not, then we are the parent request from adspace and we should resolve the sov/average spot duration/etc
                if (wrappedAd == null)
                {
                    string sovHeader = response.Headers.FirstOrDefault(h => h.Name == "x-adspace-sov").Value;
                    if (!string.IsNullOrEmpty(sovHeader))
                    {
                        try
                        {
                            ShareOfVoice = int.Parse(sovHeader);
                        }
                        catch
                        {
                            Trace.WriteLine(new LogMessage("ExchangeManager", "Request: error parsing SOV header"), LogType.Error.ToString());
                        }
                    }

                    string averageAdDurationHeader = response.Headers.FirstOrDefault(h => h.Name == "x-adspace-avg-duration").Value;
                    if (!string.IsNullOrEmpty(averageAdDurationHeader))
                    {
                        try
                        {
                            AverageAdDuration = int.Parse(averageAdDurationHeader);
                        }
                        catch
                        {
                            Trace.WriteLine(new LogMessage("ExchangeManager", "Request: error parsing avg duration header"), LogType.Error.ToString());
                        }
                    }
                }

                // Read the body of the response into XML
                XmlDocument document = new XmlDocument();
                document.LoadXml(body);

                // Expect one or more ad nodes.
                foreach (XmlNode adNode in document.DocumentElement.ChildNodes)
                {
                    // If we have a wrapped ad, resolve it.
                    Ad ad;
                    if (wrappedAd == null)
                    {
                        ad = new Ad
                        {
                            Id = adNode.Attributes["id"].Value
                        };
                    }
                    else
                    {
                        ad = wrappedAd;
                        ad.CountWraps++;
                    }

                    // In-line or wrapper
                    XmlNode wrapper = adNode.SelectSingleNode("./Wrapper");
                    if (wrapper != null)
                    {
                        ad.IsWrapper = true;

                        // Are we wrapped up too much
                        if (ad.CountWraps >= 5)
                        {
                            ReportError(ad.ErrorUrls, 302);
                            continue;
                        }

                        // pull out the URL we should go to next.
                        XmlNode adTagUrlNode = wrapper.SelectSingleNode("./VASTAdTagURI");
                        if (adTagUrlNode == null)
                        {
                            ReportError(ad.ErrorUrls, 302);
                            continue;
                        }

                        // Capture the URL
                        ad.AdTagUri = adTagUrlNode.InnerText.Trim();

                        // Get and impression/error URLs included with this wrap
                        XmlNode errorUrlNode = wrapper.SelectSingleNode("./Error");
                        if (errorUrlNode != null)
                        {
                            ad.ErrorUrls.Add(errorUrlNode.InnerText.Trim());
                        }

                        XmlNode impressionUrlNode = wrapper.SelectSingleNode("./Impression");
                        if (impressionUrlNode != null)
                        {
                            ad.ImpressionUrls.Add(impressionUrlNode.InnerText.Trim());
                        }

                        // Extensions
                        XmlNodeList extensionNodes = wrapper.SelectNodes(".//Extension");
                        foreach (XmlNode extensionNode in extensionNodes)
                        {
                            switch (extensionNode.Attributes["type"].Value)
                            {
                                case "prefetch":
                                case "xiboPrefetch":
                                    if (!prefetchUrls.Contains(extensionNode.InnerText))
                                    {
                                        prefetchUrls.Add(extensionNode.InnerText);
                                        isNewPrefetchAdded = true;
                                    }
                                    break;

                                case "validType":
                                case "xiboValidType":
                                    if (!string.IsNullOrEmpty(extensionNode.InnerText))
                                    {
                                        ad.WrapperAllowedTypes = extensionNode.InnerText.Split(',').ToList();
                                    }
                                    break;

                                case "validDuration":
                                case "xiboValidDuration":
                                    ad.WrapperAllowedDuration = extensionNode.InnerText;
                                    break;

                                case "xiboMaxDuration":
                                    try
                                    {
                                        ad.WrapperMaxDuration = int.Parse(extensionNode.InnerText.Trim());
                                    }
                                    catch
                                    {
                                        LogMessage.Trace("ExchangeManager", "Request", "Invalid xiboIsWrapperRateLimit");

                                    }
                                    break;

                                case "xiboIsWrapperOpenImmediately":
                                    try
                                    {
                                        ad.IsWrapperOpenImmediately = int.Parse(extensionNode.InnerText.Trim()) == 1;
                                    }
                                    catch
                                    {
                                        LogMessage.Trace("ExchangeManager", "Request", "Invalid xiboIsWrapperRateLimit");

                                    }
                                    break;

                                case "xiboPartner":
                                    ad.WrapperPartner = extensionNode.InnerText.Trim();
                                    break;

                                case "xiboIsWrapperRateLimit":
                                    try
                                    {
                                        ad.WrapperRateLimit = int.Parse(extensionNode.InnerText.Trim());
                                    }
                                    catch
                                    {
                                        LogMessage.Trace("ExchangeManager", "Request", "Invalid xiboIsWrapperRateLimit");

                                    }
                                    break;

                                case "xiboFileScheme":
                                    ad.WrapperFileScheme = extensionNode.InnerText.Trim();
                                    break;

                                case "xiboExtendUrl":
                                    ad.WrapperExtendUrl = extensionNode.InnerText.Trim();
                                    break;

                                case "xiboHttpMethod":
                                    ad.WrapperHttpMethod = extensionNode.InnerText.Trim();
                                    break;

                                default:
                                    LogMessage.Trace("ExchangeManager", "Request", "Unknown extension");
                                    break;
                            }
                        }

                        ad.IsWrapperResolved = false;

                        // Record our rate limit if we have one
                        if (ad.WrapperRateLimit > 0 && !string.IsNullOrEmpty(ad.WrapperPartner))
                        {
                            SetUnwrapRateThreshold(ad.WrapperPartner, ad.WrapperRateLimit);
                        }

                        // If we need to unwrap these immediately, then do so, but only
                        // if we haven't exceeded the rate threshold for this partner.
                        try
                        {
                            if (ad.IsWrapperOpenImmediately && !IsUnwrapRateThreshold(ad.WrapperPartner))
                            {
                                buffet.AddRange(Request(ad.AdTagUri, ad));
                            }
                            else
                            {
                                buffet.Add(ad);
                            }
                        }
                        catch
                        {
                            // Ignored
                        }
                        finally
                        {
                            ad = null;
                        }

                        // If we've handled a wrapper we cannot handle an inline in the same ad.
                        continue;
                    }

                    // In-line
                    XmlNode inlineNode = adNode.SelectSingleNode("./InLine");
                    if (inlineNode != null)
                    {
                        // Title
                        XmlNode titleNode = inlineNode.SelectSingleNode("./AdTitle");
                        if (titleNode != null)
                        {
                            ad.Title = titleNode.InnerText.Trim();
                        }

                        // Get and impression/error URLs included with this wrap
                        XmlNode errorUrlNode = inlineNode.SelectSingleNode("./Error");
                        if (errorUrlNode != null)
                        {
                            string errorUrl = errorUrlNode.InnerText.Trim();
                            if (errorUrl != "about:blank")
                            {
                                ad.ErrorUrls.Add(errorUrl + ad.WrapperExtendUrl);
                            }
                        }

                        XmlNode impressionUrlNode = inlineNode.SelectSingleNode("./Impression");
                        if (impressionUrlNode != null)
                        {
                            string impressionUrl = impressionUrlNode.InnerText.Trim();
                            if (impressionUrl != "about:blank")
                            {
                                ad.ImpressionUrls.Add(impressionUrl + ad.WrapperExtendUrl);
                            }
                        }

                        // Creatives
                        XmlNode creativeNode = inlineNode.SelectSingleNode("./Creatives/Creative");
                        if (creativeNode != null)
                        {
                            ad.CreativeId = creativeNode.Attributes["id"].Value;

                            // Get the duration.
                            XmlNode creativeDurationNode = creativeNode.SelectSingleNode("./Linear/Duration");
                            if (creativeDurationNode != null)
                            {
                                ad.Duration = creativeDurationNode.InnerText.Trim();
                            }
                            else
                            {
                                ReportError(ad.ErrorUrls, 302);
                                continue;
                            }

                            // Get the media file
                            XmlNode creativeMediaNode = creativeNode.SelectSingleNode("./Linear/MediaFiles/MediaFile");
                            if (creativeMediaNode != null)
                            {
                                ad.Url = creativeMediaNode.InnerText.Trim();
                                ad.Width = int.Parse(creativeMediaNode.Attributes["width"].Value);
                                ad.Height = int.Parse(creativeMediaNode.Attributes["height"].Value);
                                ad.Type = creativeMediaNode.Attributes["type"].Value;
                            }
                            else
                            {
                                ReportError(ad.ErrorUrls, 302);
                                continue;
                            }
                        }
                        else
                        {
                            // Malformed Ad.
                            ReportError(ad.ErrorUrls, 300);
                            continue;
                        }

                        // Extensions
                        XmlNodeList extensionNodes = inlineNode.SelectNodes("./Extension");
                        foreach (XmlNode extensionNode in extensionNodes)
                        {
                            switch (extensionNode.Attributes["type"].Value)
                            {
                                case "geoFence":
                                    ad.IsGeoAware = true;
                                    ad.GeoLocation = extensionNode.InnerText;
                                    break;
                            }
                        }

                        // Did this resolve from a wrapper? if so do some extra checks.
                        if (ad.IsWrapper)
                        {
                            // Type
                            if (!ad.WrapperAllowedTypes.Contains("all", StringComparer.OrdinalIgnoreCase)
                                && !ad.WrapperAllowedTypes.Contains(ad.Type.ToLower(), StringComparer.OrdinalIgnoreCase))
                            {
                                ReportError(ad.ErrorUrls, 200);
                                continue;
                            }

                            // Duration
                            if (!string.IsNullOrEmpty(ad.WrapperAllowedDuration)
                                && ad.GetDuration() != ad.GetWrapperAllowedDuration())
                            {
                                ReportError(ad.ErrorUrls, 202);
                                continue;
                            }

                            // Max duration
                            if (ad.WrapperMaxDuration > 0
                                && ad.GetDuration() > ad.WrapperMaxDuration)
                            {
                                ReportError(ad.ErrorUrls, 202);
                                continue;
                            }

                            // Wrapper is resolved
                            ad.IsWrapperResolved = true;
                        }

                        // Download if necessary
                        if (!CacheManager.Instance.IsValidPath(ad.GetFileName()))
                        {
                            Task.Factory.StartNew(() => ad.Download());
                        }

                        // Ad this to our list
                        buffet.Add(ad);
                    }
                }

                if (buffet.Count <= 0)
                {
                    // Nothing added this time.
                    Trace.WriteLine(new LogMessage("ExchangeManager", "Request: No ads returned this time"), LogType.Info.ToString());
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("ExchangeManager", "Request: failed to make or parse request. e: " + e.Message), LogType.Error.ToString());
                if (wrappedAd != null && wrappedAd.IsWrapper)
                {
                    ReportError(wrappedAd.ErrorUrls, 303);
                }
            }

            return buffet;
        }

        /// <summary>
        /// Unwrap ads
        /// </summary>
        private void UnwrapAds()
        {
            lock (buffetLock)
            {
                // Keep a list of ads we add
                List<Ad> unwrappedAds = new List<Ad>();

                // Backwards loop
                for (int i = adBuffet.Count - 1; i >= 0; i--)
                {
                    Ad ad = adBuffet[i];
                    if (ad.IsWrapper && !ad.IsWrapperResolved)
                    {
                        if (ad.IsWrapperResolving)
                        {
                            continue;
                        }

                        // Is this partner rate limited
                        if (IsUnwrapRateThreshold(ad.WrapperPartner))
                        {
                            continue;
                        }

                        LogMessage.Info("ExchangeManager", "UnwrapAds", "resolving " + ad.Id);

                        // Remove this ad (we're resolving it)
                        ad.IsWrapperResolving = true;
                        adBuffet.RemoveAt(i);

                        // Make a request to unwrap this ad.
                        try
                        {
                            unwrappedAds.AddRange(Request(ad.AdTagUri, ad));
                        }
                        catch (Exception e)
                        {
                            LogMessage.Error("ExchangeManager", "UnwrapAds", "wrapped ad did not resolve: " + e.Message.ToString());
                        }
                    }    
                }

                LogMessage.Trace("ExchangeManager", "UnwrapAds", unwrappedAds.Count + " unwrapped");

                // Add in any new ones we've got as a result
                adBuffet.AddRange(unwrappedAds);
                unwrappedAds.Clear();
            }
        }

        /// <summary>
        /// Report an error code to a list of URLs
        /// </summary>
        /// <param name="urls"></param>
        /// <param name="errorCode"></param>
        private void ReportError(List<string> urls, int errorCode)
        {
            foreach (string url in urls)
            {
                try
                {
                    // Macros
                    string uri = url
                        .Replace("[TIMESTAMP]", "" + DateTime.Now.ToString("o", System.Globalization.CultureInfo.InvariantCulture))
                        .Replace("[ERRORCODE]", "" + errorCode);

                    // Call the URL
                    new Url(uri).WithTimeout(10).GetAsync().ContinueWith(t =>
                    {
                        Trace.WriteLine(new LogMessage("ExchangeManager", "ReportError: failed to report error to " + uri + ", code: " + errorCode), LogType.Error.ToString());
                    },
                    TaskContinuationOptions.OnlyOnFaulted);
                }
                catch (Exception e)
                {
                    Trace.WriteLine(new LogMessage("ExchangeManager", "ReportError: failed to report error to " + url + ", code: " + errorCode + ". e: " + e.Message), LogType.Error.ToString());
                }
            }
        }

        private void SetUnwrapRateThreshold(string partner, int seconds)
        {
            if (!string.IsNullOrEmpty(partner))
            {
                lastUnwrapRateLimits[partner] = seconds;
            }
        }

        private void SetUnwrapLast(string partner)
        {
            if (!string.IsNullOrEmpty(partner))
            {
                lastUnwrapDates[partner] = DateTime.Now;
            }
        }


        /// <summary>
        /// Is the unwrap rate limit reached for this parnter?
        /// </summary>
        /// <param name="partner"></param>
        /// <returns></returns>
        private bool IsUnwrapRateThreshold(string partner)
        {
            if (String.IsNullOrEmpty(partner))
            {
                return false;
            }

            if (!lastUnwrapRateLimits.ContainsKey(partner))
            {
                return false;
            }

            int rateLimit = lastUnwrapRateLimits[partner];
            if (rateLimit <= 0)
            {
                return false;
            }

            if (!lastUnwrapDates.ContainsKey(partner))
            {
                return false;
            }

            DateTime lastUnwrap = lastUnwrapDates[partner];
            if (lastUnwrap == null)
            {
                return false;
            }

            return lastUnwrap.AddSeconds(rateLimit) > DateTime.Now;
        }
    }
}
