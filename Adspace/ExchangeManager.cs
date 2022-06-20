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
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private bool isActive;
        private DateTime lastFillDate;
        private DateTime lastPrefetchDate;
        private List<string> prefetchUrls = new List<string>();
        private List<Ad> adBuffet = new List<Ad>();

        public int ShareOfVoice { get; private set; } = 0;
        public int AverageAdDuration { get; private set; } = 0;

        public ExchangeManager()
        {
            lastFillDate = DateTime.Now.AddYears(-1);
            lastPrefetchDate = DateTime.Now.AddYears(-1);
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

            Ad ad = adBuffet[0];

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
            if (!CacheManager.Instance.IsValidPath(ad.File))
            {
                Task.Factory.StartNew(() => ad.Download());
            }

            // We've converted it into a play
            adBuffet.Remove(ad);

            return ad;
        }

        /// <summary>
        /// Prefetch any resources which might play
        /// </summary>
        public void Prefetch()
        {
            List<Url> urls = new List<Url>();
            
            foreach (Url url in urls)
            {
                // Get a JSON string from the URL.
                var result = url.GetJsonAsync<List<string>>().Result;

                // Download each one
                foreach (string fetchUrl in result) {
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

        /// <summary>
        /// Fill the ad buffet
        /// </summary>
        public void Fill()
        {
            Fill(false);
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

            adBuffet.AddRange(Request(url));
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

        /// <summary>
        /// Request new ads
        /// </summary>
        /// <param name="url"></param>
        /// <param name="wrappedAd">If we have a wrapper ad we can resolve it by passing it here.</param>
        /// <returns></returns>
        private List<Ad> Request(Url url, Ad wrappedAd)
        {
            List<Ad> buffet = new List<Ad>();

            // Make a request for new ads
            try
            {
                var response = url.WithTimeout(10).GetAsync().Result;
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
                        ad = new Ad();
                        ad.Id = adNode.Attributes["id"].Value;
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

                        // Make a Url from it.
                        Url adTagUrl = new Url(adTagUrlNode.Value);

                        // Get and impression/error URLs included with this wrap
                        XmlNode errorUrlNode = wrapper.SelectSingleNode("./Error");
                        if (errorUrlNode != null)
                        {
                            ad.ErrorUrls.Add(errorUrlNode.Value);
                        }

                        XmlNode impressionUrlNode = wrapper.SelectSingleNode("./Impression");
                        if (impressionUrlNode != null)
                        {
                            ad.ImpressionUrls.Add(impressionUrlNode.Value);
                        }

                        // Extensions
                        XmlNodeList extensionNodes = wrapper.SelectNodes("./Extension");
                        foreach (XmlNode extensionNode in extensionNodes)
                        {
                            switch (extensionNode.Attributes["type"].Value)
                            {
                                case "prefetch":
                                    if (prefetchUrls.Contains(extensionNode.InnerText))
                                    {
                                        prefetchUrls.Add(extensionNode.InnerText);
                                    }
                                    break;

                                case "validType":
                                    if (!string.IsNullOrEmpty(extensionNode.InnerText))
                                    {
                                        ad.AllowedWrapperTypes = extensionNode.InnerText.Split(',').ToList();
                                    }
                                    break;

                                case "validDuration":
                                    ad.AllowedWrapperDuration = extensionNode.InnerText;
                                    break;
                            }
                        }

                        // Resolve our new wrapper
                        try
                        {
                            buffet.AddRange(Request(adTagUrl, ad));
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
                            ad.Title = titleNode.InnerText;
                        }

                        // Get and impression/error URLs included with this wrap
                        XmlNode errorUrlNode = inlineNode.SelectSingleNode("./Error");
                        if (errorUrlNode != null)
                        {
                            ad.ErrorUrls.Add(errorUrlNode.InnerText);
                        }

                        XmlNode impressionUrlNode = inlineNode.SelectSingleNode("./Impression");
                        if (impressionUrlNode != null)
                        {
                            ad.ImpressionUrls.Add(impressionUrlNode.InnerText);
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
                                ad.Duration = creativeDurationNode.InnerText;
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
                                ad.Url = creativeMediaNode.InnerText;
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
                            if (!ad.AllowedWrapperTypes.Contains("all", StringComparer.OrdinalIgnoreCase)
                                && !ad.AllowedWrapperTypes.Contains(ad.Type.ToLower(), StringComparer.OrdinalIgnoreCase))
                            {
                                ReportError(ad.ErrorUrls, 200);
                                continue;
                            }

                            if (!string.IsNullOrEmpty(ad.AllowedWrapperDuration)
                                && ad.Duration != ad.AllowedWrapperDuration)
                            {
                                ReportError(ad.ErrorUrls, 302);
                                continue;
                            }
                        }

                        // We are good to go.
                        ad.File = "axe_" + ad.Url.Split('/').Last();

                        // Download if necessary
                        if (!CacheManager.Instance.IsValidPath(ad.File))
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
    }
}
