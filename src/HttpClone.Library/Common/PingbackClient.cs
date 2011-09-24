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
using System.Text;
using System.Web;
using CSharpTest.Net.Html;
using CSharpTest.Net.HttpClone.Storage;

namespace CSharpTest.Net.HttpClone.Common
{
    public class PingbackClient
    {
        private readonly Uri _targetLink;
        private bool _tested;
        private Uri _hostPingbackApi;

        public PingbackClient(string targetLink)
            : this(new Uri(targetLink, UriKind.Absolute))
        { }
        public PingbackClient(Uri targetLink)
        {
            _targetLink = targetLink;
            LogInfo += m => System.Diagnostics.Trace.WriteLine(m);
            LogError += m => Log.Warning(m);
        }

        public event Action<string> LogInfo;
        public event Action<string> LogError;

        public bool SupportsPingback
        {
            get { return TryGetPingbackUrl(out _hostPingbackApi); }
        }

        public Uri PingbackApi 
        {
            get { return TryGetPingbackUrl(out _hostPingbackApi) ? _hostPingbackApi : null; } 
        }

        public bool TryGetPingbackUrl(out Uri pingbackApi)
        {
            if (_tested)
            {
                pingbackApi = _hostPingbackApi;
                return pingbackApi != null;
            }

            _tested = true;
            if (!TryGetPingbackFromHeader(out _hostPingbackApi))
            {
                TryGetPingbackFromHtml(out _hostPingbackApi);
            }
            pingbackApi = _hostPingbackApi;
            return pingbackApi != null;
        }

        public bool TryGetPingbackFromHeader(out Uri pingbackApi) 
        {
            HttpRequestUtil http = new HttpRequestUtil(_targetLink);

            if (http.Head(_targetLink.PathAndQuery) != System.Net.HttpStatusCode.OK)
                LogError(String.Format("HEAD {0}: {1}/{2}", _targetLink, (int)http.StatusCode, http.StatusCode));
            else
            {
                string pingback = http.ResponseHeaders["X-PINGBACK"];
                if (String.IsNullOrEmpty(pingback))
                    LogError("X-Pingback header not found.");
                else
                {
                    LogInfo("Found X-Pingback: " + pingback);
                    return Uri.TryCreate(pingback, UriKind.Absolute, out pingbackApi);
                }
            }
            pingbackApi = null;
            return false;
        }

        public bool TryGetPingbackFromHtml(out Uri pingbackApi)
        {
            HttpRequestUtil http = new HttpRequestUtil(_targetLink);

            if (http.Get(_targetLink.PathAndQuery) != System.Net.HttpStatusCode.OK)
                LogError(String.Format("GET {0}: {1}/{2}", _targetLink, (int)http.StatusCode, http.StatusCode));
            else if (!http.ContentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                LogError("Invalid content-type, expected text/html, found: " + http.ContentType);
            else
            {
                try
                {
                    HtmlLightDocument htmlDoc = new HtmlLightDocument(Encoding.UTF8.GetString(http.Content));
                    XmlLightElement link = htmlDoc.SelectSingleNode("/html/head/link[@rel='pingback']");
                    if (link == null)
                        LogError("Unable to locate <link rel=\"pingback\" ... in header.");
                    else
                    {
                        string pingback;
                        if (!link.Attributes.TryGetValue("href", out pingback))
                            LogError("Link for rel=pingback is missing the href attribute.");
                        else
                        {
                            LogInfo("Found rel=pingback: " + pingback);
                            return Uri.TryCreate(pingback, UriKind.Absolute, out pingbackApi);
                        }
                    }
                }
                catch (Exception e)
                {
                    LogError(e.Message);
                }
            }
            pingbackApi = null;
            return false;
        }

        public bool SendPingback(string source, out int errorCode, out string serverMessage) 
        { return SendPingback(new Uri(source, UriKind.Absolute), out errorCode, out serverMessage); }

        public bool SendPingback(Uri source, out int errorCode, out string serverMessage)
        {
            Uri pingbackUri;
            if (TryGetPingbackUrl(out pingbackUri))
            {
                return SendPingback(source, _targetLink, pingbackUri, LogError, out errorCode, out serverMessage);
            }

            serverMessage = "The server does not support pingbacks.";
            errorCode = 0;
            return false;
        }

        public static bool SendPingback(Uri source, Uri target, Uri pingbackUri, Action<string> logError, out int errorCode, out string serverMessage)
        {
            HttpRequestUtil http = new HttpRequestUtil(pingbackUri);

            string xmlrpc = @"<?xml version=""1.0""?><methodCall><methodName>pingback.ping</methodName><params>" +
                            @"<param><value><string>{0}</string></value></param>" +
                            @"<param><value><string>{1}</string></value></param></params></methodCall>";

            xmlrpc = String.Format(xmlrpc, HttpUtility.HtmlEncode(source.AbsoluteUri), HttpUtility.HtmlEncode(target.AbsoluteUri));
            byte[] postdata = Encoding.UTF8.GetBytes(xmlrpc);

            if (http.Post(pingbackUri.PathAndQuery, "application/xml", postdata, postdata.Length) != HttpStatusCode.OK)
            {
                logError(String.Format("POST {0}: {1}/{2}", pingbackUri, (int)http.StatusCode, http.StatusCode));
                errorCode = (int)http.StatusCode;
                serverMessage = "The server returned an http error " + (int)http.StatusCode;
            }
            else
            {
                if (!http.ContentType.StartsWith("text/xml", StringComparison.OrdinalIgnoreCase) &&
                    !http.ContentType.StartsWith("application/xml", StringComparison.OrdinalIgnoreCase))
                {
                    logError("Expected content type: application/xml, found: " + http.ContentType);
                }

                try
                {
                    XmlLightDocument doc = new XmlLightDocument(Encoding.UTF8.GetString(http.Content));

                    XmlLightElement error = doc.SelectSingleNode("methodResponse/fault");
                    if (error != null)
                    {
                        XmlLightElement faultCode = error.SelectSingleNode("value/struct/member[name/text()='faultCode']/value/int");
                        XmlLightElement faultString = error.SelectSingleNode("value/struct/member[name/text()='faultString']/value");
                        if (faultCode == null || !int.TryParse(faultCode.InnerText, out errorCode))
                            errorCode = 0;
                        serverMessage = faultString != null ? faultString.InnerText : "Unknown Error";
                    }
                    else
                    {
                        serverMessage = doc.SelectRequiredNode("/methodResponse/params/param/value").InnerText;
                        errorCode = 0;
                        return true;
                    }
                }
                catch (Exception e)
                {
                    logError("Error parsing response: " + e.Message);
                    serverMessage = "Unable to parse xml response.";
                    errorCode = 0;
                }
            }
            return false;
        }

        public void Playback(Uri sourceSite, string stateFile)
        {
            Uri pingbackApi;
            if(!TryGetPingbackFromHeader(out pingbackApi))
                throw new ArgumentException("The site does not have an X-Pingback header.");

            if (!Directory.Exists(Path.GetDirectoryName(stateFile)))
                Directory.CreateDirectory(Path.GetDirectoryName(stateFile));

            using(Stream stream = new FileStream(stateFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
            {
                stream.Seek(0, SeekOrigin.End);
                HttpRequestUtil http = new HttpRequestUtil(sourceSite);
                http.RequestHeaders["If-None-Match"] = String.Format("\"position:{0}\"", stream.Position);

                if (http.Get(sourceSite.PathAndQuery) == HttpStatusCode.NotModified)
                    return;

                if (http.StatusCode != HttpStatusCode.OK)
                    throw new ApplicationException(String.Format("Unexpected http result: {0}/{1}", (int)http.StatusCode, http.StatusCode));

                PingbackRecord response = PingbackRecord.ParseFrom(http.Content);
                foreach (PingbackInfo ping in response.RecordsList)
                {
                    try
                    {
                        int errorCode;
                        string serverMessage;
                        Uri source = new Uri(ping.SourceUri, UriKind.Absolute);
                        Uri target = new Uri(_targetLink, ping.TargetUri);
                        if (!SendPingback(source, target, pingbackApi, LogError, out errorCode, out serverMessage))
                            LogError(String.Format("Failed to register pingback from {0}, {1}", ping.SourceUri, serverMessage));
                    }
                    catch (Exception e)
                    {
                        LogError(String.Format("Failed to register pingback from {0}, {1}", ping.SourceUri, e.Message));
                    }
                }

                response.WriteTo(stream);
            }
        }
    }
}
