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
using System.Text;
using System.Diagnostics;
using System.Threading;
using CSharpTest.Net.Commands;
using CSharpTest.Net.IO;
using CSharpTest.Net.Crypto;
using CSharpTest.Net.Utils;
using CSharpTest.Net.HttpClone.Publishing;
using CSharpTest.Net.HttpClone.Storage;

namespace CSharpTest.Net.HttpClone.Commands
{
    partial class CommandLine
    {
        [Command(Category = "Viewing", Description = "Print the details about the content to the console.")]
        public void Dump(
            [Argument("page", "p", Description = "The http address of the web page.")]
            string url)
        {
            With(url, (s, r) => Console.WriteLine(r.ToString()));
        }

        [Command(Category = "Viewing", Description = "Print the content url to the console.")]
        public void Type(
            [Argument("page", "p", Description = "The http address of the web page.")]
            string url)
        {
            With(url, 
                (s, r) =>
                    {
                        if (r.HasContentStoreId)
                            Console.WriteLine(Encoding.UTF8.GetString(s.ReadContent(r, true)));
                        else
                            throw new ApplicationException("The path has no content.");
                    });
        }

        [Command(Category = "Viewing", Description = "Edit the content url with the default registered program.")]
        public void Edit(
            [Argument("page", "p", Description = "The http address of the web page.")]
            string url)
        {
            OpenWith(null, "edit", true, true, url);
        }

        [Command(Category = "Viewing", Description = "Open the content url with notepad.exe.")]
        public void Notepad(
            [Argument("page", "p", Description = "The http address of the web page.")]
            string url)
        {
            OpenWith("notepad.exe", null, true, true, url);
        }

        [Command(Category = "Viewing", Description = "Open the content url with the default registered program.")]
        public void Open(
            [Argument("page", "p", Description = "The http address of the web page.")]
            string url)
        {
            OpenWith(null, "open", false, false, url);
        }

        private void With(string url, Action<ContentStorage, ContentRecord> action)
        {
            With(true, url, action);
        }

        private void With(bool readOnly, string url, Action<ContentStorage, ContentRecord> action)
        {
            using (ContentStorage storage = new ContentStorage(StoragePath(url), readOnly))
            {
                string relpath = new Uri(url, UriKind.Absolute).NormalizedPathAndQuery();
                ContentRecord rec;
                if (storage.TryGetValue(relpath, out rec))
                {
                    action(storage, rec);
                }
                else
                    throw new ApplicationException("Path not found: " + relpath);
            }
        }

        private void OpenWith(string exe, string verb, bool wait, bool editable, string url)
        {
            MimeInfoMap mimeTypes = new MimeInfoMap(new Uri(url, UriKind.Absolute), StoragePath(url));
            With(!editable || !wait, url,
                (s, r) =>
                {
                    if (r.HasContentStoreId)
                    {
                        string extension = mimeTypes.GetFileExtension(r.MimeType, r.ContentUri);
                        using (TempFile tmp = TempFile.FromExtension(extension ?? ".txt"))
                        {
                            byte[] bytes = s.ReadContent(r, true);
                            Hash hash = Hash.SHA256(bytes);
                            tmp.WriteAllBytes(bytes);
                            if(!editable)
                                s.Dispose();//close storage

                            if(exe != null) exe = FileUtils.FindFullPath(exe);
                            ProcessStartInfo psi = new ProcessStartInfo(exe ?? tmp.TempPath);
                            psi.UseShellExecute = exe == null && verb != null;
                            if (verb != null) psi.Verb = verb;
                            if (exe != null) psi.Arguments = tmp.TempPath;

                            Process p = Process.Start(psi);
                            if (wait)
                            {
                                Thread.Sleep(1000);
                                p.WaitForExit();

                                if (editable)
                                {
                                    Stream locked = null;
                                    while (locked == null)
                                    {
                                        try { locked = new FileStream(tmp.TempPath, FileMode.Open, FileAccess.Read, FileShare.None); }
                                        catch (FileNotFoundException) { throw; }
                                        catch (IOException) { Thread.Sleep(1000); }
                                    }
                                    using (locked)
                                    {
                                        bytes = IOStream.ReadAllBytes(locked);
                                        if (hash != Hash.SHA256(bytes))
                                        {
                                            s.WriteContent(r, bytes);
                                        }
                                    }
                                }
                            }
                            else
                                tmp.Detatch();
                        }
                    }
                    else
                        throw new ApplicationException("The path has no content.");
                });
        }
    }
}
