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
using System.Text;
using CSharpTest.Net.Crypto;

namespace CSharpTest.Net.HttpClone.Services
{
    class DynamicResponse : IContentResponse
    {
        public static readonly IContentResponse Empty = new DynamicResponse("application/binary", new byte[0]);

        private readonly string _contentType;
        private readonly byte[] _contentBody;

        public DynamicResponse(string contentHtml)
            : this("text/html; charset=UTF-8", Encoding.UTF8.GetBytes(contentHtml))
        { }

        public DynamicResponse(string contentType, byte[] contentBody)
        {
            _contentType = contentType;
            _contentBody = contentBody;
            LastModified = DateTime.Now;
            ETag = Hash.SHA256(ContentBytes).ToString();
        }

        public void Dispose()
        {
        }

        public HttpStatusCode Status
        {
            get { return HttpStatusCode.OK; }
        }

        public string ContentType
        {
            get { return _contentType; }
        }

        public DateTime LastModified { get; set; }
        public string ETag { get; set; }

        public string Redirect
        {
            get { throw new NotSupportedException(); }
        }

        public bool HasFile
        {
            get { return false; }
        }

        public string ContentFile
        {
            get { throw new NotSupportedException(); }
        }

        public int ContentLength
        {
            get { return _contentBody.Length; }
        }

        public byte[] ContentBytes
        {
            get { return _contentBody; }
        }

        public bool Compressed
        {
            get { return false; }
        }

        public int CompressedLength
        {
            get { throw new NotSupportedException(); }
        }

        public byte[] CompressedBytes
        {
            get { throw new NotSupportedException(); }
        }
    }
}
