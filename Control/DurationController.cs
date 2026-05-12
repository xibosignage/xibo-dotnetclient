/**
 * Copyright (C) 2023 Xibo Signage Ltd
 *
 * Xibo - Digital Signage - https://xibosignage.com
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
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using System.Reflection;
using System;
using System.Diagnostics;

namespace XiboClient.Control
{
    internal static class RequestDataFallbackHelper
    {
        public static async System.Threading.Tasks.Task<T> GetRequestDataWithQueryFallbackAsync<T>(this IHttpContext context)
            where T : class, new()
        {
            var data = await GetRequestDataFromBodyAsync<T>(context);

            if (data != null)
            {
                return data;
            }

            data = TryGetRequestDataFromQuery<T>(context);

            if (data != null)
            {
                TryPopulateIdFromReferrer(context, data);
            }

            return data;
        }

        public static async System.Threading.Tasks.Task<T> GetRequestDataWithQueryOrReferrerFallbackAsync<T>(this IHttpContext context)
            where T : class, new()
        {
            var data = await GetRequestDataFromBodyAsync<T>(context);

            if (data != null)
            {
                TryPopulateIdFromReferrer(context, data);
                return data;
            }

            data = TryGetRequestDataFromQuery<T>(context);

            if (data != null)
            {
                TryPopulateIdFromReferrer(context, data);
                return data;
            }

            return TryGetRequestDataFromReferrer<T>(context);
        }

        private static async System.Threading.Tasks.Task<T> GetRequestDataFromBodyAsync<T>(IHttpContext context)
            where T : class
        {
            try
            {
                return await context.GetRequestDataAsync<T>();
            }
            catch
            {
                return null;
            }
        }

        private static T TryGetRequestDataFromQuery<T>(IHttpContext context)
            where T : class, new()
        {
            try
            {
                var fallback = new T();
                var hasValue = false;

                foreach (var property in typeof(T).GetProperties())
                {
                    if (!property.CanWrite)
                    {
                        continue;
                    }

                    var value = context.Request.QueryString[property.Name];

                    if (string.IsNullOrEmpty(value))
                    {
                        continue;
                    }

                    if (TrySetProperty(fallback, property, value))
                    {
                        hasValue = true;
                    }
                }

                return hasValue ? fallback : null;
            }
            catch
            {
                return null;
            }
        }

        private static void TryPopulateIdFromReferrer<T>(IHttpContext context, T target)
            where T : class
        {
            try
            {
                var idProperty = typeof(T).GetProperty("id");

                if (idProperty == null || !idProperty.CanWrite)
                {
                    return;
                }

                var currentValue = idProperty.GetValue(target, null);

                if (HasMeaningfulValue(idProperty, currentValue))
                {
                    return;
                }

                var referrerId = TryGetWidgetIdFromReferrer(context);

                if (string.IsNullOrEmpty(referrerId))
                {
                    return;
                }

                TrySetProperty(target, idProperty, referrerId);
            }
            catch
            {
                // Fallback must not prevent the original request from being handled.
            }
        }

        private static T TryGetRequestDataFromReferrer<T>(IHttpContext context)
            where T : class, new()
        {
            try
            {
                var idProperty = typeof(T).GetProperty("id");

                if (idProperty == null || !idProperty.CanWrite)
                {
                    return null;
                }

                var idPart = TryGetWidgetIdFromReferrer(context);

                if (string.IsNullOrEmpty(idPart))
                {
                    return null;
                }

                var fallback = new T();

                return TrySetProperty(fallback, idProperty, idPart) ? fallback : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool HasMeaningfulValue(PropertyInfo property, object value)
        {
            if (value == null)
            {
                return false;
            }

            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (propertyType == typeof(string))
            {
                return !string.IsNullOrEmpty(value as string);
            }

            try
            {
                return !value.Equals(Activator.CreateInstance(propertyType));
            }
            catch
            {
                return true;
            }
        }

        private static string TryGetWidgetIdFromReferrer(IHttpContext context)
        {
            try
            {
                var referrer = context.Request.UrlReferrer;

                if (referrer == null)
                {
                    return null;
                }

                var fileName = System.IO.Path.GetFileName(referrer.AbsolutePath);

                if (string.IsNullOrEmpty(fileName))
                {
                    return null;
                }

                if (!fileName.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return fileName.Substring(0, fileName.Length - 4);
            }
            catch
            {
                return null;
            }
        }

        private static bool TrySetProperty<T>(T target, PropertyInfo property, string value)
        {
            try
            {
                var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                object convertedValue;

                if (propertyType == typeof(string))
                {
                    convertedValue = value;
                }
                else if (propertyType.IsEnum)
                {
                    convertedValue = Enum.Parse(propertyType, value, true);
                }
                else
                {
                    convertedValue = Convert.ChangeType(value, propertyType);
                }

                property.SetValue(target, convertedValue, null);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    class DurationController : WebApiController
    {
        private EmbeddedServer _parent;

        public DurationController(EmbeddedServer parent)
        {
            _parent = parent;
        }

        /// <summary>
        /// Expire the current Widget
        /// </summary>
        [Route(HttpVerbs.Post, "/expire")]
        public async void Expire()
        {
            if (_parent == null)
            {
                LogMessage.Info("DurationController", "Expire", "Web server closing");
                return;
            }

            try
            {
                var data = await HttpContext.GetRequestDataWithQueryOrReferrerFallbackAsync<DurationRequest>();

                if (data == null)
                {
                    Trace.WriteLine(new LogMessage("DurationController", "Expire: unable to parse request data"), LogType.Error.ToString());
                    return;
                }

                _parent.Duration("expire", data.id, 0);
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("DurationController", "Expire: unable to parse request: " + e.Message), LogType.Error.ToString());
                LogMessage.Trace("DurationController", "Expire", e.StackTrace.ToString());
            }
        }

        /// <summary>
        /// Extend the current Widget
        /// </summary>
        [Route(HttpVerbs.Post, "/extend")]
        public async void Extend()
        {
            if (_parent == null)
            {
                LogMessage.Info("DurationController", "Expire", "Web server closing");
                return;
            }

            try
            {
                var data = await HttpContext.GetRequestDataWithQueryFallbackAsync<DurationRequest>();

                if (data == null)
                {
                    Trace.WriteLine(new LogMessage("DurationController", "Extend: unable to parse request data"), LogType.Error.ToString());
                    return;
                }

                _parent.Duration("extend", data.id, data.duration);
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("DurationController", "Extend: unable to parse request: " + e.Message), LogType.Error.ToString());
                LogMessage.Trace("DurationController", "Extend", e.StackTrace.ToString());
            }
        }

        /// <summary>
        /// Set the current Widget's duration
        /// </summary>
        [Route(HttpVerbs.Post, "/set")]
        public async void Set()
        {
            if (_parent == null)
            {
                LogMessage.Info("DurationController", "Expire", "Web server closing");
                return;
            }

            try
            {
                var data = await HttpContext.GetRequestDataWithQueryFallbackAsync<DurationRequest>();

                if (data == null)
                {
                    Trace.WriteLine(new LogMessage("DurationController", "Set: unable to parse request data"), LogType.Error.ToString());
                    return;
                }

                _parent.Duration("set", data.id, data.duration);
            }
            catch (Exception e)
            {
                Trace.WriteLine(new LogMessage("DurationController", "Set: unable to parse request: " + e.Message), LogType.Error.ToString());
                LogMessage.Trace("DurationController", "Set", e.StackTrace.ToString());
            }
        }
    }
}
