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
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using CSharpTest.Net.Commands;
using CSharpTest.Net.HttpClone.Common;
using CSharpTest.Net.HttpClone.Publishing;
using CSharpTest.Net.HttpClone.Storage;

namespace CSharpTest.Net.HttpClone.Commands
{
    partial class CommandLine
    {
        [Command(Category = "Hyperlinks", Description = "Lists all the known pages for the specified site.")]
        public void List(
            [Argument("site", "s", Description = "The root http address of the website copy.")]
            string site,
            [Argument("verbose", "v", DefaultValue = false, Description = "Display detailed information about each page or link.")]
            bool details)
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

        [Command(Category = "Hyperlinks", Description = "Lists all the known links for the specified site.")]
        public void Links(
            [Argument("site", "s", Description = "The root http address of the website copy.")]
            string site,
            [Argument("verbose", "v", DefaultValue = true, Description = "Display detailed information about each link.")]
            bool details,
            [Argument("validate", DefaultValue = false, Description = "Check each link and print the status information.")]
            bool validate,
            [Argument("internal", "i", DefaultValue = true, Description = "Set to false or 0 to omit internal links.")]
            bool showInternal,
            [Argument("external", "e", DefaultValue = true, Description = "Set to false or 0 to omit external links.")]
            bool showExternal)
        {
            if (details)
                Console.WriteLine("Count Target");

            using (ContentStorage storage = new ContentStorage(StoragePath(site), true))
            {
                Uri baseUri = new Uri(site, UriKind.Absolute);
                Dictionary<string, int> links = new Dictionary<string, int>(StringComparer.Ordinal);
                ContentParser parser = new ContentParser(storage, baseUri);
                parser.VisitUri += u =>
                {
                    int count;
                    if ((!showInternal && u.IsSameHost(baseUri)) || (!showExternal && !u.IsSameHost(baseUri)))
                        return;

                    links.TryGetValue(u.AbsoluteUri, out count);
                    links[u.AbsoluteUri] = count + 1;
                };
                parser.ProcessAll((r,b) => { });

                List<string> keys = new List<string>(links.Keys);
                keys.Sort();
                foreach (string key in keys)
                {
                    if (validate)
                    {
                        Uri fetch = new Uri(key, UriKind.Absolute);
                        HttpStatusCode result;
                        if(fetch.IsSameHost(baseUri))
                        {
                            ContentRecord rec;
                            result = storage.TryGetValue(fetch.NormalizedPathAndQuery(), out rec)
                                ? (HttpStatusCode)rec.HttpStatus
                                : HttpStatusCode.NotFound;
                        }
                        else
                            result = new HttpRequestUtil(fetch).Head(fetch.PathAndQuery);
                        Console.WriteLine("{0,3} {1,-20} {2}", (int)result, result, fetch);
                    }
                    else if(details)
                        Console.WriteLine("{0,5:n0} {1}", links[key], key);
                    else
                        Console.WriteLine("{0}", key);
                }
            }
        }

        [Command(Category = "Hyperlinks", Description = "Tracks down all pages that reference a specified link.")]
        public void LinkSource(
            [Argument("site", "s", Description = "The root http address of the website copy.")]
            string site,
            [Argument("link", "l", Description = "The target link to search for.")]
            string link)
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

        [Command(Category = "Hyperlinks", Description = "Changes all links from one url to another.")]
        public void Relink(
            [Argument("site", "s", Description = "The root http address of the website copy.")]
            string site,
            [Argument("from", "f", Description = "The original link you want to change.")]
            string sourceLink,
            [Argument("target", "t", Description = "The new link you want to use instead.")]
            string targetLink)
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

        [Command(Category = "Hyperlinks", Description = "Changes all links matching an expression to the evaluated target link.")]
        public void RelinkEx(
            [Argument("site", "s", Description = "The root http address of the website copy.")]
            string site,
            [Argument("expression", "e", Description = "A regular expression to match against the links.")]
            string expression,
            [Argument("target", "t", Description = "The new link, use {0} to insert matched capture groups by ordinal.")]
            string targetLink)
        {
            Regex exp = new Regex(expression, RegexOptions.Singleline);

            using (ContentStorage storage = new ContentStorage(StoragePath(site), false))
            {
                ContentParser parser = new ContentParser(storage, new Uri(site, UriKind.Absolute));
                parser.RewriteUri += u =>
                {
                    Match match = exp.Match(u.OriginalString);
                    if (match.Success)
                    {
                        string newLink = 
                            String.Format(
                                targetLink,
                                match.Groups.Cast<Group>()
                                    .Select(g => g.Value)
                                    .Cast<object>().ToArray()
                            );
                        return new Uri(newLink, UriKind.Absolute);
                    }
                    return u;
                };
                parser.ProcessAll();
            }
        }
    }
}
