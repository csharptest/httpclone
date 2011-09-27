#region Copyright 2011 by Roger Knapp, Licensed under the Apache License, Version 2.0
/* Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion
using System;
using System.IO;
using System.Net;
using CSharpTest.Net.IO;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CSharpTest.Net.HttpClone.Common
{
    public class HttpRequestUtil
    {
        private static readonly byte[] EmptyBytes = new byte[0];
        private readonly Uri _baseUri;
        private readonly CookieContainer _cookies;
        private readonly WebHeaderCollection _requestHeaders;
        public TimeSpan Timeout;

        public HttpRequestUtil(Uri baseUri)
        {
            Timeout = TimeSpan.FromMinutes(2);
            _baseUri = baseUri;
            _cookies = new CookieContainer();
            _requestHeaders = new WebHeaderCollection();

            UserAgent = "Mozilla/5.0 (Windows; U; MSIE 9.0; WIndows NT 9.0; en-US)";
            RequestUri = _baseUri;
            Content = EmptyBytes;
            ContentType = String.Empty;
            StatusCode = HttpStatusCode.Continue;
            RedirectUri = null;
        }

        public Uri RequestUri { get; private set; }
        public string UserAgent { get; private set; }
        public byte[] Content { get; private set; }
        public string ContentType { get; private set; }
        public string ETag { get; private set; }
        public DateTime? LastModified { get; private set; }
        public Uri RedirectUri { get; private set; }
        public HttpStatusCode StatusCode { get; private set; }
        public CookieContainer Cookies { get { return _cookies; } }
        public WebHeaderCollection RequestHeaders { get { return _requestHeaders; } }
        public WebHeaderCollection ResponseHeaders { get; private set; }

        public HttpStatusCode Head(string path)
        {
            return Exec("HEAD", path, "", EmptyBytes, 0);
        }

        public HttpStatusCode Get(string path)
        {
            return Exec("GET", path, "", EmptyBytes, 0);
        }

        public HttpStatusCode Post(string path, string contentType, byte[] payload, int count)
        {
            return Exec("POST", path, contentType, payload, count);
        }

        private HttpStatusCode Exec(string method, string path, string contentType, byte[] payload, int count)
        {
            RequestUri = new Uri(_baseUri, path);
            Log.Verbose("{0} {1}", method, path);
            Stopwatch timer = new Stopwatch();
            timer.Start();

            HttpWebRequest req = (HttpWebRequest) HttpWebRequest.Create(RequestUri);
            req.CookieContainer = _cookies;
            req.UserAgent = UserAgent;
            req.Method = method;
            
            //Request Configuration
            req.Accept = "*/*";
            req.KeepAlive = true;
            req.SendChunked = false;
            req.AllowAutoRedirect = false;
            req.UseDefaultCredentials = false;
            req.Timeout = (int)Timeout.TotalMilliseconds;
            req.ReadWriteTimeout = (int)Timeout.TotalMilliseconds;
            req.AutomaticDecompression = DecompressionMethods.None;
            req.ServicePoint.UseNagleAlgorithm = false;
            req.ServicePoint.Expect100Continue = false;
            req.ServicePoint.ConnectionLimit = 25;

            HttpWebResponse response = null;
            try
            {
                StatusCode = HttpStatusCode.Continue;
                ContentType = String.Empty;
                Content = EmptyBytes;
                RedirectUri = null;
                LastModified = null;
                ETag = null;
                ResponseHeaders = null;

                if (String.IsNullOrEmpty(RequestHeaders["Accept-Encoding"]))
                    req.Headers.Add("Accept-Encoding", "gzip");

                if (RequestHeaders.Count > 0)
                {
                    foreach (string key in RequestHeaders.Keys)
                        req.Headers.Add(key, RequestHeaders[key]);
                    RequestHeaders.Clear();
                }

                if (method == "POST")
                {
                    req.ContentType = contentType;
                    req.ContentLength = count;
                    if (count > 0)
                    {
                        using (Stream io = req.GetRequestStream())
                            io.Write(payload, 0, count);
                    }
                }

                response = GetResponse(req);
            }
            finally
            {
                timer.Stop();
                Log.Verbose("{0} {1} ({2:n0} ms)", response == null ? 0 : response.StatusCode, path,
                            timer.ElapsedMilliseconds);
            }

            using (response)
            {
                WebHeaderCollection headers = response.Headers;
                ResponseHeaders = headers;
                ContentType = headers[HttpResponseHeader.ContentType] ?? String.Empty;

                StatusCode = response.StatusCode;
                ETag = headers["ETag"];
                DateTime modified;
                if (DateTime.TryParseExact(headers["Last-Modified"], "r", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out modified))
                    LastModified = modified.ToLocalTime();

                if (StatusCode == HttpStatusCode.Redirect || StatusCode == HttpStatusCode.Moved || StatusCode == HttpStatusCode.SeeOther)
                    RedirectUri = new Uri(RequestUri, headers[HttpResponseHeader.Location]);

                if (method == "HEAD")
                    Content = new byte[0];
                else
                {
                    using (Stream input = response.GetResponseStream())
                    {
                        int length;
                        if (headers[HttpResponseHeader.ContentLength] != null &&
                            int.TryParse(headers[HttpResponseHeader.ContentLength], out length))
                        {
                            Content = IOStream.Read(input, length);
                        }
                        else
                        {
                            Content = IOStream.ReadAllBytes(input);
                        }

                        if ((headers[HttpResponseHeader.ContentEncoding] ?? "").IndexOf("gzip", StringComparison.OrdinalIgnoreCase) >= 0
                            || (headers[HttpResponseHeader.TransferEncoding] ?? "").IndexOf("gzip", StringComparison.OrdinalIgnoreCase) >= 0)
                            Content = Content.Decompress();
                    }

                    if (StatusCode == HttpStatusCode.OK && Content.Length == 0)
                    {                       
                        Uri uriRedirect;
                        Match m;

                        if (!String.IsNullOrEmpty(ResponseHeaders["Refresh"])
                            && (m = Regex.Match(ResponseHeaders["Refresh"], @"^\d+\s*;\s*url\s*=\s*([^\s]+)\s*$")).Success
                            && Uri.TryCreate(RequestUri, m.Groups[1].Value, out uriRedirect))
                        {
                            RedirectUri = uriRedirect;
                            StatusCode = HttpStatusCode.Redirect;
                        }
                        else
                        {
                            Log.Warning("The server returned emtpy content for the url = {0}", RequestUri);
                        }
                    }
                }

                return StatusCode;
            }
        }

        [System.Diagnostics.DebuggerNonUserCode]
        private static HttpWebResponse GetResponse(HttpWebRequest request)
        {
            try
            {
                return (HttpWebResponse)request.GetResponse();
            }
            catch (WebException we)
            {
                HttpWebResponse response = we.Response as HttpWebResponse;
                if (response == null)
                    throw;
                Log.Verbose("{0} {1}", (int)response.StatusCode, request.RequestUri);
                return response;
            }
        }

        public static string HtmlEncode(string text)
        {
            return System.Web.HttpUtility.HtmlEncode(text);
        }

        public static string HtmlDecode(string text)
        {
            return System.Web.HttpUtility.HtmlDecode(text);
        }
    }
}
