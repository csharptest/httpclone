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
using CSharpTest.Net.Commands;
using CSharpTest.Net.HttpClone.Publishing;
using CSharpTest.Net.HttpClone.Storage;

namespace CSharpTest.Net.HttpClone.Commands
{
    partial class CommandLine
    {
        [Command(Category = "Websites", Description = "Crawl, Optimize, Deduplicate, and Index the website provided.")]
        public void UpdateSite(
            [Argument("site", "s", Description = "The root http address of the website copy.")]
            string site,
            [Argument("source", DefaultValue = null, Description = "Overrides the source http address.")]
            string source)
        {
            CrawlSite(source ?? site);
            if (source != site)
                CopySite(source, site, true);

            Optimize(site, false);
            Deduplicate(site, false, false);
            Index(site);
            MakeSearchTemplate(site);
        }

        [Command(Category = "Websites", Description = "Crawl the website provided and store any changed content.")]
        public void CrawlSite(
            [Argument("site", "s", Description = "The root http address of the website copy.")]
            string site)
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

        [Command(Category = "Websites", Description = "Delete the specified website copy.")]
        public void DeleteSite(
            [Argument("site", "s", Description = "The root http address of the website copy.")]
            string site,
            [Argument("noprompt", "q", DefaultValue = false, Description = "True to suppress confirmation message.")]
            bool noPrompt)
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

        [Command(Category = "Websites", Description = "Copy one website to another changing all links to the original to the target.")]
        public void CopySite(
            [Argument("site", "s", Description = "The root http address of the website to copy.")]
            string original,
            [Argument("target", "t", Description = "The root http address of the destination website.")]
            string target,
            [Argument("overwrite", "y", DefaultValue = false, Description = "True to overwrite any existing content.")]
            bool overwrite)
        {
            using (SiteConverter index = new SiteConverter(StoragePath(original), original))
            using (ContentStorage writer = new ContentStorage(StoragePath(target), false))
            {
                index.Overwrite = overwrite;
                index.ConvertTo(target, writer);
            }
        }
    }
}
