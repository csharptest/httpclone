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
using System.ComponentModel;
using CSharpTest.Net.Commands;
using CSharpTest.Net.Crypto;
using CSharpTest.Net.Utils;
using CSharpTest.Net.HttpClone.Common;

namespace CSharpTest.Net.HttpClone.Commands
{
    partial class CommandLine
    {
        public void CreateKeys(string url, [DefaultValue(4096)]int keySize, [DefaultValue(null)] string clientKeyPassword, [DefaultValue(false)] bool noServerPassword)
        {
            ConfirmPrompt prompt = new ConfirmPrompt();
            string path = StoragePath(url);
            string serverFile = Path.Combine(path, "server-publishing.key");
            if (File.Exists(serverFile) && !prompt.Continue("Overwrite existing file " + serverFile))
                return;
            string clientFile = Path.Combine(path, "client-publishing.key");
            if (File.Exists(clientFile) && !prompt.Continue("Overwrite existing file " + clientFile))
                return;

            byte[] serverPassBytes = new byte[48];
            new System.Security.Cryptography.RNGCryptoServiceProvider().GetBytes(serverPassBytes);

            using (RSAPrivateKey skey = new RSAPrivateKey(keySize))
            using (RSAPrivateKey ckey = new RSAPrivateKey(keySize))
            using (RSAKeyPair server = new RSAKeyPair(skey, ckey.PublicKey))
            using (RSAKeyPair client = new RSAKeyPair(skey.PublicKey, ckey))
            {
                server.WriteTo(serverFile, noServerPassword ? null : (byte[])serverPassBytes.Clone());
                client.WriteTo(clientFile, clientKeyPassword == null ? null : Encoding.UTF8.GetBytes(clientKeyPassword));
            }
            using (RSAKeyPair server = new RSAKeyPair(serverFile, true))
            using (RSAKeyPair client = new RSAKeyPair(clientFile, true))
            { /* we can read both */
                Check.NotNull(server.ClientPublicKey);
                Check.NotNull(server.ServerPublicKey);
                Check.NotNull(client.ClientPublicKey);
                Check.NotNull(client.ServerPublicKey);
               
                if (clientKeyPassword != null)
                    client.SetClientPassword(Encoding.UTF8.GetBytes(clientKeyPassword));
            }

            serverFile = FileUtils.MakeRelativePath(Environment.CurrentDirectory.TrimEnd('\\') + '\\', serverFile);
            clientFile = FileUtils.MakeRelativePath(Environment.CurrentDirectory.TrimEnd('\\') + '\\', clientFile);

            Console.WriteLine("IMPORTANT: Do not loose or share these files for security reasons.");
            Console.WriteLine();
            Console.WriteLine("The client key file is: " + clientFile);
            Console.WriteLine("The server key file is: " + serverFile);
            Console.WriteLine(@"
NEXT STEPS:

 1. You must manually place the server key file in the web server's bin directory.  

 2. You should set the file ACL so that only the web server's account can read this file.
");
            if (!noServerPassword) Console.WriteLine(@"
 3. After that you will need to run the following command to allow the server account to
    access the private key:
    > HttpClone.exe ServerKeyPassword " + url + @" " + Convert.ToBase64String(serverPassBytes) + @"

 4. Don't screw it up since you only get one change then you must restart the server.
");
        }

        public void ClientKeyPassword(string site, string passwordText)
        {
            string keyfile = Path.Combine(StoragePath(site), "client-publishing.key");
            using (RSAKeyPair clientKey = new RSAKeyPair(keyfile, true))
                clientKey.SetClientPassword(Encoding.UTF8.GetBytes(passwordText));

        }

        public void ServerKeyPassword(string site, string passwordText)
        {
            HttpRequestUtil util = new HttpRequestUtil(new Uri(site, UriKind.Absolute));
            if (util.Post("/api/publish/set-password/", "application/binary", null, 0) == System.Net.HttpStatusCode.OK)
            {
                using (RSAPublicKey pk = RSAPublicKey.FromBytes(util.Content))
                {
                    byte[] passbytes = pk.Encrypt(Convert.FromBase64String(passwordText));
                    if (util.Post("/api/publish/set-password/", "application/binary", passbytes, passbytes.Length) == System.Net.HttpStatusCode.OK)
                    {
                        Console.WriteLine("Success.");
                        return;
                    }
                }
                Console.WriteLine("FAILURE: You need verify the key file and restart the server app domain to try again.");
            }
        }
    }
}
