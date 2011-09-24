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
using System.Net;
using System.IO;

namespace CSharpTest.Net.HttpClone.Services
{
    abstract class SimpleHttpResponse
    {
        protected abstract void SetStatusCode(int code, string desc);
        public abstract void AddHeader(string name, string value);
        public abstract void Write(byte[] bytes, int offset, int length);

        public virtual HttpStatusCode StatusCode { set { SetStatusCode((int) value, value.ToString()); } }
        public virtual string RedirectLocation { set { AddHeader("Location", value); } }
        public virtual string ContentType { set { AddHeader("Content-Type", value); } }
        public virtual long ContentLength { set { AddHeader("Content-Length", value.ToString()); } }
        public virtual string ContentEncoding { set { AddHeader("Content-Encoding", value); } }
        public virtual string TransferEncoding { set { AddHeader("Transfer-Encoding", value); } }

        public virtual DateTime LastModified { set { AddHeader("Last-Modified", value.ToString("r")); } }
        public virtual string ETag { set { AddHeader("Etag", String.Format("\"{0}\"", value)); } }

        public virtual void TransmitFile(string contentFile, long offset, long length)
        {
            byte[] buffer = new byte[Math.Min(ushort.MaxValue, length)];
            using(Stream io = new FileStream(contentFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                int amt;
                io.Position = offset;
                while(length > 0 && 0 != (amt = io.Read(buffer, 0, (int)Math.Min(buffer.Length, length))))
                {
                    Write(buffer, 0, amt);
                    offset += amt;
                    length -= amt;
                }
            }
        }
    }
}