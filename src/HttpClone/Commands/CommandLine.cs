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
using System.IO;
using System.Linq;
using CSharpTest.Net.Commands;
using System.ComponentModel;

namespace CSharpTest.Net.HttpClone.Commands
{
    /// <summary>
    /// This class implements the command-line interface.  
    /// </summary>
    /// <remarks>
    /// The actions are grouped and orgainized in the source files located in this directory.
    /// </remarks>
    public partial class CommandLine : IDisposable
    {
        [Command(Visible = false)]
        public void HtmlHelp([DefaultValue(null)] string name, ICommandInterpreter ci)
        {
            string html = ((CommandInterpreter)ci).GetHtmlHelp(name);
            string path = Path.Combine(Path.GetTempPath(), "HttpClone.Help.html");
            File.WriteAllText(path, html);
            System.Diagnostics.Process.Start(path);
        }

        [Command("Help", "-?", "/?", "?", Category = "Built-in", Description = "Gets the help for a specific command or lists available commands.")]
        public void Help(
            [Argument("name", "command", "c", "option", "o", Description = "The name of the command or option to show help for.", DefaultValue = null)] 
			string name,
            ICommandInterpreter ci
            )
        {
            Dictionary<string, ICommand> cmds = new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase);
            foreach (ICommand c in ci.Commands)
                foreach (string nm in c.AllNames)
                    cmds[nm] = c;

            ICommand cmd;
            if (name != null && cmds.TryGetValue(name, out cmd))
            {
                cmd.Help();
            }
            else
            {
                ILookup<string, ICommand> categories = ci.Commands.ToLookup(c => c.Category ?? "Unk");
                foreach (IGrouping<string, ICommand> group in categories.OrderBy(g => g.Key))
                {
                    Console.WriteLine("{0}:", group.Key);
                    foreach (ICommand item in group)
                    {
                        Console.WriteLine("{0,12}: {1}", item.DisplayName, item.Description);
                    }
                    Console.WriteLine();
                }
            }
        }

        string StoragePath(string site)
        {
            Uri url = new Uri(site, UriKind.Absolute);
            if(url.IsFile || url.IsUnc)
            {
                Check.Assert<DirectoryNotFoundException>(Directory.Exists(url.LocalPath));
                return url.LocalPath;
            }

            string store = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Settings.StorePath);
            store = Path.Combine(store, url.Host);
            if (!url.IsDefaultPort)
                store += "." + url.Port;
            return store;
        }

        void IDisposable.Dispose()
        { }
    }
}
