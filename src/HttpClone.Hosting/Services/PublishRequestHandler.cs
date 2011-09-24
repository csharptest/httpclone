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
using System.Collections.Specialized;
using CSharpTest.Net.IO;
using CSharpTest.Net.Crypto;
using CSharpTest.Net.Formatting;
using CSharpTest.Net.Serialization;
using CSharpTest.Net.Serialization.StorageClasses;
using CSharpTest.Net.HttpClone.Storage;
using Ionic.Zip;
using CSharpTest.Net.Utils;

namespace CSharpTest.Net.HttpClone.Services
{
    class PublishRequestHandler : IRequestHandler
    {
        private const string PathPrefix = "/api/publish/";
        private const string PathSetPassword = "/api/publish/set-password/";
        private string _publishUri;
        private readonly ContentState _content;

        public PublishRequestHandler(ContentState content)
        {
            _content = content;
        }

        protected string TransferDirectory
        {
            get { return Path.Combine(_content.StoragePath, "Transfers"); }
        }

        public bool IsMatch(string method, string rawUrl)
        {
            if(method == "POST" && rawUrl.StartsWith(PathPrefix, StringComparison.Ordinal))
            {
                if (_publishUri == null)
                {
                    try 
                    { _publishUri = PathPrefix + Safe64Encoding.EncodeBytes(_content.KeyPair.KeyPairHash.ToArray()) + "/"; }
                    catch (Exception e)
                    { _publishUri = String.Empty; Log.Error(e); /* no keys? */ }
                }
                return StringComparer.Ordinal.Equals(rawUrl, PathSetPassword)
                    || StringComparer.Ordinal.Equals(rawUrl, _publishUri);
            }
            return false;
        }

        public IContentResponse HandlePasswordSet(NameValueCollection headers, Stream inputStream)
        {
            INameValueStore store = new RegistryStorage(Settings.RegistryPath);
                
            int contentLen = int.Parse(headers["Content-Length"]);
            if (contentLen == 0)
            {
                using (RSAPrivateKey _temporaryKey = new RSAPrivateKey(2048))
                {
                    store.Write("Transfers", "temp-key", Convert.ToBase64String(_temporaryKey.ToArray()));
                    return new DynamicResponse("application/public-key", _temporaryKey.PublicKey.ToArray());
                }
            }

            string tempkey;
            if (contentLen <= 2048 && store.Read("Transfers", "temp-key", out tempkey))
            {
                byte[] bytes = IOStream.Read(inputStream, contentLen);
                using (RSAPrivateKey _temporaryKey = RSAPrivateKey.FromBytes(Convert.FromBase64String(tempkey)))
                    bytes = _temporaryKey.Decrypt(bytes);

                _content.KeyPair.SetServerPassword(bytes);
            }
            return DynamicResponse.Empty;
        }

        public IContentResponse GetResponse(string method, string rawUrl, NameValueCollection headers, Stream inputStream)
        {
            if (StringComparer.Ordinal.Equals(rawUrl, PathSetPassword))
                return HandlePasswordSet(headers, inputStream);

            if (!StringComparer.Ordinal.Equals(rawUrl, _publishUri))
                throw new ApplicationException("Unexpected path " + rawUrl);

            try
            {
                SecureTransfer.Server handler = new SecureTransfer.Server(
                    _content.KeyPair.ServerPrivateKey,
                    _content.KeyPair.ClientPublicKey,
                    new RegistryStorage(Path.Combine(Settings.RegistryPath, "Transfers")));

                handler.BeginTransfer += BeginTransfer;
                handler.BytesReceived += BytesReceived;
                handler.CompleteTransfer += CompleteTransfer;
                handler.ErrorRaised += ErrorRaised;

                using (Stream response = handler.Receive(inputStream))
                {
                    return new DynamicResponse("application/binary", IOStream.ReadAllBytes(response));
                }
            }
            catch(Exception error)
            {
                Log.Error(error);
                return HttpErrorHandler.Unauthorized.GetResponse(method, rawUrl, headers, inputStream);
            }
        }

        string GetPath(Guid transferId, string location)
        {
            Check.NotEmpty(location);
            Check.Assert<InvalidDataException>(FileUtils.MakeValidFileName(location) == location);
            return Path.Combine(TransferDirectory, FileUtils.MakeValidFileName(location));
        }

        void BeginTransfer(object sender, SecureTransfer.BeginTransferEventArgs e)
        {
            string path = GetPath(e.TransferId, e.Location);
            using (Stream io = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite))
            {
                io.SetLength(e.TotalSize);
            }
        }

        void BytesReceived(object sender, SecureTransfer.BytesReceivedEventArgs e)
        {
            string path = GetPath(e.TransferId, e.Location);
            using(Stream io = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                byte[] bytes = e.BytesReceived;
                Check.Assert<InvalidDataException>(io.Length >= e.WriteOffset + bytes.Length);
                io.Position = e.WriteOffset;
                io.Write(bytes, 0, bytes.Length);
            }
        }

        void CompleteTransfer(object sender, SecureTransfer.CompleteTransferEventArgs e)
        {
            string path = GetPath(e.TransferId, e.Location);
            using (Stream io = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Hash uploaded = Hash.FromBytes(e.ContentHash.CreateAlgorithm().ComputeHash(io));
                Check.Assert<InvalidDataException>(e.ContentHash.Equals(uploaded));
            }

            int ix = 1;
            string pathname = Path.Combine(_content.StoragePath, FileUtils.MakeValidFileName(Path.GetFileNameWithoutExtension(e.Location)));
            string originalPath = pathname;
            while (Directory.Exists(pathname))
            {
                pathname = String.Format("{0}({1})", originalPath, ix++);
            }

            string filename = GetPath(e.TransferId, e.Location);
            using (ZipFile zip = new ZipFile(filename))
                zip.ExtractAll(pathname);

            File.WriteAllText(Path.Combine(_content.StoragePath, "version"), Path.GetFileName(pathname));

            _content.ChangeStorage(new ContentStorage(pathname, true));
        }

        void ErrorRaised(object sender, ErrorEventArgs e)
        {
            Log.Error(e.GetException());
        }
    }
}

