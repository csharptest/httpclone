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
using CSharpTest.Net.HttpClone.Storage;

namespace CSharpTest.Net.HttpClone.Services
{
    internal class ContentResponse : IContentResponse
    {
        private readonly ContentStorage _content;
        private readonly ContentRecord _record;
        private readonly HttpStatusCode _status;

        public ContentResponse(ContentStorage content, Uri uri)
        {
            _content = content;
            _status = HttpStatusCode.InternalServerError;
            _record = ContentRecord.DefaultInstance;
            try
            {
                string path = uri.NormalizedPathAndQuery();
                if (_content.TryGetValue(path, out _record))
                {
                    if (_record.HasContentRedirect)
                        _status = HttpStatusCode.Redirect;
                    else
                        _status = HttpStatusCode.OK;
                }
                else
                {
                    _record = ContentRecord.DefaultInstance;
                    _status = HttpStatusCode.NotFound;
                    Log.Warning("404 - {0}", path);
                }
            }
            catch (Exception  ex)
            {
                Log.Error(ex, "Exception on {0}", uri);
            }
        }

        void IDisposable.Dispose() { }

        public HttpStatusCode Status
        {
            get { return _status; }
        }


        public DateTime LastModified { get { return _record.DateModified; } }
        public string ETag { get { return _record.HashContents; } }

        public string ContentType
        {
            get { return _record.MimeType; }
        }

        public string Redirect
        {
            get { return _record.HasContentRedirect ? _record.ContentRedirect : null; }
        }

        public bool HasFile { get { return _record.HasContentStoreId; } }
        public string ContentFile
        {
            get
            {
                if (!HasFile)
                    throw new InvalidOperationException();
                return _content.GetContentFile(_record);
            }
        }

        public int ContentLength { get { return (int)_record.ContentLength; } }
        public byte[] ContentBytes
        {
            get
            {
                if (_record.HasContentStoreId == false)
                    return null;
                return _content.ReadContent(_record, true);
            }
        }

        public bool Compressed { get { return _record.HasCompressedLength; } }
        public int CompressedLength { get { return (int)_record.CompressedLength; } }
        public byte[] CompressedBytes
        {
            get
            {
                if (!Compressed || !_record.HasContentStoreId)
                    throw new InvalidOperationException();
                return _content.ReadContent(_record, false);
            }
        }
    }
}
