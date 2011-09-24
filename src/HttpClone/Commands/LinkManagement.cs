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
using System.ComponentModel;
using System.Text.RegularExpressions;
using CSharpTest.Net.Commands;
using CSharpTest.Net.HttpClone.Publishing;
using CSharpTest.Net.HttpClone.Storage;

namespace CSharpTest.Net.HttpClone.Commands
{
    partial class CommandLine
    {
        public void List(string site, [DefaultValue(false)]bool details)
        {
            string prefix = new Uri(site, UriKind.Absolute).NormalizedPathAndQuery();
            using (ContentStorage storage = new ContentStorage(StoragePath(site), true))
            {
                if(details)
                    Console.WriteLine("Http Modified-On Content-Type Length(Kb) Zip {0,-40} Redirect", "Path");

                foreach (KeyValuePair<string, ContentRecord> kv in storage)
                {
                    if (!kv.Key.StartsWith(prefix))
                        continue;

                    if (details)
                        Console.WriteLine("{0}  {1,11:yyyy-MM-dd} {2,12} {3,10:n1} {4,-3} {5,-40} {6}", 
                            kv.Value.HttpStatus, 
                            kv.Value.HasDateModified ? kv.Value.DateModified : kv.Value.DateCreated, 
                            kv.Value.ContentType.Split(';')[0],
                            kv.Value.ContentLength / 1024.0, 
                            kv.Value.HasCompressedLength ? "Y" : "N", 
                            kv.Value.ContentUri, 
                            kv.Value.ContentRedirect);
                    else
                        Console.WriteLine("{0}", kv.Key);
                }
            }
        }

        public void Links(string site, [DefaultValue(true)]bool details, [DefaultValue(true)]bool showInternal, [DefaultValue(true)]bool showExternal, [DefaultValue(false)]bool validate)
        {
            if (details)
                Console.WriteLine("Count Target");

            using (ContentStorage storage = new ContentStorage(StoragePath(site), true))
            {
                Dictionary<string, int> links = new Dictionary<string, int>(StringComparer.Ordinal);
                ContentParser parser = new ContentParser(storage, new Uri(site, UriKind.Absolute));
                parser.VisitUri += u =>
                {
                    int count;
                    links.TryGetValue(u.OriginalString, out count);
                    links[u.OriginalString] = count + 1;
                };
                parser.ProcessAll((r,b) => { });

                List<string> keys = new List<string>(links.Keys);
                keys.Sort();
                foreach (string key in keys)
                {
                    Console.WriteLine("{0,5:n0} {1}", links[key], key);
                }
            }
        }

        public void LinkSource(string site, string link)
        {
            using (ContentStorage storage = new ContentStorage(StoragePath(site), true))
            {
                ContentParser parser = new ContentParser(storage, new Uri(site, UriKind.Absolute));
                ContentRecord rec = ContentRecord.DefaultInstance;
                parser.ContextChanged += r => rec = r;
                parser.VisitUri += u =>
                {
                    if (StringComparer.Ordinal.Equals(link, u.OriginalString))
                        Console.WriteLine(rec.ContentUri);
                };
                parser.ProcessAll((r, b) => { });
            }
        }

        public void Relink(string site, string sourceLink, string targetLink)
        {
            Uri sourceUri = new Uri(sourceLink, UriKind.Absolute);
            Uri targetUri = new Uri(targetLink, UriKind.Absolute);

            using (ContentStorage storage = new ContentStorage(StoragePath(site), false))
            {
                ContentParser parser = new ContentParser(storage, new Uri(site, UriKind.Absolute));
                parser.RewriteUri += u =>
                {
                    if (sourceUri == u)
                        return targetUri;
                    return u;
                };
                parser.ProcessAll();
            }
        }

        public void RelinkMatching(string site, string expression, string targetLink)
        {
            Regex match = new Regex(expression, RegexOptions.Singleline);
            Uri targetUri = new Uri(targetLink, UriKind.Absolute);

            using (ContentStorage storage = new ContentStorage(StoragePath(site), false))
            {
                ContentParser parser = new ContentParser(storage, new Uri(site, UriKind.Absolute));
                parser.RewriteUri += u =>
                {
                    if (match.IsMatch(u.OriginalString))
                        return targetUri;
                    return u;
                };
                parser.ProcessAll();
            }
        }
    }
}
