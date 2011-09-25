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
using System.Web;
using CSharpTest.Net.Commands;
using CSharpTest.Net.Html;

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
        [Command(Visible = false, Description = "Gets the help for a specific command or lists available commands.")]
        public void HtmlHelp(ICommandInterpreter _ci)
        {
            CommandInterpreter ci = ((CommandInterpreter)_ci);
            HtmlLightDocument doc = new HtmlLightDocument(ci.GetHtmlHelp("help"));
            XmlLightElement e = doc.SelectRequiredNode("/html/body/h1[2]");
            XmlLightElement body = e.Parent;
            int i = body.Children.IndexOf(e);
            body.Children.RemoveRange(i, body.Children.Count - i);

            StringWriter sw = new StringWriter();
            // Command index
            sw.WriteLine("<html><body>");
            sw.WriteLine("<h1>All Commands:</h1>");
            sw.WriteLine("<blockquote><ul>");
            ILookup<string, ICommand> categories = ci.Commands.Where(c => c.Visible).ToLookup(c => c.Category ?? "Unk");
            foreach (IGrouping<string, ICommand> group in categories.OrderBy(g => g.Key))
            {
                sw.WriteLine("<li><a href=\"#{0}\">{0}</a></li>", group.Key);
                sw.WriteLine("<ul>");
                foreach (ICommand cmd in group)
                    sw.WriteLine("<li><a href=\"#{0}\">{0}</a> - {1}</li>", cmd.DisplayName, HttpUtility.HtmlEncode(cmd.Description));
                sw.WriteLine("</ul>");
            }
            sw.WriteLine("</ul></blockquote>");

            // Command Help
            foreach (IGrouping<string, ICommand> group in categories.OrderBy(g => g.Key))
            {
                sw.WriteLine("<h2><a name=\"{0}\"></a>{0} Commands:</h2>", group.Key);
                sw.WriteLine("<blockquote>");
                foreach (ICommand cmd in group)
                {
                    e = new HtmlLightDocument(ci.GetHtmlHelp(cmd.DisplayName)).SelectRequiredNode("/html/body/h3");
                    sw.WriteLine("<a name=\"{0}\"></a>", cmd.DisplayName);
                    sw.WriteLine(e.InnerXml);
                    sw.WriteLine(e.NextSibling.NextSibling.InnerXml);
                }
                sw.WriteLine("</blockquote>");
            }

            e = new HtmlLightDocument(sw.ToString()).SelectRequiredNode("/html/body");
            body.Children.AddRange(e.Children);

            string html = body.Parent.InnerXml;
            string path = Path.Combine(Path.GetTempPath(), "HttpClone.Help.html");
            File.WriteAllText(path, html);
            System.Diagnostics.Process.Start(path);
        }

        [Command("Help", "-?", "/?", "?", Visible = false, Description = "Gets the help for a specific command or lists available commands.")]
        public void Help(
            [Argument("name", "command", "c", "option", "o", Description = "The name of the command or option to show help for.", DefaultValue = null)] 
			string name,
            ICommandInterpreter ci
            )
        {
            ICommand cmd;
            if (name != null)
            {
                Dictionary<string, ICommand> cmds = new Dictionary<string, ICommand>(StringComparer.OrdinalIgnoreCase);
                foreach (ICommand c in ci.Commands)
                    foreach (string nm in c.AllNames)
                        cmds[nm] = c;

                if (cmds.TryGetValue(name, out cmd))
                {
                    cmd.Help();
                    return;
                }
                Console.WriteLine("Unknown command: {0}", name);
                Console.WriteLine();
                Environment.ExitCode = 1;
            }

            int padding = 4 + ci.Commands.Max(c => c.DisplayName.Length);
            string format = "{0," + padding + "}: {1}";

            ILookup<string, ICommand> categories = ci.Commands.Where(c=>c.Visible).ToLookup(c => c.Category ?? "Unk");
            foreach (IGrouping<string, ICommand> group in categories.OrderBy(g => g.Key))
            {
                Console.WriteLine("{0}:", group.Key);
                foreach (ICommand item in group)
                {
                    Console.WriteLine(format, item.DisplayName, item.Description);
                }
                Console.WriteLine();
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
