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
using System.Text;
using CSharpTest.Net.HttpClone.Commands;
using CSharpTest.Net.HttpClone.Publishing;
using CSharpTest.Net.HttpClone.Services;
using CSharpTest.Net.HttpClone.Storage;
using CSharpTest.Net.IO;
using Ionic.Zip;
using NUnit.Framework;

namespace CSharpTest.Net.HttpClone.Test
{
    [TestFixture]
    [Category("TestCommands")]
    public partial class TestCommands
    {
        #region Test SetUp/TearDown
        readonly TempDirectory LogDir = new TempDirectory();
        [SetUp]
        public virtual void Setup()
        {
            Settings.StorePath = TempDirectory.TempPath;
            Settings.LogPath = LogDir.TempPath;
            Settings.LogLevel = Logging.LogLevels.None;

            foreach (var item in TempDirectory.Info.GetFileSystemInfos())
            {
                if (item is DirectoryInfo)
                    ((DirectoryInfo) item).Delete(true);
                else
                    item.Delete();
            }
        }
        [TestFixtureTearDown]
        public virtual void TearDown()
        {
            TempDirectory.Dispose();
        }
        #endregion
        #region Test Helpers
        static readonly Uri W3ExampleUrl = new Uri("http://w3example.wordpress.com", UriKind.Absolute);
        string W3ExampleDirectory { get { return Path.Combine(TempDirectory.TempPath, W3ExampleUrl.Authority.Replace(':', '.')); } }
        readonly TempDirectory TempDirectory = new TempDirectory();
        static readonly string[] KnownLinks = new[] 
                { 
                    "/",
                    "/?p=14",
                    "/?s=%00",
                    "/2011/09/",
                    "/2011/09/23/hello-world-2-0/",
                    "/2011/09/23/hello-world-2-0/?like=1",
                    "/about",
                    "/about-me/",
                    "/about-me/feed/",
                    "/category/blog/",
                    "/category/blog/feed/",
                    "/feed/",
                    "/search.css",
                    "/search/",
                    "/w3example/",
                    "/w3example/feed/"
                };

        private void SetupW3Example()
        {
            Stream input = GetType().Assembly.GetManifestResourceStream(GetType().Namespace + ".w3example.zip");
            using(ZipInputStream z = new ZipInputStream(input))
            {
                ZipEntry e;
                while (null != (e = z.GetNextEntry()))
                {
                    if (e.IsDirectory) continue;
                    string output = Path.Combine(W3ExampleDirectory, e.FileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    File.WriteAllBytes(output, IOStream.Read(z, (int)e.UncompressedSize));
                }
            }
        }
        
        private void VerifyLinks(string directory, IEnumerable<string> knownLinks)
        {
            using (ContentStorage store = new ContentStorage(directory, true))
            {
                foreach (string url in knownLinks)
                    Assert.IsTrue(store.ContainsKey(url), "missing link " + url);
                Assert.AreEqual(knownLinks.Count(), store.Count, "incorrect total links");
            }
        }
        
        private int CountLinks(string directory, Predicate<Uri> test)
        {
            using (ContentStorage store = new ContentStorage(directory, true))
            {
                int counter = 0;
                ContentParser parser = new ContentParser(store, W3ExampleUrl);
                parser.VisitUri += u => { if (test(u)) counter++; };
                parser.ProcessAll();
                return counter;
            }
        }
        #endregion

        [Test]
        public void TestCrawlSite()
        {
            Uri siteUri = new Uri("http://127.0.0.1:11080", UriKind.Absolute);
            string path = Path.Combine(TempDirectory.TempPath, siteUri.Authority.Replace(':', '.'));
            SetupW3Example();
            new CommandLine().RelinkEx(W3ExampleUrl.AbsoluteUri, @"^http://w3example\.wordpress\.com(.*)$", "http://127.0.0.1:11080{1}");

            using (ContentStorage store = new ContentStorage(W3ExampleDirectory, true))
            using (new WcfHttpHost(store, siteUri.Port))
            {
                Assert.IsFalse(Directory.Exists(path));
                new CommandLine().CrawlSite(siteUri.AbsoluteUri);
                Assert.IsTrue(Directory.Exists(path));
                VerifyLinks(path, KnownLinks);
            }
        }

        [Test]
        public void TestExampleSite()
        {
            SetupW3Example();
            using (ContentStorage store = new ContentStorage(W3ExampleDirectory, true))
            {
                foreach (string url in KnownLinks)
                    Assert.IsTrue(store.ContainsKey(url));
            }
        }

        [Test]
        public void TestDeleteSite()
        {
            SetupW3Example();
            VerifyLinks(W3ExampleDirectory, KnownLinks);
            new CommandLine().DeleteSite(W3ExampleUrl.AbsoluteUri, true);
            Assert.IsTrue(!Directory.Exists(W3ExampleDirectory) || new DirectoryInfo(W3ExampleDirectory).GetFileSystemInfos().Length == 0);
        }

        [Test]
        public void TestCopySite()
        {
            Uri testUri = new Uri("http://w3example.localhost.test", UriKind.Absolute);

            SetupW3Example();
            VerifyLinks(W3ExampleDirectory, KnownLinks);
            new CommandLine().CopySite(W3ExampleUrl.AbsoluteUri, testUri.AbsoluteUri, false);
            string path = Path.Combine(TempDirectory.TempPath, testUri.Authority.Replace(':', '.'));
            VerifyLinks(path, KnownLinks);
            
            Assert.AreEqual(0, CountLinks(path, u=>u.IsSameHost(W3ExampleUrl)));
        }

        [Test]
        public void TestDeduplicate()
        {
            SetupW3Example();
            VerifyLinks(W3ExampleDirectory, KnownLinks);
            // the html wordpress generates is full of user-tracking junk, need to strip it out
            new CommandLine().Optimize(W3ExampleUrl.AbsoluteUri, false);
            // then one last piece to strip is to clean up the .css user tracking links
            new CommandLine().RelinkEx(W3ExampleUrl.AbsoluteUri, @"^(.*?\.css)\?.*$", "{1}");

            string a, b;
            using (ContentStorage store = new ContentStorage(W3ExampleDirectory, true))
            {
                a = Encoding.UTF8.GetString(store.ReadContent(store["/2011/09/23/hello-world-2-0/"], true));
                b = Encoding.UTF8.GetString(store.ReadContent(store["/2011/09/23/hello-world-2-0/?like=1"], true));
            }
            // make sure we now have a duplicate
            Assert.AreEqual(a, b);
            // Remove the duplicate
            new CommandLine().Deduplicate(W3ExampleUrl.AbsoluteUri, true, true);
            VerifyLinks(W3ExampleDirectory, KnownLinks.Where(u => u != "/2011/09/23/hello-world-2-0/?like=1"));
            Assert.AreEqual(0, CountLinks(W3ExampleDirectory, u => u.PathAndQuery == "/2011/09/23/hello-world-2-0/?like=1"));
        }

        [Test]
        public void TestImportOne()
        {
            SetupW3Example();
            new CommandLine().RelinkEx(W3ExampleUrl.AbsoluteUri, @"^http://w3example\.wordpress\.com(.*)$", "http://127.0.0.1:11080{1}");

            using (ContentStorage store = new ContentStorage(W3ExampleDirectory, true))
            using (new WcfHttpHost(store, 11080))
            {
                Uri tempUri = new Uri("http://w3example.localhost.test", UriKind.Absolute);
                string path = Path.Combine(TempDirectory.TempPath, tempUri.Authority.Replace(':', '.'));
                Assert.IsFalse(Directory.Exists(path));
                new CommandLine().Import(new Uri(tempUri, "/copy-of-root/").AbsoluteUri, "http://127.0.0.1:11080", false, true);
                Assert.IsTrue(Directory.Exists(path));
                VerifyLinks(path, new[] {"/copy-of-root/"});
            }
        }

        [Test]
        public void TestImportAll()
        {
            SetupW3Example();
            new CommandLine().RelinkEx(W3ExampleUrl.AbsoluteUri, @"^http://w3example\.wordpress\.com(.*)$", "http://127.0.0.1:11080{1}");

            using (ContentStorage store = new ContentStorage(W3ExampleDirectory, true))
            using (new WcfHttpHost(store, 11080))
            {
                Uri tempUri = new Uri("http://w3example.localhost.test", UriKind.Absolute);
                string path = Path.Combine(TempDirectory.TempPath, tempUri.Authority.Replace(':', '.'));
                Assert.IsFalse(Directory.Exists(path));
                new CommandLine().Import(new Uri(tempUri, "/copy-of-root/").AbsoluteUri, "http://127.0.0.1:11080", /* Recursive: */ true, true);
                Assert.IsTrue(Directory.Exists(path));

                List<string> expect = new List<string>(KnownLinks);
                expect.Remove("/?s=%00");//not linked, added by search
                expect.Remove("/search.css");//not linked, added by search
                expect.Remove("/search/");//not linked, added by search
                expect.Remove("/");//root was renamed
                expect.Add("/copy-of-root/");//new rename of root path
                
                VerifyLinks(path, expect);
            }
        }
        
        [Test]
        public void TestRemove()
        {
            SetupW3Example();
            List<string> remaining = new List<string>(KnownLinks);

            while (remaining.Count > 0)
            {
                string url = remaining[0];
                remaining.RemoveAt(0);
                new CommandLine().Remove(new Uri(W3ExampleUrl, url).AbsoluteUri);

                VerifyLinks(W3ExampleDirectory, remaining);
            }
        }

        [Test]
        public void TestRename()
        {
            SetupW3Example();

            foreach(string url in KnownLinks)
                new CommandLine().Rename(
                    new Uri(W3ExampleUrl, "/sub-directory" + url).AbsoluteUri,
                    new Uri(W3ExampleUrl, url).AbsoluteUri,
                    false);

            VerifyLinks(W3ExampleDirectory, KnownLinks.Select(u => "/sub-directory" + u));
        }

        [Test]
        public void TestArchive()
        {
            SetupW3Example();
            new CommandLine().Archive(W3ExampleUrl.AbsoluteUri);

            string[] files = Directory.GetFiles(W3ExampleDirectory, "*.zip");
            Assert.AreEqual(1, files.Length);
            using (ZipFile z = new ZipFile(files[0]))
            {
                foreach(ZipEntry e in z.Entries)
                {
                    if (e.IsDirectory) continue;
                    string path = Path.Combine(W3ExampleDirectory, e.FileName);
                    byte[] content = File.ReadAllBytes(path);
                    Assert.AreEqual(content, IOStream.ReadAllBytes(e.OpenReader()));
                }
            }
        }

        [Test]
        public void TestLinks()
        {
            SetupW3Example();
            StringWriter sw = new StringWriter();
            TextWriter old = Console.Out;
            try
            {
                Console.SetOut(sw);
                new CommandLine().Optimize(W3ExampleUrl.AbsoluteUri, false);
                new CommandLine().Links(W3ExampleUrl.AbsoluteUri, false, false, true, false);
            }
            finally
            {
                Console.SetOut(old);
            }

            List<string> linksExpected = new List<string>(KnownLinks.Select(s => new Uri(W3ExampleUrl, s).AbsoluteUri));

            linksExpected.Remove(W3ExampleUrl.AbsoluteUri + "?s=%00");
            linksExpected.Remove(W3ExampleUrl.AbsoluteUri + "2011/09/23/hello-world-2-0/?like=1");
            linksExpected.Add("http://w3example.wordpress.com/?pushpress=hub");
            linksExpected.Add("http://w3example.wordpress.com/2011/09/23/hello-world-2-0/#comments");
            linksExpected.Add("http://w3example.wordpress.com/osd.xml");
            linksExpected.Add("http://w3example.wordpress.com/wp-admin");
            linksExpected.Add("http://w3example.wordpress.com/wp-admin/post.php?post=1&action=edit");
            linksExpected.Add("http://w3example.wordpress.com/wp-admin/post-new.php");
            linksExpected.Add("http://w3example.wordpress.com/wp-admin/tools.php");
            linksExpected.Sort(StringComparer.OrdinalIgnoreCase);

            List<string> actualLinks = new List<string>(sw.ToString().Replace("\r", "").Trim().Split('\n'));
            actualLinks.Sort(StringComparer.OrdinalIgnoreCase);

            Assert.AreEqual(
                String.Join(Environment.NewLine, linksExpected.ToArray()), 
                String.Join(Environment.NewLine, actualLinks.ToArray())
                );
        }

        [Test]
        public void TestList()
        {
            SetupW3Example();
            StringWriter sw = new StringWriter();
            TextWriter old = Console.Out;
            try
            {
                Console.SetOut(sw);
                new CommandLine().List(W3ExampleUrl.AbsoluteUri, false);
            }
            finally
            {
                Console.SetOut(old);
            }

            Assert.AreEqual(String.Join(Environment.NewLine, KnownLinks).Trim(), sw.ToString().Trim());
        }

        [Test]
        public void TestLinkSource()
        {
            SetupW3Example();
            StringWriter sw = new StringWriter();
            TextWriter old = Console.Out;
            try
            {
                Console.SetOut(sw);
                new CommandLine().LinkSource(W3ExampleUrl.AbsoluteUri, W3ExampleUrl.AbsoluteUri + "about");
            }
            finally
            {
                Console.SetOut(old);
            }

            Assert.AreEqual("/w3example/", sw.ToString().Trim());
        }

        [Test]
        public void TestReLink()
        {
            SetupW3Example();

            Assert.AreEqual(1, CountLinks(W3ExampleDirectory, u => u.PathAndQuery == "/about"));
            int prevCount = CountLinks(W3ExampleDirectory, u => u.PathAndQuery == "/about-me/");

            new CommandLine().Relink(W3ExampleUrl.AbsoluteUri, 
                W3ExampleUrl.AbsoluteUri + "about",
                W3ExampleUrl.AbsoluteUri + "about-me/");

            Assert.AreEqual(0, CountLinks(W3ExampleDirectory, u => u.PathAndQuery == "/about"));
            Assert.AreEqual(prevCount + 1, CountLinks(W3ExampleDirectory, u => u.PathAndQuery == "/about-me/"));
        }
    }
}
