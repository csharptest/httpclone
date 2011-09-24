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
using System.Net;
using System.IO;
using System.Text;

namespace CSharpTest.Net.HttpClone.Common
{
    public class PathExclusionList
    {
        private readonly List<string> _items;
        public PathExclusionList()
        {
            _items = new List<string>();
        }

        public PathExclusionList(IEnumerable<string> items) : this()
        {
            AddRange(items);
        }

        public void Add(string item)
        {
            if (String.IsNullOrEmpty(item))
                return;

            item = item.Trim();
            if (String.IsNullOrEmpty(item) || item[0] != '/' || IsExcluded(item))
                return;

            int pos = _items.BinarySearch(item, StringComparer.Ordinal);
            if (pos < 0)
                _items.Insert(~pos, item);
        }

        public void AddRange(IEnumerable<string> items)
        {
            if (items != null)
            {
                foreach (string item in items)
                    Add(item);
            }
        }

        public bool IsExcluded(string path)
        {
            int pos = _items.BinarySearch(path, StringComparer.Ordinal);
            if (pos >= 0)
                return true;

            pos = ~pos;
            if (pos > 0 && path.StartsWith(_items[pos - 1], StringComparison.Ordinal))
                return true;

            return false;
        }

        public bool ReadRobotsFile(Uri baseUri, string userAgent)
        {
            HttpRequestUtil http = new HttpRequestUtil(baseUri);
            if (http.Get("/robots.txt") != HttpStatusCode.OK)
                return false;

            using(TextReader rdr = new StreamReader(new MemoryStream(http.Content), Encoding.UTF8))
            {
                string line;
                bool matched = false;
                char[] divide = new[] {':'};
            
                while(null != (line = rdr.ReadLine()))
                {
                    if (line.Length == 0 || line[0] == '#')
                        continue;

                    string[] parts = line.Split(divide, 2);
                    if(parts.Length != 2)
                        continue;

                    parts[0] = parts[0].Trim().ToLower();
                    switch(parts[0])
                    {
                        case "user-agent":
                            matched = parts[1].Trim() == "*" || parts[1].Trim() == userAgent;
                            break;
                        case "disallow":
                            if(matched)
                                Add(parts[1]);
                            break;
                    }
                }
            }
            return true;
        }
    }
}
