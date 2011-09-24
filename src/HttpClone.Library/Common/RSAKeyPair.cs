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
using System.Text;
using System.Xml;
using CSharpTest.Net.Crypto;
using System.IO;
using CSharpTest.Net.Serialization.StorageClasses;

namespace CSharpTest.Net.HttpClone.Common
{
    public class RSAKeyPair : IDisposable
    {
        private readonly Stream _stream;
        private RSAKey _server, _client;
        private bool _loaded;

        public RSAKeyPair(RSAPublicKey server, RSAPublicKey client)
        {
            _client = client is RSAPrivateKey ? new RSAKey((RSAPrivateKey)client) : new RSAKey(client);
            _server = server is RSAPrivateKey ? new RSAKey((RSAPrivateKey)server) : new RSAKey(server);
        }

        public RSAKeyPair(string fileName, bool exclusiveAccess)
        {
            if (File.Exists(fileName))
            {
                _stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, exclusiveAccess ? FileShare.None : FileShare.Read);
                _loaded = false;
            }
            else
            {
                _stream = null;
                _loaded = true;
            }
        }

        public void Dispose()
        {
            if (_client != null)
                _client.Dispose();
            if (_server != null)
                _server.Dispose();
            if (_stream != null)
                _stream.Dispose();
        }

        #region Accessors

        public Hash KeyPairHash
        {
            get { Load(); return _client.PublicHash.Combine(_server.PublicHash); }
        }

        public RSAPrivateKey ClientPrivateKey
        {
            get { Load(); return _client.PrivateKey; }
        }

        public RSAPublicKey ClientPublicKey
        {
            get { Load(); return _client.PublicKey; }
        }

        public RSAPrivateKey ServerPrivateKey
        {
            get { Load(); return _server.PrivateKey; }
        }

        public RSAPublicKey ServerPublicKey
        {
            get { Load(); return _server.PublicKey; }
        }
        #endregion

        public void SetServerPassword(byte[] bytes)
        {
            Load();
            _server.SetPassword(bytes);
        }

        public void SetClientPassword(byte[] bytes)
        {
            Load();
            _client.SetPassword(bytes);
        }

        private void Load()
        {
            if (_loaded)
                return;
            _loaded = true;

            using (XmlReader reader = XmlReader.Create(_stream, new XmlReaderSettings() { CloseInput = false }))
            {
                reader.MoveToContent();
                reader.ReadStartElement("keyset");

                reader.ReadToFollowing("client");
                _client = new RSAKey(reader);
                
                reader.ReadToFollowing("server");
                _server = new RSAKey(reader);
                
                reader.ReadEndElement();
            }
        }

        public void WriteTo(string fileName, byte[] passbytes)
        {
            XmlWriterSettings settings = new XmlWriterSettings()
            {
                OmitXmlDeclaration = true,
                Encoding = new UTF8Encoding(false),
                Indent = true,
                IndentChars = "  ",
                NewLineChars = Environment.NewLine,
            };

            using (XmlWriter writer = XmlWriter.Create(fileName, settings))
            {
                writer.WriteStartElement("keyset");
                _client.WriteTo(writer, "client", passbytes);
                _server.WriteTo(writer, "server", passbytes);
                writer.WriteEndDocument();
            }
        }

        class RSAKey : IDisposable
        {
            private static readonly Guid IV = new Guid(0xE4A4BAFE, 0x0A8D, 0x4209, 0xBB, 0x20, 0xB8, 0x17, 0x36, 0x84, 0x12, 0xB2);
            private readonly Hash _publicHash, _privateHash;
            private readonly bool _hasPrivateKey, _passwordRequired;
            private readonly RSAPublicKey _publicKey;
            private readonly byte[] _privateBits;
            private RSAPrivateKey _privateKey;

            public RSAKey(XmlReader reader)
            {
                _privateKey = null;
                _hasPrivateKey = XmlConvert.ToBoolean(reader.GetAttribute("private") ?? "0");

                XmlReader subtree = reader.ReadSubtree();
                while (subtree.Read())
                {
                    if (subtree.LocalName == "public")
                    {
                        _publicHash = Hash.FromString(subtree.GetAttribute("hash"));
                        _publicKey = RSAPublicKey.FromXml(subtree.ReadSubtree());
                    }
                    else if (subtree.LocalName == "private")
                    {
                        _privateHash = Hash.FromString(subtree.GetAttribute("hash"));
                        _passwordRequired = XmlConvert.ToBoolean(subtree.GetAttribute("protected") ?? "0");
                        _privateBits = Convert.FromBase64String(subtree.ReadElementString("private"));
                    }
                }
            }

            public RSAKey(RSAPublicKey publicKey)
            {
                _publicKey = publicKey;
                _publicHash = Hash.SHA256(_publicKey.ToArray());
            }

            public RSAKey(RSAPrivateKey privateKey)
                : this(privateKey.PublicKey)
            {
                _hasPrivateKey = true;
                _privateBits = null;
                _privateKey = privateKey;
                _privateHash = Hash.SHA256(_privateKey.ToArray());
            }

            static T Validate<T>(T instance) where T : RSAPublicKey
            {
                if (instance == null)
                    throw new System.Security.Cryptography.CryptographicException(typeof(T).Name + " not found.");
                return instance;
            }

            public bool HasPrivateKey { get { return _hasPrivateKey; } }
            public Hash PublicHash { get { return _publicHash; } }
            public RSAPublicKey PublicKey { get { return Validate(_publicKey); } }
            public RSAPrivateKey PrivateKey
            {
                get
                {
                    if (_hasPrivateKey && _privateKey == null && _privateBits != null)
                    {
                        byte[] keybytes = _privateBits;
                        RegistryStorage store = new RegistryStorage(Settings.RegistryPath);
                        if (_passwordRequired)
                        {
                            string rawpassPhrase;
                            if (store.Read("CryptoKey", _privateHash.ToString(), out rawpassPhrase))
                            {
                                byte[] passbytes = Convert.FromBase64String(rawpassPhrase);
                                passbytes = Encryption.CurrentUser.Decrypt(passbytes);
                                using (Password pwd = new Password(true, passbytes))
                                {
                                    pwd.IV = IV.ToByteArray();
                                    keybytes = pwd.Decrypt(keybytes, Salt.Size.b256);
                                }
                            }
                        }
                        _privateKey = RSAPrivateKey.FromBytes(keybytes);
                        Array.Clear(_privateBits, 0, _privateBits.Length);
                    }
                    return Validate(_privateKey);
                }
            }

            public void Dispose()
            {
                if (_publicKey != null)
                    _publicKey.Dispose();
                if (_privateKey != null)
                    _privateKey.Dispose();
                if (_privateBits != null)
                    Array.Clear(_privateBits, 0, _privateBits.Length);
            }

            public void SetPassword(byte[] passbytes)
            {
                Check.ArraySize(passbytes, 1, 2048);
                Check.Assert<InvalidOperationException>(_passwordRequired && _privateBits != null && _privateKey == null);
                try
                {
                    byte[] keybytes;
                    using (Password pwd = new Password(false, passbytes))
                    {
                        pwd.IV = IV.ToByteArray();
                        keybytes = pwd.Decrypt(_privateBits, Salt.Size.b256);
                    }
                    Check.Assert<InvalidDataException>(_privateHash.Equals(Hash.SHA256(keybytes)));
                    _privateKey = RSAPrivateKey.FromBytes(keybytes);
                    Check.Assert<InvalidDataException>(_publicHash.Equals(Hash.SHA256(_privateKey.PublicKey.ToArray())));
                    
                    passbytes = Encryption.CurrentUser.Encrypt(passbytes);
                    RegistryStorage store = new RegistryStorage(Settings.RegistryPath);
                    store.Write("CryptoKey", _privateHash.ToString(), Convert.ToBase64String(passbytes));
                }
                catch(Exception err)
                {
                    Log.Error(err);
                    throw new InvalidDataException();
                }
            }

            public void WriteTo(XmlWriter writer, string elementName, byte[] passbytes)
            {
                writer.WriteStartElement(elementName);
                writer.WriteAttributeString("private", XmlConvert.ToString(_hasPrivateKey));

                writer.WriteStartElement("public");
                writer.WriteAttributeString("hash", _publicHash.ToString());
                _publicKey.ToXml(writer);
                writer.WriteEndElement();

                if(HasPrivateKey)
                {
                    writer.WriteStartElement("private");
                    writer.WriteAttributeString("hash", _privateHash.ToString());

                    bool encrypted = passbytes != null && passbytes.Length > 0;
                    byte[] bits = PrivateKey.ToArray();

                    if(encrypted)
                    {
                        writer.WriteAttributeString("protected", XmlConvert.ToString(true));
                        using (Password pwd = new Password(true, passbytes))
                        {
                            pwd.Salt = new Salt(Salt.Size.b256);
                            pwd.IV = IV.ToByteArray();
                            bits = pwd.Encrypt(bits);
                        }
                    }
                    writer.WriteString(Convert.ToBase64String(bits));
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }
        }
    }
}
