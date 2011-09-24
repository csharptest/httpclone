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
using CSharpTest.Net.Collections;
using CSharpTest.Net.Serialization;
using CSharpTest.Net.Synchronization;
using CSharpTest.Net.IO;
using CSharpTest.Net.Interfaces;
using CSharpTest.Net.Crypto;
using Google.ProtocolBuffers;

namespace CSharpTest.Net.HttpClone.Storage
{
    public partial class ContentStorage : IDictionary<string, ContentRecord>, IDisposable
    {
        private readonly bool _readonly;
        private readonly BPlusTree<string, ContentRecord> _index;
        private readonly string _storageDir;
        private readonly string _dataDir;
        private readonly string _indexDir;
        private readonly DisposingList _disposables;

        public ContentStorage(string directory, bool asReadonly)
        {
            _disposables = new DisposingList();
            _readonly = asReadonly;

            _storageDir = directory;
            _dataDir = Path.Combine(directory, "content");
            if (!_readonly && !Directory.Exists(_dataDir))
                Directory.CreateDirectory(_dataDir);
            _indexDir = Path.Combine(directory, "index");

            BPlusTree<string, ContentRecord>.Options options = new BPlusTree<string, ContentRecord>.Options(
                PrimitiveSerializer.Instance, new ProtoSerializer<ContentRecord, ContentRecord.Builder>()
            );
            options.CacheKeepAliveMaximumHistory = 1000;
            options.CacheKeepAliveMinimumHistory = 100;
            options.CacheKeepAliveTimeout = int.MaxValue;
            options.CachePolicy = asReadonly ? CachePolicy.All : CachePolicy.Recent;

            options.CreateFile = asReadonly ? CreatePolicy.Never : CreatePolicy.IfNeeded;
            options.FileName = Path.Combine(directory, "content.index");
            options.FileBlockSize = 0x02000; //8kb
            options.ReadOnly = asReadonly;
            options.CallLevelLock = asReadonly ? (ILockStrategy)new IgnoreLocking() : new SimpleReadWriteLocking();
            options.LockingFactory = asReadonly ? (ILockFactory)new LockFactory<IgnoreLocking>() : new LockFactory<SimpleReadWriteLocking>();

            options.CalcBTreeOrder(64, 256);
            _index = new BPlusTree<string, ContentRecord>(options);
            _disposables.Add(_index);

            _index.EnableCount();
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }

        public int Count { get { return _index.Count; } }
        public bool IsReadOnly { get { return _readonly; } }
        public string IndexDirectory { get { return _indexDir; } }
        public string StorageDirectory { get { return _storageDir; } }

        private void AssertModify() { if (IsReadOnly) throw new InvalidOperationException(); }

        public void Clear()
        {
            AssertModify();
            _index.Clear();
        }

        private string FileName(ulong fileId)
        {
            return Path.Combine(_dataDir, fileId.ToString("x16"));
        }

        public string GetContentFile(ContentRecord record)
        {
            if (record.HasContentStoreId)
                return FileName(record.ContentStoreId);
            return null;
        }

        public void WriteContent(ContentRecord rec, byte[] bytes)
        {
            bool[] modified = new bool[1];
            modified[0] = false;
            bool success = Update(rec.ContentUri,
                r =>
                {
                    ContentRecord.Builder b = r.ToBuilder();
                    using (ITransactable t = WriteContent(b, bytes))
                        t.Commit();
                    ContentRecord newRec = b.Build();
                    modified[0] = !newRec.Equals(r);
                    return newRec;
                }
            );
            if (!success && modified[0])
                throw new ApplicationException("Record not found.");
        }

        public ITransactable WriteContent(ContentRecord.Builder builder, byte[] contents)
        {
            byte[] compressed;
            builder.SetContentLength((uint)contents.Length);
            if (contents.TryCompress(out compressed))
            {
                builder.SetHashContents(Hash.SHA256(compressed).ToString());
                builder.SetCompressedLength((uint)compressed.Length);
                return WriteBytes(builder, compressed);
            }
            else
            {
                builder.SetHashContents(Hash.SHA256(contents).ToString());
                builder.ClearCompressedLength();
                return WriteBytes(builder, contents);
            }
        }

        private ITransactable WriteBytes(ContentRecord.Builder builder, byte[] content)
        {
            AssertModify();
            if(!builder.HasContentStoreId)
            {
                ulong fid;
                string name;
                do
                {
                    fid = Guid.NewGuid().ToUInt64();
                    name = FileName(fid);
                } 
                while(File.Exists(name));

                builder.SetContentStoreId(fid);
            }

            ReplaceFile f = new ReplaceFile(FileName(builder.ContentStoreId));
            f.WriteAllBytes(content);
            return f;
        }

        public byte[] ReadContent(ContentRecord record) { return ReadContent(record, true); }
        public byte[] ReadContent(ContentRecord record, bool decompress)
        {
            if (!record.HasContentStoreId)
                throw new InvalidOperationException();

            byte[] result = File.ReadAllBytes(FileName(record.ContentStoreId));

            if (Hash.SHA256(result).ToString() != record.HashContents)
                throw new InvalidDataException();

            if (decompress && record.HasCompressedLength)
                result = result.Decompress(record.ContentLength);

            return result;
        }

        public ContentRecord.Builder New(string uri, DateTime timestamp)
        {
            return new ContentRecord.Builder()
                .SetId(ByteString.CopyFrom(Guid.NewGuid().ToByteArray()))
                .SetContentUri(uri)
                .SetDateCreated(timestamp)
                ;
        }

        public void Rename(string source, string dest)
        {
            ContentRecord rec;
            Check.Assert<ArgumentException>(_index.TryGetValue(source, out rec), "The source was not found.");
            rec = rec.ToBuilder().SetContentUri(dest).Build();
            Check.Assert<ArgumentException>(_index.Add(dest, rec), "The target already exists.");
            _index.Remove(source);
        }

        #region IDictionary<string,ContentRecord> Members

        void IDictionary<string,ContentRecord>.Add(string key, ContentRecord value)
        {
            if (!Add(key, value))
                throw new DuplicateKeyException();
        }

        public bool Add(string key, ContentRecord value)
        {
            AssertModify();
            return _index.Add(key, value);
        }

        public bool AddOrUpdate(string key, ContentRecord value)
        {
            AssertModify();
            return _index.AddOrUpdate(key, value);
        }

        public bool Update(string key, Converter<ContentRecord, ContentRecord> updater)
        {
            AssertModify();
            return _index.Update(key, updater);
        }

        public bool ContainsKey(string key)
        {
            return _index.ContainsKey(key);
        }

        public ICollection<string> Keys
        {
            get { return _index.Keys; }
        }

        public bool Remove(string key)
        {
            AssertModify();
            ContentRecord cr;
            if(_index.TryGetValue(key, out cr) && _index.Remove(key))
            {
                if (cr.HasContentStoreId)
                {
                    File.Delete(FileName(cr.ContentStoreId));
                }
            }
            return false;
        }

        public bool TryGetValue(string key, out ContentRecord value)
        {
            return _index.TryGetValue(key, out value);
        }

        public ICollection<ContentRecord> Values
        {
            get { return _index.Values; }
        }

        public ContentRecord this[string key]
        {
            get { return _index[key]; }
            set 
            { 
                AssertModify();
                _index[key] = value;
            }
        }

        #endregion
        #region ICollection<KeyValuePair<string, ContentRecord>> Members

        bool ICollection<KeyValuePair<string, ContentRecord>>.Contains(KeyValuePair<string, ContentRecord> item)
        {
            throw new NotImplementedException();
        }

        void ICollection<KeyValuePair<string, ContentRecord>>.CopyTo(KeyValuePair<string, ContentRecord>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        void ICollection<KeyValuePair<string, ContentRecord>>.Add(KeyValuePair<string, ContentRecord> item)
        {
            throw new NotImplementedException();
        }

        bool ICollection<KeyValuePair<string, ContentRecord>>.Remove(KeyValuePair<string, ContentRecord> item)
        {
            throw new NotImplementedException();
        }

        #endregion

        public IEnumerator<KeyValuePair<string, ContentRecord>> GetEnumerator()
        { return _index.GetEnumerator(); }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        { return _index.GetEnumerator(); }
    }
}
