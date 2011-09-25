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
        [Command(Category = "Publishing", Description = "Publishes the a new snapshot of the website.")]
        public void Publish(
            [Argument("site", "s", Description = "The root http address of the website copy.")]
            string site)
        {
            using (SitePublisher index = new SitePublisher(StoragePath(site), site))
            {
                GetClientPassword(index);
                index.Publish();
            }
        }

        [Command(Category = "Publishing", Description = "Publishes the a new snapshot of the website to the specified host.")]
        public void PublishTo(
            [Argument("site", "s", Description = "The root http address of the website copy.")]
            string siteToPublish,
            [Argument("host", "h", Description = "The root http address of the target website host.")]
            string destinationUrl)
        {
            using (SitePublisher index = new SitePublisher(StoragePath(siteToPublish), siteToPublish))
            {
                GetClientPassword(index);
                index.Publish(new Uri(destinationUrl, UriKind.Absolute));
            }
        }

        [Command(Category = "Publishing", Description = "Runs a small HTTP host to server the content of the site on the port provided.")]
        public void Host(
            [Argument("site", "s", Description = "The root http address of the website copy.")]
            string url,
            [Argument("port", "p", DefaultValue = 11080, Description = "The tcp/ip port to use for hosting the content.")]
            int port)
        {
            Uri siteUri = new Uri(url, UriKind.Absolute);
            using (ContentStorage store = new ContentStorage(StoragePath(url), true))
            using (WcfHttpHost host = new WcfHttpHost(store, port))
            {
                Process.Start(new Uri(host.Uri, siteUri.PathAndQuery).AbsoluteUri);
                Console.Write("Press [Enter] to exit.");
                Console.ReadLine();
            }
        }

        private void GetClientPassword(SitePublisher publisher)
        {
            if(!publisher.HasClientPassword)
                GetPassword("Enter client password: ", publisher.SetClientPassword);
        }

        [Command(Category = "Publishing", Visible = false, Description = "Runs a repeated get request for the provided url and times the results.")]
        public void Benchmark(
            [Argument("page", "p", Description = "The http address of the web page to fetch.")]
            string url, 
            [Argument(DefaultValue = 60, Description = "The duration of a single run of the test")] 
            int duration,
            [Argument(DefaultValue = 1, Description = "The number of times to repeat the duration of the test")] 
            int repeated,
            [Argument(DefaultValue = 0, Description = "The number of threads to run")] 
            int threads,
            [Argument(DefaultValue = false, Description = "Cancel the test on the first http error.")] 
            bool stopOnError)
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
