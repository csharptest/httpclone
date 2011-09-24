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
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using CSharpTest.Net.Html;
using CSharpTest.Net.IO;

namespace CSharpTest.Net.HttpClone
{
    public static class Extensions
    {
        public static ulong ToUInt64(this Guid guid)
        {
            byte[] binary = guid.ToByteArray();
            return BitConverter.ToUInt64(binary, 0) ^ BitConverter.ToUInt64(binary, 8);
        }

        public static bool IsSameHost(this Uri baseUri, Uri other)
        {
            if ((!StringComparer.OrdinalIgnoreCase.Equals(baseUri.Host, other.Host) ||
                 baseUri.Port != other.Port || baseUri.Scheme != other.Scheme))
                return false;
            return true;
        }

        public static string NormalizedPathAndQuery(this Uri uri)
        {
            string lnkPath = uri.IsAbsoluteUri ? uri.PathAndQuery : uri.OriginalString;
            if (lnkPath.LastIndexOf('#') > 0)
                lnkPath = lnkPath.Substring(0, lnkPath.LastIndexOf('#'));
            lnkPath = lnkPath.TrimEnd('?', '&');

            //One ugly case is default documents.  Currently there is only one, '/Default.aspx' and only
            //in the development runtime (piece-o-shit).  At some point we need to merge these in a more
            //robust way.  Essentially we should treat '/default.aspx?a=b' as '/?a=b' and the like.
            foreach(string defaultDocName in new string[] { "default.aspx" })
            {
                if (lnkPath.EndsWith("/" + defaultDocName, StringComparison.OrdinalIgnoreCase))
                    lnkPath = lnkPath.Substring(0, lnkPath.Length - defaultDocName.Length);
                else
                {
                    int pos = lnkPath.IndexOf("/" + defaultDocName + "?", 0, StringComparison.OrdinalIgnoreCase);
                    if(pos >= 0 && pos == lnkPath.IndexOf('?') - defaultDocName.Length - 1)
                        lnkPath = lnkPath.Remove(pos + 1, defaultDocName.Length);
                }
            }

            return lnkPath;
        }

        public static bool TryCompress(this byte[] bytes, out byte[] gzipped)
        {
            gzipped = null;
            using (MemoryStream ms = new MemoryStream())
            {
                using (GZipStream gz = new GZipStream(ms, CompressionMode.Compress, true))
                    gz.Write(bytes, 0, bytes.Length);

                if (ms.Position > bytes.Length * 0.9)
                    return false;

                gzipped = ms.ToArray();
                return true;
            }
        }

        public static byte[] Decompress(this byte[] bytes, long size)
        {
            using (MemoryStream output = new MemoryStream((int)size))
            using (MemoryStream input = new MemoryStream(bytes, false))
            {
                using (GZipStream gz = new GZipStream(input, CompressionMode.Decompress, false))
                    IOStream.CopyStream(gz, output, size);
                return output.ToArray();
            }
        }

        public static byte[] Decompress(this byte[] bytes)
        {
            using (MemoryStream output = new MemoryStream())
            using (MemoryStream input = new MemoryStream(bytes, false))
            {
                using (GZipStream gz = new GZipStream(input, CompressionMode.Decompress, false))
                    IOStream.CopyStream(gz, output);
                return output.ToArray();
            }
        }

        public static IEnumerable<T> SafeEnumeration<T>(this IEnumerable<T> value)
        { return value ?? new T[0]; }

        public static XmlLightElement SelectRequiredNode(this XmlLightElement node, string xpath)
        {
            if (node == null)
                throw new ArgumentNullException("node", String.Format("The element node is null searching for '{0}'", xpath));
            if (String.IsNullOrEmpty(xpath))
                throw new ArgumentNullException("xpath", String.Format("The xpath is {0}.", xpath == null ? "null" : "empty"));
            XmlLightElement result = node.SelectSingleNode(xpath);
            if(result == null)
                throw new ArgumentException(String.Format("The xpath element was not found: '{0}' in <{1}>.", xpath, node.TagName));
            return result;
        }

        public static bool TrySelectNode(this XmlLightElement node, string xpath, out XmlLightElement result)
        {
            if (node == null)
                throw new ArgumentNullException("node", String.Format("The element node is null searching for '{0}'", xpath));
            if (String.IsNullOrEmpty(xpath))
                throw new ArgumentNullException("xpath", String.Format("The xpath is {0}.", xpath == null ? "null" : "empty"));
            result = node.SelectSingleNode(xpath);
            return result != null;
        }

    }

    namespace HtmlExtensions
    {
        public static class HtmlString
        {
            public static string ToNbsp(this string str)
            {
                return str.Replace(" ", "\xa0");
            }

            public static string AddQuery(this string uri, string argument, int value)
            {
                return AddQuery(uri, argument, value.ToString());
            }

            public static string AddQuery(this string uri, string argument, string value)
            {
                if(String.IsNullOrEmpty(uri))
                    uri = "/";
                else
                    uri = uri.TrimEnd('&', '?');

                int pos = uri.LastIndexOf('?');
                if (pos < 0)
                    return String.Format("{0}?{1}={2}", uri, Uri.EscapeDataString(argument), Uri.EscapeDataString(value));

                return String.Format("{0}&{1}={2}", uri, Uri.EscapeDataString(argument), Uri.EscapeDataString(value));
            }
        }
    }

}
