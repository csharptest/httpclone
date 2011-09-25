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
using System.Net;
using CSharpTest.Net.Commands;
using CSharpTest.Net.HttpClone.Publishing;
using CSharpTest.Net.HttpClone.Storage;
using CSharpTest.Net.IO;

namespace CSharpTest.Net.HttpClone.Commands
{
    partial class CommandLine
    {
        [Command(Category = "Content", Description = "Removes a single page from the site.")]
        public void Remove(
            [Argument("page", "p", Description = "The full http address of the page you want to remove.")]
            string url)
        {
            using (ContentStorage store = new ContentStorage(StoragePath(url), false))
                store.Remove(new Uri(url, UriKind.Absolute).NormalizedPathAndQuery());
        }

        [Command(Category = "Content", Description = "Rename a content url from one location to another.")]
        public void Rename(
            [Argument("page", "p", Description = "The full http address of the page to move the source content to.")]
            string targetLink,
            [Argument("source", "s", Description = "The full http address of the page you want to move.")]
            string sourceLink,
            [Argument("redirect", "r", DefaultValue = true, Description = "True to insert a redirect after moving the content.")]
            bool redirect)
        {
            Uri targetUri = new Uri(targetLink, UriKind.Absolute);
            Uri sourceUri = new Uri(sourceLink, UriKind.Absolute);
            Check.Assert<InvalidOperationException>(sourceUri.IsSameHost(targetUri), "The source and target should be in the same site.");

            using (ContentStorage store = new ContentStorage(StoragePath(sourceLink), false))
            {
                store.Rename(sourceUri.NormalizedPathAndQuery(), targetUri.NormalizedPathAndQuery());
                if (redirect)
                {
                    DateTime time = DateTime.Now;
                    ContentRecord.Builder builder = store.New(sourceUri.NormalizedPathAndQuery(), time);
                    builder
                        .SetHttpStatus((uint) HttpStatusCode.Redirect)
                        .SetContentRedirect(targetUri.NormalizedPathAndQuery())
                        ;
                    store.Add(builder.ContentUri, builder.Build());
                }
            }
        }

        [Command(Category = "Content", Description = "Search the site for duplicate content and redirect or remove the duplicates.")]
        public void Deduplicate(
            [Argument("site", "s", Description = "The root http address of the website copy.")]
            string site,
            [Argument("remove", "r", DefaultValue = false, Description = "True to remove the page and modify source links, otherwise inserts a redirect.")]
            bool remove,
            [Argument("noprompt", "q", DefaultValue = false, Description = "True to stop prompt for confirmation before changing content.")]
            bool noPrompt)
        {
            using (ContentStorage store = new ContentStorage(StoragePath(site), false))
            {
                Dictionary<string, string> replacements = new Dictionary<string, string>(StringComparer.Ordinal);
                Dictionary<string, string> hashes = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, ContentRecord> item in store)
                {
                    if (item.Value.HasHashContents)
                    {
                        string original;
                        if (hashes.TryGetValue(item.Value.HashContents, out original))
                        {
                            replacements[item.Key] = original;
                            Console.WriteLine("{0,-38} => {1,-38}", item.Key, original);
                        }
                        else
                            hashes.Add(item.Value.HashContents, item.Key);
                    }
                }

                if (replacements.Count > 0 &&
                    (noPrompt || new ConfirmPrompt().Continue("Replace all of the above links")))
                {
                    Uri baseUri = new Uri(site, UriKind.Absolute);

                    if (remove)
                    {
                        ContentParser parser = new ContentParser(store, baseUri);
                        parser.RewriteUri += u =>
                        {
                            string target;
                            if (u.IsSameHost(baseUri) && replacements.TryGetValue(u.NormalizedPathAndQuery(), out target))
                                return new Uri(baseUri, target);
                            return u;
                        };
                        parser.ProcessAll();
                    }
                    foreach (string removed in replacements.Keys)
                    {
                        ContentRecord rec = store[removed];
                        store.Remove(removed);
                        if (!remove)
                        {
                            ContentRecord.Builder builder = rec.ToBuilder();
                            builder
                                .ClearCompressedLength()
                                .ClearContentLength()
                                .ClearContentStoreId()
                                .ClearContentType()
                                .ClearHashContents()
                                .SetHttpStatus((uint)HttpStatusCode.Redirect)
                                .SetContentRedirect(replacements[removed])
                                ;
                            store.Add(removed, builder.Build());
                        }
                    }
                }
            }
        }

        [Command(Category = "Content", Description = "Imports a url, and then modified all links to source to point to the target page.")]
        public void Internalize(
            [Argument("page", "p", Description = "The full http address of the page to save the source content to.")]
            string targetLink,
            [Argument("source", "s", Description = "The full http address of the page you want to import.")]
            string sourceLink,
            [Argument("recursive", "r", DefaultValue = false, Description = "True to recursivly import all links within the same domain.")]
            bool recursive,
            [Argument("noprompt", "q", DefaultValue = false, Description = "True to stop prompt for confirmation before overwriting content.")]
            bool noPrompt)
        {
            Import(targetLink, sourceLink, recursive, noPrompt);
            Relink(targetLink, sourceLink, targetLink);
        }

        [Command(Category = "Content", Description = "Imports a url to a specified page.")]
        public void Import(
            [Argument("page", "p", Description = "The full http address of the page to save the source content to.")]
            string targetLink,
            [Argument("source", "s", Description = "The full http address of the page you want to import.")]
            string sourceLink,
            [Argument("recursive", "r", DefaultValue = false, Description = "True to recursivly import all links within the same domain.")]
            bool recursive,
            [Argument("noprompt", "q", DefaultValue = false, Description = "True to stop prompt for confirmation before overwriting content.")]
            bool noPrompt
            )
        {
            Uri targetUri = new Uri(targetLink, UriKind.Absolute);
            using (TempDirectory tempFolder = new TempDirectory())
            using (ContentStorage writer = new ContentStorage(StoragePath(targetLink), false))
            {
                bool exists = writer.ContainsKey(targetUri.NormalizedPathAndQuery());
                if (exists)
                    if (!noPrompt && !new ConfirmPrompt().Continue("Overwrite " + targetUri.NormalizedPathAndQuery()))
                        return;

                Uri sourceUri = new Uri(sourceLink, UriKind.Absolute);
                using (SiteCollector index = new SiteCollector(tempFolder.TempPath, sourceLink))
                {
                    index.NoDefaultPages = true;
                    index.UpdateSearchTemplate = false;
                    if (recursive)
                        index.CrawlSite();
                    else
                    {
                        index.AddUrlsFound = false;
                        index.CrawlPage(sourceUri.NormalizedPathAndQuery());
                    }
                }

                using (SiteConverter index = new SiteConverter(tempFolder.TempPath, sourceLink))
                {
                    index.Overwrite = true;
                    index.ConvertTo(targetLink, writer);
                }

                if(exists)
                    writer.Remove(targetUri.NormalizedPathAndQuery());
                writer.Rename(sourceUri.NormalizedPathAndQuery(), targetUri.NormalizedPathAndQuery());
            }
        }
    }
}
