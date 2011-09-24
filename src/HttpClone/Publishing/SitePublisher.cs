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
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Security.Cryptography;
using Ionic.Zip;
using CSharpTest.Net.Crypto;
using CSharpTest.Net.Formatting;
using CSharpTest.Net.IO;
using CSharpTest.Net.HttpClone.Common;
using CSharpTest.Net.HttpClone.Storage;

namespace CSharpTest.Net.HttpClone.Publishing
{
    class SitePublisher : IDisposable
    {
        private readonly string _storagePath;
        private readonly ContentStorage _content;
        private readonly Uri _siteUri;
        private readonly RSAKeyPair _rsaKeyPair;
        private readonly string _publishUri;

        public SitePublisher(string storagePath, string site)
        {
            _storagePath = storagePath;
            _siteUri = new Uri(site, UriKind.Absolute);
            _content = new ContentStorage(storagePath, true);

            string keyfile = Path.Combine(storagePath, "client-publishing.key");
            if(!File.Exists(keyfile))
                throw new FileNotFoundException("You must have a client publication key, see the command 'createkeys'", keyfile);
            _rsaKeyPair = new RSAKeyPair(keyfile, true);

            // we publish on the hash of both client and server keys so that if the handler is invoked there is already
            // a high-probability that the keyset will match.
            _publishUri = "/api/publish/" + Safe64Encoding.EncodeBytes(_rsaKeyPair.KeyPairHash.ToArray()) + "/";
        }

        public void Dispose()
        {
            _content.Dispose();
            _rsaKeyPair.Dispose();
        }

        public void CreateArchive()
        {
            string filename = CreateArchiveFile();
            Console.WriteLine("{0} {1:n2} mb", filename, new FileInfo(filename).Length / 1000.0 / 1024.0);
        }
        private string CreateArchiveFile()
        {
            string filename = Path.Combine(_storagePath, DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip");
            using(ZipFile output = new ZipFile())
            {
                output.UseZip64WhenSaving = Ionic.Zip.Zip64Option.AsNecessary;
                output.CompressionLevel = Ionic.Zlib.CompressionLevel.BestSpeed;
                output.SortEntriesBeforeSaving = false;
                output.AddFile(Path.Combine(_storagePath, "content.index"), "");

                foreach (KeyValuePair<string, ContentRecord> rec in _content)
                {
                    string relpath = _content.GetContentFile(rec.Value);
                    if (relpath != null)
                    {
                        relpath = relpath.Substring(_storagePath.Length).TrimStart('\\', '/');
                        output.AddFile(Path.Combine(_storagePath, relpath), Path.GetDirectoryName(relpath));
                    }
                }

                string idxPath = Path.Combine(_storagePath, "index");
                if (Directory.Exists(idxPath))
                    foreach (string file in Directory.GetFiles(idxPath))
                        output.AddFile(file, "index");

                output.Save(filename);
            }
            return filename;
        }

        public void Publish()
        {
            Publish(_siteUri);
        }

        public void Publish(Uri destination)
        {
            Console.WriteLine("Creating site archive...");
            string file = CreateArchiveFile();
            Console.WriteLine("Connecting to server...");
            
            SecureTransfer.Client client =
                new SecureTransfer.Client(
                    _rsaKeyPair.ClientPrivateKey,
                    _rsaKeyPair.ServerPublicKey,
                    (transferid, location, request) =>
                        {
                            byte[] body = IOStream.ReadAllBytes(request);
                            HttpRequestUtil http = new HttpRequestUtil(destination);

                            if (http.Post(_publishUri, "application/binary", body, body.Length) != HttpStatusCode.OK)
                                throw new WebException(String.Format(
                                    "The server returned an invalid response: {0}/{1}", 
                                    (int) http.StatusCode,
                                    http.StatusCode));

                            return new MemoryStream(http.Content);
                        }
                    );
            client.ProgressChanged += ProgressChanged;

            try { client.Upload(Path.GetFileName(file), file); }
            finally { Console.WriteLine(); }

            Console.WriteLine("Complete.");
        }

        void ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            Console.Write("\rSending archive to server: {0,3}%", e.ProgressPercentage);
        }
    }
}
