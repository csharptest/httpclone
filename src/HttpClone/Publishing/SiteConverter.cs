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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CSharpTest.Net.HttpClone.Common;
using CSharpTest.Net.HttpClone.Storage;
using System.Collections.Generic;
using CSharpTest.Net.Interfaces;

namespace CSharpTest.Net.HttpClone.Publishing
{
    class SiteConverter : IDisposable
    {
        private readonly Uri _baseUri;
        private readonly ContentStorage _content;
        private readonly Regex CleanupRegex;
        private readonly MimeInfoMap _mime;

        public SiteConverter(string storagePath, string baseUri)
        {
            RebaseLinks = true;
            _baseUri = new Uri(baseUri, UriKind.Absolute);
            HttpCloneConfig config = Config.ReadConfig(_baseUri, storagePath);
            _mime = new MimeInfoMap(_baseUri, storagePath);
            CleanupRegex = new Regex(config.BadNameCharsExpression ?? @"[^\w]+", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            _content = new ContentStorage(storagePath, true);
        }

        public bool Overwrite { get; set; }
        public bool RebaseLinks { get; set; }
        public bool Reformat { get; set; }

        public void Dispose()
        {
            _content.Dispose();
        }

        public void CopyTo(ContentStorage writer, Func<ContentRecord, byte[], byte[]> fnprocess)
        {
            bool success;
            foreach(KeyValuePair<string, ContentRecord> item in _content)
            {
                ContentRecord.Builder builder = item.Value.ToBuilder();
                if(item.Value.HasContentStoreId)
                {
                    byte[] data = _content.ReadContent(item.Value, true);
                    if (fnprocess != null)
                        data = fnprocess(item.Value, data);

                    using (ITransactable trans = writer.WriteContent(builder, data))
                    {
                        success = Overwrite 
                                ? writer.AddOrUpdate(item.Key, builder.Build())
                                : writer.Add(item.Key, builder.Build());
                        if (success)
                            trans.Commit();
                    }
                }
                else
                    success = Overwrite
                            ? writer.AddOrUpdate(item.Key, builder.Build())
                            : writer.Add(item.Key, builder.Build());
                
                if(!success)
                    Console.Error.WriteLine("Path already exists " + item.Key);
            }
        }

        public void ConvertTo(string target, ContentStorage writer)
        {
            Uri tgt = new Uri(target, UriKind.Absolute);
            ContentParser processor = new ContentParser(writer, _baseUri);

            processor.Reformat = Reformat;
            processor.RelativeUri = false;
            if (RebaseLinks)
            {
                processor.RewriteUri +=
                    uri =>
                        {
                            if (uri.IsSameHost(_baseUri))
                                return new Uri(tgt, uri.PathAndQuery);
                            return uri;
                        };
            }
            CopyTo(writer, processor.ProcessFile);
        }

        public Dictionary<string, string> GetFriendlyNames()
        {
            Dictionary<string, string> used = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> renamed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, ContentRecord> item in _content)
            {
                if (!item.Value.HasContentStoreId)
                    continue;

                Uri uri = new Uri(_baseUri, item.Key);
                string name, ext = _mime.GetFileExtension(item.Value.MimeType);

                if (String.IsNullOrEmpty(uri.Query) || uri.Query == "?")
                {
                    name = uri.LocalPath;
                    if (Regex.IsMatch(name, @"\w+\.\w{1,10}$"))
                        ext = Path.GetExtension(name);
                }
                else
                {
                    string title = String.Empty;
                    Regex titleMatch;
                    int groupId;

                    if (_mime.TryGetTitleExpression(item.Value.MimeType, out titleMatch, out groupId))
                    {
                        string contents = Encoding.UTF8.GetString(_content.ReadContent(item.Value, true));
                        title = String.Empty;
                        foreach (Match m in titleMatch.Matches(contents).Cast<Match>()
                            .Where(m=>m.Groups[groupId].Success).Take(1))
                        {
                            title = m.Groups[groupId].Value;
                            title = HttpRequestUtil.HtmlDecode(title);
                        }
                    }

                    title = CleanupRegex.Replace(title, "_").Trim('_');
                    if (String.IsNullOrEmpty(title) && !String.IsNullOrEmpty(uri.Query))
                        title = CleanupRegex.Replace(uri.Query, "_").Trim('_');
                    if (String.IsNullOrEmpty(title))
                        title = item.Value.ContentStoreId.ToString("x8");

                    name = Path.Combine(String.Join("/", uri.Segments), title);
                }

                name = name.Replace('\\', '/');
                if (name.EndsWith("/"))
                    name += "index";
                if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    name += ext;

                string test = name;
                if(used.ContainsKey(test))
                {
                    test = name.Insert(name.Length - ext.Length, CleanupRegex.Replace(uri.Query ?? "", "_").TrimEnd('_'));
                    int ix = 1;
                    while (used.ContainsKey(test))
                        test = name.Insert(name.Length - ext.Length, String.Format("_{0}", ix++));

                    name = test;
                }

                used.Add(name, item.Key);
                renamed.Add(item.Key, name);
            }
            return renamed;
        }

        public void Export(string directory)
        {
            Dictionary<string, string> renamed = GetFriendlyNames();
            Uri location = new Uri(directory, UriKind.Absolute);

            ContentParser parser = new ContentParser(_content, _baseUri);
            parser.RewriteElement +=
                e =>
                {
                    return e;
                };

            parser.RewriteUri +=
                uri =>
                {
                    string rename;
                    if (uri.IsSameHost(_baseUri))
                    {
                        if (renamed.TryGetValue(uri.NormalizedPathAndQuery(), out rename))
                        {
                            if (RebaseLinks)
                                return new Uri(location, rename.TrimStart('/', '\\'));
                            else
                                return new Uri(_baseUri, rename.TrimStart('/', '\\'));
                        }
                    }
                    return uri;
                };

            parser.RewriteAll = true;
            parser.RelativeUri = RebaseLinks;
            parser.Reformat = Reformat;
            parser.IndentChars = "  ";
            parser.ProcessAll(
                (r, b) =>
                {
                    string path;
                    if (renamed.TryGetValue(r.ContentUri, out path))
                    {
                        string file = Path.Combine(directory, path.Replace('/', '\\').TrimStart('\\'));
                        if (!Directory.Exists(Path.GetDirectoryName(file)))
                            Directory.CreateDirectory(Path.GetDirectoryName(file));
                        File.WriteAllBytes(file, b);
                    }
                }
            );
        }
    }
}
