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
using CSharpTest.Net.Commands;
using CSharpTest.Net.HttpClone.Common;
using CSharpTest.Net.HttpClone.Publishing;
using CSharpTest.Net.HttpClone.Storage;

namespace CSharpTest.Net.HttpClone.Commands
{
    partial class CommandLine
    {
        [Command(Category = "Searching", Description = "Creates a clean copy of the search index for the site.")]
        public void Index(
            [Argument("site", "s", Description = "The root http address of the website copy.")]
            string site)
        {
            using (ContentIndexing indexer = new ContentIndexing(StoragePath(site), site))
            {
                indexer.BuildIndex();
            }
        }

        [Command("NewTemplate", "MakeSearchTemplate", Category = "Searching", Description = "Recreates the search template from the original content.")]
        public void MakeSearchTemplate(
            [Argument("site", "s", Description = "The root http address of the website copy.")]
            string site)
        {
            using(ContentStorage store = new ContentStorage(StoragePath(site), false))
            {
                SearchTemplateBuilder builder = new SearchTemplateBuilder(store, new Uri(site, UriKind.Absolute));
                builder.RebuildTemplate();
            }
        }

        [Command(Category = "Searching", Description = "Displays content similar to the page provided.")]
        public void Like(
            [Argument("page", "p", Description = "The http address of the web page.")]
            string url)
        {
            using (ContentStorage store = new ContentStorage(StoragePath(url), true))
            {
                Uri uri = new Uri(url, UriKind.Absolute);
                foreach (var result in store.Similar(uri.NormalizedPathAndQuery(), 10, false))
                {
                    Console.WriteLine(" {0,3}%  {1,-25}  {2:yyyy-MM-dd}  \"{3}\"", result.Ranking, result.Uri, result.Modified, result.Title);
                }
            }
        }

        [Command(Category = "Searching", Description = "Searches the site and prints the matching pages.")]
        public void Search(
            [Argument("site", "s", Description = "The root http address of the website copy.")]
            string site,
            [Argument("term", "t", Description = "The expression to search for, see http://lucene.apache.org/java/2_4_0/queryparsersyntax.html.")]
            string term,
            [Argument("newest", "n", DefaultValue = false, Description = "Order the results by date rather than by best match.")]
            bool newest)
        {
            int limit = 50;
            using (ContentStorage store = new ContentStorage(StoragePath(site), true))
            {
                int total, count = 0;
                foreach (var result in store.Search(term, 0, limit, newest, out total))
                {
                    Console.WriteLine(" {0,3}%  {1,-25}  {2:yyyy-MM-dd}  \"{3}\"", result.Ranking, result.Uri, result.Modified, result.Title);
                    count++;
                }

                if (count == total)
                    Console.WriteLine("[{0} total]", count);
                else
                    Console.WriteLine("[{0} of {1} total]", count, total);
            }
        }

        [Command(Category = "Searching", Description = "Searches the site and prints the rendered html result.")]
        public void HtmlSearch(
            [Argument("site", "s", Description = "The root http address of the website copy.")]
            string site,
            [Argument("term", "t", Description = "The expression to search for, see http://lucene.apache.org/java/2_4_0/queryparsersyntax.html.")]
            string term,
            [Argument("page", "p", DefaultValue = 1, Description = "The result page to return.")]
            int page)
        {
            using (ContentStorage store = new ContentStorage(StoragePath(site), true))
            {
                SearchTemplate template = new SearchTemplate(store);
                string tmp = template.RenderResults(term, page);
                Console.WriteLine(tmp);
            }
        }
    }
}
