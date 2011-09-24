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
using CSharpTest.Net.Commands;
using CSharpTest.Net.HttpClone.Common;
using CSharpTest.Net.HttpClone.Publishing;
using CSharpTest.Net.HttpClone.Storage;

namespace CSharpTest.Net.HttpClone.Commands
{
    partial class CommandLine
    {
        public void Index(string site)
        {
            using (ContentIndexing indexer = new ContentIndexing(StoragePath(site), site))
            {
                indexer.BuildIndex();
            }
        }

        public void MakeSearchTemplate(string site)
        {
            using(ContentStorage store = new ContentStorage(StoragePath(site), false))
            {
                SearchTemplateBuilder builder = new SearchTemplateBuilder(store, new Uri(site, UriKind.Absolute));
                builder.RebuildTemplate();
            }
        }

        public void Like(string url)
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

        public void Search(string site, string term, [DefaultValue(false)] bool newest)
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
        
        public void HtmlSearch(string site, string term)
        {
            using (ContentStorage store = new ContentStorage(StoragePath(site), true))
            {
                SearchTemplate template = new SearchTemplate(store);
                string tmp = template.RenderResults(term, 1);
                Console.WriteLine(tmp);
            }
        }
    }
}
