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
using System.Text;
using CSharpTest.Net.Synchronization;

namespace CSharpTest.Net.HttpClone.Storage
{
    public class TextQueue : IDisposable
    {
        private readonly StreamReader _read;
        private readonly StreamWriter _write;
        private readonly ILockStrategy _readLock;
        private readonly ILockStrategy _writeLock;

        public TextQueue(string queueFile, bool synchronized)
        {
            Stream output = new FileStream(queueFile, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            output.Seek(0, SeekOrigin.End);
            _write = new StreamWriter(output, Encoding.UTF8);

            Stream input = new FileStream(queueFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            _read = new StreamReader(input, Encoding.UTF8);

            if (synchronized)
            {
                _writeLock = new SimpleReadWriteLocking();
                _readLock = new ExclusiveLocking();
            }
            else
            {
                _writeLock = _readLock = IgnoreLocking.Instance;
            }
        }

        public void Dispose() 
        {
            try
            {
                _readLock.Dispose();
            }
            finally
            {
                try
                {
                    _writeLock.Dispose();
                }
                finally
                {
                    _write.Dispose();
                }
                _read.Dispose();
            }
        }

        public void Enqueue(string value)
        {
            using(_writeLock.Write())
            {
                _write.WriteLine(value);
                _write.Flush();
            }
        }

        public void Enqueue(IEnumerable<string> values)
        {
            using (_writeLock.Write())
            {
                foreach(string value in values)
                    _write.WriteLine(value);
                _write.Flush();
            }
        }

        public bool TryDequeue(out string value)
        {
            using (_writeLock.Read())
            using (_readLock.Write())
            {
                value = _read.ReadLine();
            }
            return value != null;
        }
    }
}
