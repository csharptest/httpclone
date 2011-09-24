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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CSharpTest.Net.IpcChannel;
using CSharpTest.Net.Synchronization;
using CSharpTest.Net.HttpClone.Storage;
using CSharpTest.Net.Crypto;
using CSharpTest.Net.HttpClone.Common;

namespace CSharpTest.Net.HttpClone.Services
{
    class ContentState : IDisposable
    {
        static class Events
        {
            public const string ContentUpdate = "Content-Update";
            public const string CompletionAck = "S_OK";
        }
        private readonly ILockStrategy _executionLock;
        private readonly RSAKeyPair _rsaKeyPair;
        private readonly IpcEventChannel _channel;

        private ContentStorage _content;

        public ContentState()
            : this(null)
        {
        }

        public ContentState(ContentStorage content)
        {
            //_executionLock = new SimpleReadWriteLocking();
            _executionLock = IgnoreLocking.Instance;
            _rsaKeyPair = ReadKeyFile();
            _content = content ?? ReadCurrent();

            _channel = new IpcEventChannel(Path.Combine(Settings.RegistryPath, "IISChannel"),
                BitConverter.ToString(Hash.MD5(Encoding.UTF8.GetBytes(StoragePath)).ToArray()));

            _channel.OnError += (o, e) => Log.Error(e.GetException());
            _channel[Events.ContentUpdate].OnEvent += OnContentUpdated;
            _channel[Events.CompletionAck].OnEvent += (o, e) => { };
            _channel.StartListening();
        }

        public void Dispose()
        {
            if (_content != null)
            {
                using (ExecutionLock.Write(LockTimeout))
                {
                    if (_content != null)
                        _content.Dispose();
                    _content = null;

                    if (_rsaKeyPair != null)
                        _rsaKeyPair.Dispose();
                }
            }
        }

        public RSAKeyPair KeyPair
        {
            get { return _rsaKeyPair; }
        }

        private ILockStrategy ExecutionLock
        {
            get { return _executionLock; }
        }

        public ReadLock BeginRequest()
        {
            try
            {
                return _executionLock.Read(LockTimeout);
            }
            catch (Exception e)
            {
                throw new CorruptApplicationDomainException(e);
            }
        }

        public int LockTimeout { get { return 60000; } }

        public ContentStorage Storage
        {
            get
            {
                if (_content == null)
                    throw new InvalidOperationException();
                return _content;
            }
        }

        public string StoragePath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Settings.StorePath); } }

        private void OnContentUpdated(object o, IpcSignalEventArgs e)
        {
            ContentStorage current = ReadCurrent();
            SwapStorage(current, false);
        }

        private ContentStorage ReadCurrent()
        {
            string fname = Path.Combine(StoragePath, "version");
            if (!File.Exists(fname))
            {
                string empty = Path.Combine(StoragePath, "EMPTY");
                new ContentStorage(empty, false).Dispose();
                File.WriteAllText(fname, "EMPTY");
            }

            fname = File.ReadAllText(fname);
            Check.Assert<ArgumentOutOfRangeException>(Regex.IsMatch(fname, @"^[\w_\-]{1,64}(?:\(\d{1,5}\))?$"));
            return new ContentStorage(Path.Combine(StoragePath, fname), true);
        }

        private RSAKeyPair ReadKeyFile()
        {
            string keypath = AppDomain.CurrentDomain.BaseDirectory;
            if(AppDomain.CurrentDomain.RelativeSearchPath != null)
                keypath = Path.Combine(keypath, AppDomain.CurrentDomain.RelativeSearchPath);

            keypath = Path.Combine(keypath, "server-publishing.key");
            return new RSAKeyPair(keypath, false);
        }

        private int BroadcastToOthers(string eventName)
        {
            string[] others = _channel.Registrar.GetRegisteredInstances(_channel.ChannelName)
                    .Where(name => name != _channel.InstanceId)
                    .ToArray();

            int sent = _channel.SendTo(1000, others, eventName, null);
            Log.Write("Informed {0} domains of new content.", sent);

            // We follow one event with another to wait for completion of the first.
            int ack = _channel.SendTo(1000, others, Events.CompletionAck, null);
            Log.Verbose("{0} domains confirmed updated/active.", ack);
            return sent;
        }

        private void SwapStorage(ContentStorage contentStorage, bool bNotify)
        {
            ContentStorage old;
            using (ExecutionLock.Write(LockTimeout))
            {
                if (bNotify)
                {
                    BroadcastToOthers(Events.ContentUpdate);
                }
                old = _content;
                _content = contentStorage;
            }
            if(old != null)
                old.Dispose();
        }

        public void ChangeStorage(ContentStorage contentStorage)
        {
            ExecutionLock.ReleaseRead(); //they already have a read-lock
            try
            {
                SwapStorage(contentStorage, true);
            }
            finally
            {
                try
                {
                    ExecutionLock.Read(LockTimeout);
                }
                catch (Exception e)
                {
                    throw new CorruptApplicationDomainException(e);
                }
            }

        }
    }
}
