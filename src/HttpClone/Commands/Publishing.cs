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
using System.Diagnostics;
using System.Threading;
using System.ComponentModel;
using CSharpTest.Net.Commands;
using CSharpTest.Net.Threading;
using CSharpTest.Net.HttpClone.Common;
using CSharpTest.Net.HttpClone.Publishing;
using CSharpTest.Net.HttpClone.Storage;
using CSharpTest.Net.HttpClone.Services;

namespace CSharpTest.Net.HttpClone.Commands
{
    partial class CommandLine
    {
        public void Publish(string site)
        {
            using (SitePublisher index = new SitePublisher(StoragePath(site), site))
            {
                index.Publish();
            }
        }

        public void PublishTo(string siteToPublish, string destinationUrl)
        {
            using (SitePublisher index = new SitePublisher(StoragePath(siteToPublish), siteToPublish))
            {
                index.Publish(new Uri(destinationUrl, UriKind.Absolute));
            }
        }

        public void Host(string url, [DefaultValue(11080)] int port)
        {
            using (ContentStorage store = new ContentStorage(StoragePath(url), true))
            using (WcfHttpHost host = new WcfHttpHost(store, port))
            {
                Process.Start(host.Uri.AbsoluteUri);
                Console.Write("Press [Enter] to exit.");
                Console.ReadLine();
            }
        }

        public void Perf(string url, [DefaultValue(60)] int duration, [DefaultValue(1)] int repeated, [DefaultValue(0)] int threads, [DefaultValue(false)] bool stopOnError)
        {
            Uri uri = new Uri(url, UriKind.Absolute);
            Uri baseUri = new Uri(uri, "/");
            string uriPath = uri.PathAndQuery;

            if(threads <= 0) threads = Environment.ProcessorCount;
            repeated = Math.Max(1, repeated);
            duration = Math.Max(1, duration);

            int[] errors = {0};
            int[] total = {0};
            ManualResetEvent start = new ManualResetEvent(false);
            ManualResetEvent stop = new ManualResetEvent(false);
            Action<string> proc = x =>
                {
                    start.WaitOne();
                    while (!stop.WaitOne(0))
                    {
                        HttpRequestUtil http = new HttpRequestUtil(baseUri);
                        if (http.Get(x) != System.Net.HttpStatusCode.OK)
                        {
                            Interlocked.Increment(ref errors[0]);
                            if (stopOnError)
                                throw new ApplicationException("Server returned " + http.StatusCode);
                        }
                        else
                            Interlocked.Increment(ref total[0]);
                    }
                };

            for (int run = 0; run <= repeated; run++)
            {
                errors[0] = total[0] = 0;
                start.Reset();
                stop.Reset();

                Stopwatch timer = new Stopwatch();
                using (WorkQueue<string> worker = new WorkQueue<string>(proc, threads))
                {
                    worker.OnError += (o, e) => stop.Set();
                    for (int i = 0; i < threads; i++) worker.Enqueue(uriPath);

                    Thread.Sleep(1000);
                    start.Set();
                    timer.Start();
                    Thread.Sleep(1000 * (run == 0 ? 1 : duration));
                    stop.Set();

                    worker.Complete(true, 10000);
                }
                timer.Stop();

                if(run > 0)
                    Console.WriteLine("Run {0,3}: {1,6} requests, {2,6} failures, in {3,6:n0}ms \t {4,6:n0} req/sec",
                        run, total[0], errors[0], timer.ElapsedMilliseconds, total[0] / timer.Elapsed.TotalSeconds);
            }
        }
    }
}
