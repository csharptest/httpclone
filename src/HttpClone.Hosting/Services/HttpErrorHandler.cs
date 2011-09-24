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
using CSharpTest.Net.Crypto;

namespace CSharpTest.Net.HttpClone.Services
{
    sealed class HttpErrorHandler : IRequestHandler, IContentResponse
    {
        public static readonly IRequestHandler MethodNotAllowed = new HttpErrorHandler(HttpStatusCode.MethodNotAllowed);
        public static readonly IRequestHandler NotFound = new HttpErrorHandler(HttpStatusCode.NotFound);
        public static readonly IRequestHandler Unauthorized = new HttpErrorHandler(HttpStatusCode.Unauthorized);
        public static readonly IRequestHandler InternalServerError = new HttpErrorHandler(HttpStatusCode.InternalServerError);

        private HttpErrorHandler(HttpStatusCode code)
        {
            Status = code;
        }

        public HttpStatusCode Status { get; private set; }

        public DateTime LastModified { get { return DateTime.MinValue; } }
        public string ETag { get { return String.Empty; } }

        #region IContentResponse Members

        string IContentResponse.ContentType
        {
            get { return "application/binary"; }
        }

        string IContentResponse.Redirect
        {
            get { throw new NotSupportedException(); }
        }

        bool IContentResponse.HasFile
        {
            get { return false; }
        }

        string IContentResponse.ContentFile
        {
            get { throw new NotSupportedException(); }
        }

        int IContentResponse.ContentLength
        {
            get { return 0; }
        }

        byte[] IContentResponse.ContentBytes
        {
            get { return null; }
        }

        bool IContentResponse.Compressed
        {
            get { return false; }
        }

        int IContentResponse.CompressedLength
        {
            get { throw new NotSupportedException(); }
        }

        byte[] IContentResponse.CompressedBytes
        {
            get { throw new NotSupportedException(); }
        }

        void IDisposable.Dispose()
        {
        }

        #endregion

        #region IRequestHandler Members

        bool IRequestHandler.IsMatch(string method, string rawUrl)
        {
            return true;
        }

        IContentResponse IRequestHandler.GetResponse(string method, string rawUrl, System.Collections.Specialized.NameValueCollection headers, System.IO.Stream inputStream)
        {
            return this;
        }

        #endregion
    }
}
