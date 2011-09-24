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
using System.ComponentModel;
using System.IO;
using System.Net;
using CSharpTest.Net.Commands;
using CSharpTest.Net.Crypto;
using CSharpTest.Net.HttpClone.Common;
using CSharpTest.Net.HttpClone.Publishing;
using CSharpTest.Net.HttpClone.Storage;
using CSharpTest.Net.Interfaces;
using CSharpTest.Net.IO;

namespace CSharpTest.Net.HttpClone.Commands
{
    partial class CommandLine
    {
        public void Remove(string url)
        {
            using (ContentStorage store = new ContentStorage(StoragePath(url), false))
                store.Remove(new Uri(url, UriKind.Absolute).NormalizedPathAndQuery());
        }

        public void AddRecursive(string targetLink, string sourceLink)
        {
            Uri targetUri = new Uri(targetLink, UriKind.Absolute);
            using (TempDirectory tempFolder = new TempDirectory())
            using (ContentStorage writer = new ContentStorage(StoragePath(targetLink), false))
            {
                bool exists = writer.ContainsKey(targetUri.NormalizedPathAndQuery());
                if (exists)
                    if (!new ConfirmPrompt().Continue("Overwrite " + targetUri.NormalizedPathAndQuery()))
                        return;

                Uri sourceUri = new Uri(sourceLink, UriKind.Absolute);
                using (SiteCollector index = new SiteCollector(tempFolder.TempPath, sourceLink))
                {
                    index.NoDefaultPages = true;
                    index.UpdateSearchTemplate = false;
                    index.CrawlSite();
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

        public void Add(string targetLink, string sourceLink, [DefaultValue(true)]bool fixRelativeLinks)
        {
            AddFrom(targetLink, sourceLink, parser =>
                //All imported links must be absolute
                parser.RewriteUri += uri => new Uri(uri.OriginalString, UriKind.Absolute)
            );
        }

        private void AddFrom(string targetLink, string sourceLink, Action<ContentParser> rewrite)
        {
            DateTime timestamp = DateTime.Now;
            Uri targetUri = new Uri(targetLink, UriKind.Absolute);
            using (ContentStorage storage = new ContentStorage(StoragePath(targetLink), false))
            {
                string relpath = targetUri.NormalizedPathAndQuery();
                    
                ContentRecord rec;
                if (!storage.TryGetValue(relpath, out rec))
                {
                    rec = storage.New(relpath, timestamp).BuildPartial();
                }
                else if(!new ConfirmPrompt().Continue("Overwrite the existing uri"))
                    return;

                Uri location;
                if (!Uri.TryCreate(sourceLink, UriKind.Absolute, out location))
                {
                    if (File.Exists(Path.Combine(Environment.CurrentDirectory, sourceLink)))
                    {
                        sourceLink = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, sourceLink));
                        location = new Uri(sourceLink, UriKind.Absolute);
                    }
                    else throw new ApplicationException("Unable to identify source link: " + sourceLink);
                }
                string contentType;
                byte[] contents;
                ContentRecord.Builder builder = rec.ToBuilder();

                if (location.IsFile || location.IsUnc)
                {
                    contentType = new MimeInfoMap(targetUri, StoragePath(targetLink))
                        .FromExtension(Path.GetExtension(location.OriginalString));
                    contents = File.ReadAllBytes(location.OriginalString);
                    builder.SetHttpStatus(200);
                    builder.ClearETag();
                }
                else
                {
                    HttpRequestUtil http = new HttpRequestUtil(location);
                    if (http.Get(location.PathAndQuery) != HttpStatusCode.OK)
                        throw new ApplicationException(String.Format("The source link returned {0}/{1}.", (int)http.StatusCode, http.StatusCode));
                    contentType = http.ContentType;
                    contents = http.Content;
                    builder.SetHttpStatus((uint)http.StatusCode);
                    if (!String.IsNullOrEmpty(http.ETag))
                        builder.SetETag(http.ETag);
                }

                string hashString = Hash.SHA256(contents).ToString();
                if(builder.HashOriginal != hashString)
                    builder.SetDateModified(timestamp);
                builder
                    .SetLastCrawled(timestamp)
                    .SetLastValid(timestamp)
                    .ClearContentRedirect()
                    .SetContentType(contentType)
                    .SetContentLength((uint)contents.Length)
                    .SetHashOriginal(hashString);

                if (rewrite != null)
                {
                    Uri srcUri = new Uri(sourceLink, UriKind.Absolute);
                    Uri srcBase = new Uri(srcUri, "./");
                    rec = builder.Clone().SetContentUri(srcBase.MakeRelativeUri(srcUri).OriginalString).Build();

                    ContentParser parser = new ContentParser(storage, targetUri, srcBase);
                    rewrite(parser);
                    contents = parser.ProcessFile(rec, contents);
                }

                using (ITransactable trans = storage.WriteContent(builder, contents))
                {
                    if (storage.AddOrUpdate(builder.ContentUri, builder.Build()))
                        trans.Commit();
                }
            }
        }

        public void Internalize(string targetLink, string sourceLink)
        {
            AddRecursive(targetLink, sourceLink);
            Relink(targetLink, sourceLink, targetLink);
        }
    }
}
