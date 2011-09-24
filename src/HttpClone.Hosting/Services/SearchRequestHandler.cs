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
using System.Text;
using CSharpTest.Net.HttpClone.Common;

namespace CSharpTest.Net.HttpClone.Services
{
    class SearchRequestHandler : IRequestHandler
    {
        private object _templateReadFrom;
        private SearchTemplate _template;
        private readonly ContentState _content;
        private readonly string _urlPrefix = SearchTemplate.SearchPath;

        public SearchRequestHandler(ContentState content)
        {
            _content = content;
        }

        public bool IsMatch(string method, string rawUrl)
        {
            return method == "GET" && rawUrl.StartsWith(_urlPrefix);
        }

        public IContentResponse GetResponse(string method, string rawUrl, System.Collections.Specialized.NameValueCollection headers, System.IO.Stream inputStream)
        {
            string pageText = null;
            StringBuilder searchText = new StringBuilder();
            string[] args = rawUrl.Substring(_urlPrefix.Length).TrimStart('?').Split('#')[0].Split('&');
            foreach (string arg in args)
            {
                if (arg.StartsWith("page="))
                    pageText = arg.Substring(5);
                else
                    searchText.AppendFormat("{0} ", Uri.UnescapeDataString(arg.Substring(arg.IndexOf('=') + 1)).Replace('+', ' '));
            }

            int page;
            if (pageText == null || !int.TryParse(pageText, out page))
                page = 1;

            string query = searchText.ToString().Trim();

            if(_template == null || !Object.ReferenceEquals(_templateReadFrom, _content.Storage))
            {
                _templateReadFrom = _content.Storage;
                _template = new SearchTemplate(_content.Storage);
            }

            return new DynamicResponse(_template.RenderResults(query, page));
        }
    }
}
