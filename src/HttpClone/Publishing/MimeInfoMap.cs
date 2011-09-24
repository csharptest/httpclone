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
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CSharpTest.Net.HttpClone.Common;

namespace CSharpTest.Net.HttpClone.Publishing
{
    class MimeInfoMap
    {
        private readonly HttpCloneConfig _config;
        private static readonly Regex FileExtension = new Regex(@"\.\w{2,8}(?=$|\?|\#)");
        readonly Dictionary<string, HttpCloneDocType> _map = new Dictionary<string, HttpCloneDocType>(StringComparer.OrdinalIgnoreCase);

        public MimeInfoMap(Uri baseUri, string storageDir)
        {
            _config = Config.ReadConfig(baseUri, storageDir);
            foreach (HttpCloneDocType t in _config.DocumentTypes.Where(x=>!String.IsNullOrEmpty(x.MimeType)))
            {
                _map.Add(t.MimeType, t);
                if (t.Aliases != null)
                    foreach (var a in t.Aliases.Where(x => !String.IsNullOrEmpty(x.MimeType)))
                        _map.Add(a.MimeType, t);
            }
        }

        public HttpCloneDocType this[string rawMime]
        {
            get
            {
                HttpCloneDocType info;
                if (!_map.TryGetValue(rawMime, out info))
                {
                    info = new HttpCloneDocType()
                               {
                                   MimeType = rawMime,
                                   Type = ContentFormat.Unknown,
                                   FileExtension = null,
                                   Aliases = new HttpCloneDocType[0],
                               };
                    _map.Add(rawMime, info);
                }
                if (String.IsNullOrEmpty(info.FileExtension))
                {
                    info.FileExtension = ".unk";
                    foreach (Match m in Regex.Matches(rawMime, @"^\w*/(?<type>\w*)$"))
                        info.FileExtension = '.' + m.Groups["type"].Value.ToLower();
                }
                return info; 
            }
        }

        public ContentFormat GetContentFormat(string rawMime)
        { return this[rawMime].Type; }

        public bool TryGetTitleExpression(string rawMime, out Regex expression, out int groupId)
        {
            groupId = 0;
            expression = null;

            var info = this[rawMime];
            if (info.TitleExpression != null && info.TitleExpression.Expression != null)
            {
                expression = new Regex(info.TitleExpression.Expression, RegexOptions.Singleline | RegexOptions.IgnoreCase);
                groupId = info.TitleExpression.GroupId;
            }
            if (info.Type == ContentFormat.Html && expression == null)
            {
                expression = new Regex(@"<title>([^<]*)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                groupId = 1;
            }
            return expression != null;
        }

        public bool TryGetTitle(string rawMime, string contents, out string title)
        {
            Match titleMatch;
            Regex regexp;
            int groupId;
            if (TryGetTitleExpression(rawMime, out regexp, out groupId) &&
                (titleMatch = regexp.Match(contents)).Success && titleMatch.Groups[groupId].Success)
            {
                title = titleMatch.Groups[groupId].Value;
                if (this[rawMime].Type == ContentFormat.Html || this[rawMime].Type == ContentFormat.Xml)
                    title = HttpRequestUtil.HtmlDecode(title);
                title = (title ?? String.Empty).Trim();
                return !String.IsNullOrEmpty(title);
            }
            title = null;
            return false;
        }

        public string GetFileExtension(string rawMime)
        { return this[rawMime].FileExtension; }

        public string GetFileExtension(string rawMime, string path)
        {
            Match m = FileExtension.Match(path);
            if (m.Success)
                return m.Value;
            return GetFileExtension(rawMime);
        }

        public string FromExtension(string extension)
        {
            HttpCloneDocType type = _config.DocumentTypes.FirstOrDefault(
                d =>
                    StringComparer.OrdinalIgnoreCase.Equals(d.FileExtension, extension)
                    || null != d.Aliases.SafeEnumeration().FirstOrDefault(
                        a => StringComparer.OrdinalIgnoreCase.Equals(a.FileExtension, extension))
                    );

            if (type == null)
                throw new ApplicationException("Unknown file type " + extension);
            return type.MimeType;
        }
    }
}
