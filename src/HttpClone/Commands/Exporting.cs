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
using System.ComponentModel;
using CSharpTest.Net.Commands;
using CSharpTest.Net.HttpClone.Publishing;

namespace CSharpTest.Net.HttpClone.Commands
{
    partial class CommandLine
    {
        public void Archive(string site)
        {
            using (SitePublisher index = new SitePublisher(StoragePath(site), site))
            {
                index.CreateArchive();
            }
        }

        public void Export(string site, string directory, [DefaultValue(true)] bool rebase)
        {
            if (String.IsNullOrEmpty(directory) || File.Exists(directory))
                throw new ArgumentException("Please specify a valid directory.");

            if (!Directory.Exists(directory))
            {
                if (!new ConfirmPrompt().Continue("Directory does not exist, create the directory"))
                    return;
                Directory.CreateDirectory(directory);
            }

            using (SiteConverter converter = new SiteConverter(StoragePath(site), site))
            {
                converter.RebaseLinks = rebase;
                converter.Export(directory);
            }
        }
    }
}
