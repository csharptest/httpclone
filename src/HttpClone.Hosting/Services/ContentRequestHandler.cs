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

namespace CSharpTest.Net.HttpClone.Services
{
    class ContentRequestHandler : IRequestHandler
    {
        private readonly ContentState _contentState;

        public ContentRequestHandler(ContentState contentState)
        {
            _contentState = contentState;
        }

        public bool IsMatch(string method, string rawUrl)
        {
            string testuri;
            return (method == "HEAD" || method == "GET") && FindPath(rawUrl, out testuri);
        }

        private bool FindPath(string rawUrl, out string foundUri)
        {
            foundUri = new Uri(rawUrl, UriKind.Relative).NormalizedPathAndQuery();
            if (_contentState.Storage.ContainsKey(foundUri))
                return true;

            int pos;
            char[] args = new char[] {'?', '&'};
            while((pos = foundUri.LastIndexOfAny(args)) > 0) //not possible to find index zero, i.e. '/?'
            {
                foundUri = foundUri.Substring(0, pos);
                if (_contentState.Storage.ContainsKey(foundUri))
                    return true;
            }
            return false;
        }

        public IContentResponse GetResponse(string method, string rawUrl, NameValueCollection headers, Stream inputStream)
        {
            string foundUri;
            if (!FindPath(rawUrl, out foundUri))
                return HttpErrorHandler.NotFound.GetResponse(method, rawUrl, headers, inputStream);

            return new ContentResponse(_contentState.Storage, new Uri(foundUri, UriKind.Relative));
        }
    }
}
