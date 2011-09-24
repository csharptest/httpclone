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
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using CSharpTest.Net.Html;
using CSharpTest.Net.Crypto;
using CSharpTest.Net.Synchronization;
using CSharpTest.Net.HttpClone.Storage;
using CSharpTest.Net.IO;

namespace CSharpTest.Net.HttpClone.Services
{
    class PingbackHandler : IRequestHandler
    {
        public static readonly string PingbackUrl = "/api/pingback";
        public static readonly string PingbackFile = "pingbacks.dat";
        private readonly ContentState _content;
        private readonly string _filename;
        private readonly string _lockName;

        public PingbackHandler(ContentState content)
        {
            _content = content;
            _filename = Path.Combine(_content.StoragePath, PingbackFile);
            _lockName = BitConverter.ToString(Hash.MD5(Encoding.UTF8.GetBytes(_filename)).ToArray());
        }

        private void RecordPingback(Uri source, Uri dest)
        {
            try
            {
                using (new MutexLock(2000, _lockName))
                using (Stream output = new FileStream(_filename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                {
                    output.Seek(0, SeekOrigin.End);
                    new PingbackRecord.Builder()
                        .AddRecords(
                            new PingbackInfo.Builder()
                                .SetSourceUri(source.AbsoluteUri)
                                .SetTargetUri(dest.PathAndQuery)
                                .SetWhen(DateTime.Now)
                        )
                        .Build()
                        .WriteTo(output);
                }
            }
            catch(Exception e)
            {
                Log.Error(e, "Failed to record pingback from {0}", source);
            }
        }

        public bool IsMatch(string method, string rawUrl)
        {
            return StringComparer.Ordinal.Equals(rawUrl, PingbackUrl);
        }

        public IContentResponse GetResponse(string method, string rawUrl, NameValueCollection headers, Stream inputStream)
        {
            if (method == "POST")
                return Post(method, rawUrl, headers, inputStream);
            return Get(method, rawUrl, headers, inputStream);
        }

        public IContentResponse Get(string method, string rawUrl, NameValueCollection headers, Stream inputStream)
        {
            long length;
            using (new MutexLock(2000, _lockName))
            {
                using (Stream output = new FileStream(_filename, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read))
                    length = output.Length;
            }
            using (Stream rawinput = new FileStream(_filename, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite))
            {
                long position = 0;
                Match m = Regex.Match(headers["If-None-Match"] ?? String.Empty, "^\"position:(?<position>\\d+)\"$");
                if (m.Success)
                    position = Math.Min(Math.Max(0, long.Parse(m.Groups["position"].Value)), length);

                using (Stream input = new ClampedStream(rawinput, position, Math.Max(0, length - position)))
                {
                    PingbackRecord rec = PingbackRecord.ParseFrom(input);
                    return new DynamicResponse("application/vnd.google.protobuf", rec.ToByteArray())
                               {
                                   ETag = String.Format("position:{0}", length),
                                   LastModified = rec.RecordsCount > 0
                                                      ? rec.RecordsList[rec.RecordsCount - 1].When
                                                      : DateTime.Now,
                               };
                }
            }
        }

        public IContentResponse Post(string method, string rawUrl, NameValueCollection headers, Stream inputStream)
        {
            try
            {
                try
                {
                    Assert("POST", method);
                    Assert("application/xml", headers["Content-Type"]);
                    if (int.Parse(headers["Content-Length"]) > 4096)
                        throw new ArgumentException("Content exceeds limit of 4096 bytes.");

                    string xmessage;
                    using (TextReader rdr = new StreamReader(inputStream, Encoding.UTF8, false))
                        xmessage = rdr.ReadToEnd();

                    XmlLightDocument doc = new XmlLightDocument(xmessage);
                    Assert("methodCall", doc.Root.LocalName);
                    Assert("pingback.ping", doc.SelectRequiredNode("/methodCall/methodName").InnerText);
                    XmlLightElement[] args = doc.Select("/methodCall/params/param").ToArray();
                    if (args.Length != 2)
                        throw new ArgumentException("Invalid number of arguments, expected: 2");

                    Uri source, dest;
                    if (!Uri.TryCreate(args[0].InnerText, UriKind.Absolute, out source) ||
                        !Uri.TryCreate(args[1].InnerText, UriKind.Absolute, out dest))
                        throw new ArgumentException("Invalid uri format.");

                    RecordPingback(source, dest);

                    return new DynamicResponse("application/xml",
                        Encoding.UTF8.GetBytes(@"<?xml version=""1.0""?><methodResponse><params><param><value>Accepted</value></param></params></methodResponse>")
                        );
                }
                catch (ArgumentException e)
                {
                    return new DynamicResponse("application/xml",
                        Encoding.UTF8.GetBytes(
                        String.Format(
@"<?xml version=""1.0""?>
<methodResponse>
  <fault>
    <value>
      <struct>
        <member><name>faultCode</name><value><int>{0}</int></value></member>
        <member><name>faultString</name><value><string>{1}</string></value></member>
      </struct>
    </value>
  </fault>
</methodResponse>", 0, HttpUtility.HtmlEncode(e.Message))));
                }
            }
            catch (Exception err)
            {
                Log.Error(err);
                return HttpErrorHandler.InternalServerError.GetResponse(method, rawUrl, headers, inputStream);
            }
        }

        void Assert(string expected, string actual)
        {
            if (!StringComparer.Ordinal.Equals(expected, actual))
            {
                throw new ArgumentException(String.Format("Expected {0} but was {1}", expected, actual));
            }
        }
    }
}
