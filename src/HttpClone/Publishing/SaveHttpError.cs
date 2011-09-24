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
using CSharpTest.Net.HttpClone.Common;

namespace CSharpTest.Net.HttpClone.Publishing
{
    class SaveHttpError
    {
        private readonly SiteCollector _collector;
        private readonly string _path;
        private readonly Action<Action> _queue;
        private readonly HttpRequestUtil _request;

        public SaveHttpError(SiteCollector collector, string path, Action<Action> queue, HttpRequestUtil request)
        {
            _collector = collector;
            _path = path;
            _queue = queue;
            _request = request;
        }

        public void DoWork()
        {
            _collector.SetHttpError(_path, _request.StatusCode);
        }
    }
}
