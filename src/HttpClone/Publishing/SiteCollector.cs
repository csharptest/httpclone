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
using CSharpTest.Net.Crypto;
using System.IO;
using CSharpTest.Net.HttpClone.Common;
using CSharpTest.Net.HttpClone.Storage;
using CSharpTest.Net.Interfaces;
using CSharpTest.Net.Threading;
using System.Net;

namespace CSharpTest.Net.HttpClone.Publishing
{
    class SiteCollector : IDisposable
    {
        private readonly ulong _instanceId;
        private readonly Uri _baseUri;
        private readonly ContentStorage _data;
        private readonly PathExclusionList _excluded;
        private readonly TextQueue _queue;
        private readonly HttpCloneConfig _config;
        private readonly ContentParser _parser;

        public SiteCollector(string directory, string uriStart)
        {
            CrawlTime = DateTime.Now;
            _instanceId = Guid.NewGuid().ToUInt64();
            _baseUri = new Uri(new Uri(uriStart, UriKind.Absolute), "/");

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            _config = Config.ReadConfig(_baseUri, directory);
            _excluded = new PathExclusionList();
            _queue = new TextQueue(Path.Combine(directory, "workqueue.txt"), true);
            _data = new ContentStorage(directory, false);
            _parser = new ContentParser(_data, _baseUri);
            _parser.VisitUri += AddUri;

            AddUrlsFound = true;
            UpdateSearchTemplate = true;
            MaxCrawlAge = TimeSpan.MaxValue;
            AddUri(new Uri(uriStart, UriKind.Absolute));
        }

        public void Dispose()
        {
            _data.Dispose();
            _queue.Dispose();
        }

        public bool Modified { get; set; }
        private DateTime CrawlTime { get; set; }
        public TimeSpan MaxCrawlAge { get; set; }
        public Uri BaseUri { get { return _baseUri; } }
        public bool UpdateSearchTemplate { get; set; }
        public bool NoDefaultPages { get; set; }
        public bool AddUrlsFound { get; set; }

        public bool CrawlSite()
        {
            if (!NoDefaultPages && UpdateSearchTemplate && _config.Searching != null && !String.IsNullOrEmpty(_config.Searching.TemplateUri))
                AddUri(new Uri(_baseUri, _config.Searching.TemplateUri));

            _excluded.ReadRobotsFile(_baseUri, "HttpClone");
            _excluded.AddRange(_config.ExcludedPaths.SafeEnumeration());
            if (!NoDefaultPages)
                AddUrls(_baseUri, _config.IncludedPaths.SafeEnumeration());

            using (WorkQueue queue = new WorkQueue(System.Diagnostics.Debugger.IsAttached ? 1 : 10))
            {
                queue.OnError += (o, e) => Console.Error.WriteLine(e.GetException().Message);

                TaskCounter httpCalls = new TaskCounter(queue.Enqueue);
                TaskCounter parsing = new TaskCounter(queue.Enqueue);

                while (true)
                {
                    if (httpCalls.Count >= 5)
                    {
                        httpCalls.WaitOne();
                    }
                    else
                    {
                        bool complete = httpCalls.Count == 0 && parsing.Count == 0;

                        string path;
                        if (_queue.TryDequeue(out path))
                        {
                            string[] etag = new string[1];
                            if (ShouldFetch(path, etag))
                                httpCalls.Run(new FetchUrl(this, path, etag[0], parsing.Run).DoWork);
                        }
                        else
                        {
                            if (complete)
                                break;

                            parsing.WaitOne();
                        }
                    }
                }

                queue.Complete(true, 1000);
            }

            //Post-crawling step(s)
            if (UpdateSearchTemplate && _config.Searching != null && !String.IsNullOrEmpty(_config.Searching.TemplateUri))
            {
                new SearchTemplateBuilder(_data, _baseUri)
                    .UpdateTemplate();
            }

            return Modified;
        }

        public void CrawlPage(string sourceLink)
        {
            new FetchUrl(this, sourceLink, String.Empty, a => a()).DoWork();
        }

        public void AddUrls(Uri relative, IEnumerable<string> links)
        {
            foreach (string link in links)
            {
                Uri location;
                if (!Uri.TryCreate(relative, link, out location))
                    continue;

                AddUri(location);
            }
        }

        private void AddUri(Uri location)
        {
            if (!_baseUri.IsSameHost(location))
                return;

            string lnkPath = location.NormalizedPathAndQuery();

            if (_excluded.IsExcluded(lnkPath))
                return;

            if (_data.ContainsKey(lnkPath))
                return;

            _data.Add(lnkPath, _data.New(lnkPath, CrawlTime).Build());
            _queue.Enqueue(lnkPath);
        }

        private bool ShouldFetch(string path, string[] etag)
        {
            etag[0] = null;
            if (_excluded.IsExcluded(path))
                return false;
            
            bool update = false;
            _data.Update(path,
                rec =>
                {
                    if ((rec.LastCrawled == DateTime.MinValue || (CrawlTime - rec.LastCrawled) >= MaxCrawlAge)
                        && rec.CrawlingInstance != _instanceId)
                    {
                        update = true;
                        etag[0] = rec.HasETag ? rec.ETag : null;
                        return rec.ToBuilder()
                            .SetCrawlingInstance(_instanceId)
                            .Build();
                    }
                    return rec;
                }
            );
            return update;
        }

        public void SetHttpError(string path, HttpStatusCode status)
        {
            Console.Error.WriteLine("{0} - {1}", (int)status, path);
            _data.Update(path,
                rec =>
                {
                    ContentRecord.Builder builder = rec.ToBuilder()
                        .SetContentUri(path)
                        .SetLastCrawled(CrawlTime)
                        .SetHttpStatus((uint)status)
                        ;
                    return builder.Build();
                }
            );
        }

        public void SetNotModified(string path)
        {
            _data.Update(path,
               rec =>
                   {
                       ContentRecord.Builder builder = rec.ToBuilder()
                           .SetLastCrawled(CrawlTime)
                           .SetLastValid(CrawlTime)
                           ;
                       return builder.Build();
                   }
               );
        }

        public void SetRedirect(string path, HttpStatusCode status, Uri redirectUri)
        {
            string targetUri = redirectUri.PathAndQuery;
            if (!_baseUri.IsSameHost(redirectUri))
                targetUri = redirectUri.AbsoluteUri;

            _data.Update(path,
                rec =>
                {
                    Modified = Modified || rec.HttpStatus != (uint) status ||
                               rec.ContentRedirect != targetUri;

                    ContentRecord.Builder builder = rec.ToBuilder()
                        .SetContentUri(path)
                        .SetLastCrawled(CrawlTime)
                        .SetLastValid(CrawlTime)
                        .SetHttpStatus((uint)status)
                        .SetContentRedirect(targetUri)
                        ;
                    return builder.Build();
                }
            );
            AddUri(redirectUri);
        }

        public void SetContents(string path, HttpStatusCode status, string contentType, string etag, DateTime? modified, byte[] contents)
        {
            if (contents.Length == 0)
            {
                Console.Error.WriteLine("{0} - {1}  (Content is empty)", (int)status, path);
            }

            ContentRecord rec = _data[path];
            ITransactable pendingUpdate = null;
            try
            {
                ContentRecord.Builder builder = rec.ToBuilder()
                    .SetContentUri(path)
                    .SetLastCrawled(CrawlTime)
                    .SetLastValid(CrawlTime)
                    .SetHttpStatus((uint)status)
                    ;

                builder.ClearContentRedirect();
                builder.SetContentType(contentType);
                builder.SetContentLength((uint) contents.Length);
                if(!String.IsNullOrEmpty(etag))
                    builder.SetETag(etag);

                string hash = Hash.SHA256(contents).ToString();
                if (hash != builder.HashOriginal)
                {
                    Modified = true;
                    builder.SetHashOriginal(hash);
                    builder.SetDateModified(CrawlTime);
                    pendingUpdate = _data.WriteContent(builder, contents);
                }

                if (_data.AddOrUpdate(path, rec = builder.Build()))
                {
                    if (pendingUpdate != null)
                    {
                        pendingUpdate.Commit();
                        pendingUpdate.Dispose();
                        pendingUpdate = null;
                    }
                }
            }
            finally
            {
                if (pendingUpdate != null)
                {
                    pendingUpdate.Rollback();
                    pendingUpdate.Dispose();
                }
            }

            ProcessFileContent(rec, contents);
        }

        private void ProcessFileContent(ContentRecord record, byte[] contentBytes)
        {
            if(AddUrlsFound)
                _parser.ProcessFile(record, contentBytes);
        }
    }
}
