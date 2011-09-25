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
using CSharpTest.Net.Commands;
using CSharpTest.Net.HttpClone.Publishing;
using CSharpTest.Net.HttpClone.Storage;

namespace CSharpTest.Net.HttpClone.Commands
{
    partial class CommandLine
    {
        public void Update(string site, [DefaultValue(null)]string source)
        {
            Crawl(source ?? site);
            if (source != site)
                CopyTo(source, site);
            Build(site);
        }

        public void Build(string site)
        {
            Index(site);
            MakeSearchTemplate(site);
            Optimize(site, false);
        }

        public void Crawl(string site)
        {
            using (SiteCollector index = new SiteCollector(StoragePath(site), site))
            {
                index.MaxCrawlAge = TimeSpan.FromMinutes(1);
                if (!index.CrawlSite())
                {
                    Console.Error.WriteLine("Not Modified.");
                    Environment.ExitCode = 1;
                }
            }
        }

        public void DeleteSite(string site, [DefaultValue(false)]bool noPrompt)
        {
            if (noPrompt || new ConfirmPrompt().Question("Are you sure you want to remove this site", "yes", "no") == "yes")
            {
                foreach (string dname in new[] { "content", "index" })
                {
                    string dir = Path.Combine(StoragePath(site), dname);
                    if (Directory.Exists(dir))
                        Directory.Delete(dir, true);
                }
                foreach (string fname in new[] { "content.index", "workqueue.txt" })
                {
                    string file = Path.Combine(StoragePath(site), fname);
                    if (File.Exists(file))
                        File.Delete(file);
                }
            }
        }

        public void CopyTo(string original, string target)
        {
            using (SiteConverter index = new SiteConverter(StoragePath(original), original))
            using (ContentStorage writer = new ContentStorage(StoragePath(target), false))
            {
                index.Overwrite = true;
                index.ConvertTo(target, writer);
            }
        }

        public void Rename(string original, string target)
        {
            using (SiteConverter index = new SiteConverter(StoragePath(original), original))
            using (ContentStorage writer = new ContentStorage(StoragePath(target), false))
            {
                index.ConvertTo(target, writer);
            }
        }
    }
}
