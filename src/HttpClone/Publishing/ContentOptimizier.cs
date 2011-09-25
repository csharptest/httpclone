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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CSharpTest.Net.HttpClone.Storage;
using CSharpTest.Net.Html;
using CSharpTest.Net.Utils;

namespace CSharpTest.Net.HttpClone.Publishing
{
    class ContentOptimizier : IDisposable
    {
        private readonly Uri _baseUri;
        private readonly ContentStorage _content;
        private readonly HttpCloneConfig _config;

        public ContentOptimizier(string storagePath, string baseUri)
        {
            _baseUri = new Uri(baseUri, UriKind.Absolute);
            _config = Config.ReadConfig(_baseUri, storagePath);
            _content = new ContentStorage(storagePath, false);
        }

        public bool CondenseHtml { get; set; }

        public void Dispose()
        {
            _content.Dispose();
        }

        public void OptimizeAll()
        {
            RunOptimizer(null);
        }

        public void OptimizePage(string path)
        {
            RunOptimizer(x => x.ContentUri == path);
        }

        private void RunOptimizer(Predicate<ContentRecord> filter)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                        { "site.uri", _baseUri.AbsoluteUri }
                    };

            foreach (var type in _config.DocumentTypes.Where(t => t.Optimizations != null))
            {
                ILookup<string, HttpCloneFileType> lookup = new[] {type}
                    .Concat(type.Aliases.SafeEnumeration())
                    .ToLookup(a => a.MimeType);

                ContentParser processor = new ContentParser(_content, _baseUri);
                processor.Reformat = CondenseHtml;
                processor.RelativeUri = true;
                processor.IndentChars = String.Empty;

                if (type.Type == ContentFormat.Html && _config.Searching != null && _config.Searching.FormXPath != null)
                    processor.RewriteXmlDocument += RewriteSearchForm;

                if (type.Type == ContentFormat.Html || type.Type == ContentFormat.Xml)
                {
                    new XmlRewriter(processor, type.Optimizations, values);
                }
                new RegexRewriter(processor, type.Optimizations, values);

                processor.ContextChanged +=
                    r =>
                    {
                        values["page.path"] = r.ContentUri;
                        values["page.uri"] = new Uri(_baseUri, r.ContentUri).AbsoluteUri;
                        values["page.mime"] = r.MimeType;
                    };
                processor.Process(x => (filter == null || filter(x)) && lookup.Contains(x.MimeType));
            }
        }

        private bool RewriteSearchForm(XmlLightDocument doc)
        {
            if (_config.Searching.FormXPath != null)
            {
                XmlLightElement selectFrom = doc.Root;
                if (_config.Searching.XPathBase != null)
                    selectFrom = selectFrom.SelectRequiredNode(_config.Searching.XPathBase.XPath);

                XmlLightElement found = selectFrom.SelectRequiredNode(_config.Searching.FormXPath.XPath);
                found.Attributes["method"] = "GET";
                found.Attributes["action"] = new Uri(_baseUri, "search/").AbsoluteUri;
                return true;
            }
            return false;
        }

        class RegexRewriter
        {
            private readonly ContentParser _processor;
            private readonly HttpCloneOptimizations _optimizations;
            private readonly IDictionary<string, string> _namedValues;
            private readonly Dictionary<object, HttpCloneOptimizationReplace> _replaces;
            private readonly HttpCloneMatch[] _matches;
            private readonly Regex[] _expressions;

            private ContentRecord _context;

            public RegexRewriter(ContentParser processor, HttpCloneOptimizations optimizations, IDictionary<string, string> namedValues)
            {
                _processor = processor;
                _optimizations = optimizations;
                _namedValues = namedValues;

                _replaces = new Dictionary<object, HttpCloneOptimizationReplace>();
                foreach (var rep in optimizations.AllItems())
                    foreach (var i in rep.ReplaceItem)
                        _replaces.Add(i, rep);

                _matches = optimizations.AllItems()
                    .SelectMany(item => item.ReplaceItem.OfType<HttpCloneMatch>())
                    .ToArray();
                _expressions = _matches.Select(m => new Regex(m.Expression)).ToArray();

                if (_matches.Length > 0)
                {
                    _processor.RewriteContent += RewriteContent;
                    _processor.ContextChanged += ContextChanged;
                }
            }

            void ContextChanged(ContentRecord obj)
            {
                _context = obj;
            }

            string RewriteContent(string content)
            {
                for (int i = 0; i < _expressions.Length; i++)
                {
                    HttpCloneMatch key = _matches[i];
                    if(_expressions[i].IsMatch(content))
                    {
                        content = _expressions[i].Replace(content, 
                                m => GetReplacementText(m, key.GroupId, key)
                            );
                    }
                }
                return content;
            }

            private string GetReplacementText(Match match, int groupId, object key)
            {
                string prefix = String.Empty;
                string suffix = String.Empty;

                if (groupId > 0)
                {
                    if (match.Groups[groupId].Success == false)
                        return match.Value;

                    prefix = match.Value.Substring(0,
                    match.Groups[groupId].Index - match.Index
                    );
                    suffix = match.Value.Substring(
                        match.Groups[groupId].Index
                        + match.Groups[groupId].Length
                        - match.Index);
                }

                string replaceText = String.Empty;

                HttpCloneOptimizationReplace replacement = _replaces[key];
                if (!String.IsNullOrEmpty(replacement.ReplacementValue))
                {
                    replaceText = replacement.ReplacementValue;
                    if (replacement.ExpandValue)
                    {
                        replaceText = RegexPatterns.FormatNameSpecifier.Replace(replaceText,
                            m =>
                            {
                                if (_namedValues.ContainsKey(m.Groups["field"].Value))
                                    return _namedValues[m.Groups["field"].Value];
                                else if (match.Groups[m.Groups["field"].Value].Success)
                                    return match.Groups[m.Groups["field"].Value].Value;
                                return m.Value;
                            });
                    }
                }

                return prefix + replaceText + suffix;
            }
        }

        class XmlRewriter
        {
            private readonly ContentParser _processor;
            private readonly HttpCloneOptimizations _optimizations;
            private readonly IDictionary<string, string> _namedValues;
            private readonly Dictionary<object, HttpCloneOptimizationReplace> _replaces;
            private readonly Dictionary<XmlLightElement, object> _elements;
            private readonly ILookup<string, HttpCloneTag> _bytag;
            private readonly HttpCloneXPath[] _xpaths;

            private ContentRecord _context;

            public XmlRewriter(ContentParser processor, HttpCloneOptimizations optimizations, IDictionary<string, string> namedValues)
            {
                _context = ContentRecord.DefaultInstance;
                _processor = processor;
                _optimizations = optimizations;
                _namedValues = namedValues;
                _elements = new Dictionary<XmlLightElement, object>();
                _replaces = new Dictionary<object, HttpCloneOptimizationReplace>();
                foreach (var rep in optimizations.AllItems())
                    foreach (var i in rep.ReplaceItem)
                        _replaces.Add(i, rep);

                _bytag = optimizations.AllItems()
                    .SelectMany(item => item.ReplaceItem.OfType<HttpCloneTag>())
                    .ToLookup(t => t.TagName);

                _xpaths = optimizations.AllItems()
                    .SelectMany(item => item.ReplaceItem.OfType<HttpCloneXPath>())
                    .ToArray();

                if (_bytag.Count > 0 || _xpaths.Length > 0)
                {
                    processor.ContextChanged += ContextChanged;
                    processor.RewriteElement += RewriteElement;
                    processor.RewriteXmlDocument += RewriteXmlDocument;
                }
            }

            void ContextChanged(ContentRecord obj)
            { 
                _context = obj;
                _elements.Clear();
            }

            XmlLightElement GetReplacementNode(XmlLightElement e, object key)
            {
                HttpCloneOptimizationReplace replacement = _replaces[key];
                if (String.IsNullOrEmpty(replacement.ReplacementValue))
                    return null;

                string replaceText = replacement.ReplacementValue;
                if (replacement.ExpandValue)
                {
                    replaceText = RegexPatterns.FormatNameSpecifier.Replace(replaceText,
                        m =>
                        {
                            string tmp;
                            if (_namedValues.TryGetValue(m.Groups["field"].Value, out tmp)
                                || e.Attributes.TryGetValue(m.Groups["field"].Value, out tmp))
                                return tmp;
                            return m.Value;
                        });
                }

                XmlLightElement newElem = new XmlLightDocument(replaceText).Root;
                newElem.Parent = null;
                return newElem;
            }

            XmlLightElement RewriteElement(XmlLightElement e)
            {
                e = ProcessXPaths(e);
                if (e != null && _bytag.Contains(e.TagName))
                {
                    var replaced = e;
                    foreach (var key in _bytag[e.TagName].Where(t => _processor.IsTagMatch(e, t)))
                    {
                        replaced = GetReplacementNode(replaced, key);
                        if (replaced == null)
                            break;
                    }

                    return replaced;
                }
                return e;
            }

            private bool RewriteXmlDocument(XmlLightDocument doc)
            {
                foreach (HttpCloneXPath path in _xpaths)
                {
                    foreach (XmlLightElement found in doc.Select(path.XPath))
                    {
                        _elements.Add(found, path);
                    }
                }

                return false;
            }

            private XmlLightElement ProcessXPaths(XmlLightElement e)
            {
                object foundBy;
                if (_elements.TryGetValue(e, out foundBy))
                {
                    return GetReplacementNode(e, foundBy);
                }

                return e;
            }
        }
    }
}
