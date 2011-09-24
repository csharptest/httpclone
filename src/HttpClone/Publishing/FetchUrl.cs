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
using CSharpTest.Net.HttpClone.Common;

namespace CSharpTest.Net.HttpClone.Publishing
{
    class FetchUrl
    {
        private readonly SiteCollector _collector;
        private readonly string _path;
        private HttpRequestUtil _request;
        private readonly Action<Action> _enqueue;

        public FetchUrl(SiteCollector collector, string path, string etag, Action<Action> enqueue)
        {
            _collector = collector;
            _path = path;
            _enqueue = enqueue;
            _request = new HttpRequestUtil(collector.BaseUri);

            if (!String.IsNullOrEmpty(etag))
                _request.RequestHeaders["If-None-Match"] = etag;
        }

        public void DoWork()
        {
            _request.Get(_path);
            if(_request.StatusCode == HttpStatusCode.OK)
            {
                _enqueue(
                    new SaveContent(_collector, _path, _enqueue, _request)
                        .DoWork
                    );
            }
            else if(_request.StatusCode == HttpStatusCode.Redirect 
                || _request.StatusCode == HttpStatusCode.Moved 
                || _request.StatusCode == HttpStatusCode.SeeOther)
            {
                _enqueue(
                    new SaveRedirect(_collector, _path, _enqueue, _request)
                        .DoWork
                    );
            }
            else if(_request.StatusCode == HttpStatusCode.NotModified)
            {
                _enqueue(
                    new SaveNotModified(_collector, _path, _enqueue, _request)
                        .DoWork
                    );
            }
            else
            {
                _enqueue(
                    new SaveHttpError(_collector, _path, _enqueue, _request)
                        .DoWork
                    );
            }
        }
    }
}
