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
using CSharpTest.Net.HttpClone.Publishing;

namespace CSharpTest.Net.HttpClone.Commands
{
    partial class CommandLine
    {
        [Command(Category = "Optimizations", Description = "Apply the optimization rules associated with each document type configured.")]
        public void Optimize(
            [Argument("site", "s", Description = "The root http address of the website copy.")]
            string site,
            [Argument("condense", Visible = false, DefaultValue = false, Description = "TODO: Rewrite the html collapsing whitespace.")]
            bool condense)
        {
            using (ContentOptimizier optimizier = new ContentOptimizier(StoragePath(site), site))
            {
                //BROKEN: optimizier.CondenseHtml = condense;
                optimizier.OptimizeAll();
            }
        }

        [Command(Category = "Optimizations", Description = "Apply the optimization rules to a single page.")]
        public void OptimizePage(
            [Argument("page", "p", Description = "The full http address of the page you want to run optimizations on.")]
            string site,
            [Argument("condense", Visible = false, DefaultValue = false, Description = "TODO: Rewrite the html collapsing whitespace.")]
            bool condense)
        {
            Uri path = new Uri(site, UriKind.Absolute);
            using (ContentOptimizier optimizier = new ContentOptimizier(StoragePath(site), site))
            {
                //BROKEN: optimizier.CondenseHtml = condense;
                optimizier.OptimizePage(path.NormalizedPathAndQuery());
            }
        }
    }
}
