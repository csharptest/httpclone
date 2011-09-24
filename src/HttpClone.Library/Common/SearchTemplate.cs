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
using System.Linq;
using System.Text;
using CSharpTest.Net.Html;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using CSharpTest.Net.HttpClone.HtmlExtensions;
using CSharpTest.Net.HttpClone.Storage;
using Lucene.Net.QueryParsers;

namespace CSharpTest.Net.HttpClone.Common
{
    public class SearchTemplate
    {
        public const string TemplatePath = "/search/";
        public const string SearchPath = "/search/";
        private static readonly Regex TemplatePattern = new Regex(@"<(?<closed>/?)search-template:(?<name>[\w\-]+)[^>]*?>", RegexOptions.Singleline);
        private static readonly Regex FormInputPattern = new Regex(@"<form[^>]+>.*?<input(?:[^>]+?(?:type=""text""|value=(?<value>"""")))+[^>]+?>.*?</form", RegexOptions.Singleline);

        private readonly ContentStorage _data;
        public SearchTemplate(ContentStorage data)
        {
            _data = data;
        }

        public string RenderResults(string searchTerms, int page)
        {
            string errorMessage = "";
            if (searchTerms.Length > 128)
            {
                errorMessage = "The query is too long.";
            }
            else
            {
                try
                {
                    return RenderResultsInternal(searchTerms, page);
                }
                catch (ParseException)
                {
                    errorMessage = String.Format("Unable to parse query '{0}', be sure to match quotes and parentheses.", searchTerms);
                }
            }

            var summary = new Dictionary<string, Action<TextWriter>>();
            summary.Add("search-terms", w => w.Write(HttpRequestUtil.HtmlEncode(errorMessage)));
            summary.Add("search-result", w => { });

            using (StringWriter sw = new StringWriter())
            {
                RenderTemplate(sw, searchTerms, summary);
                return sw.ToString();
            }
        }

        string RenderResultsInternal(string searchTerms, int page)
        {
            const int PageSize = 10;
            page = Math.Max(1, page);
            int total;
            var results = _data.Search(searchTerms, (page - 1) * PageSize, PageSize, false, out total);

            int maxPage = Math.Max(1, (total + PageSize - 1) / PageSize);
            if(page > maxPage)
            {
                page = maxPage;
                results = _data.Search(searchTerms, (page - 1)*PageSize, PageSize, false, out total);
            }

            string desc = String.Format(
                "Viewing page {0} of {1}, {2} total entries found for '{3}'.",
                page, Math.Max(1, (total + PageSize - 1) / PageSize), total, searchTerms);

            return RenderResults(desc, searchTerms, page, PageSize, total, results);
        }

        private string RenderResults(string description, string terms, int page, int pageSize, int total, IEnumerable<ContentSearchResult> results)
        {
            using (StringWriter wtr = new StringWriter())
            {
                var summary = new Dictionary<string, Action<TextWriter>>();
                summary.Add("search-terms", w => w.Write(HttpRequestUtil.HtmlEncode(description)));
                try
                {
                    if (total > 0)
                        summary.Add("search-result", w => WriteResultHtml(w, terms, page, pageSize, total, results));
                    else
                        summary.Add("search-result", w => WriteErrorHtml(w, "No results found."));

                    RenderTemplate(wtr, terms, summary);
                }
                catch
                {
                    wtr.GetStringBuilder().Length = 0;

                    summary.Remove("search-result");
                    RenderTemplate(wtr, terms, summary);
                }

                return wtr.ToString();
            }
        }

        private static XmlLightElement MakeElement(XmlLightElement parent, string tag, string cls, string text)
        {
            XmlLightElement e= new XmlLightElement(parent, tag);
            e.Attributes["class"] = cls;
            e.IsEmpty = false;
            if (text != null)
                new XmlLightElement(e, XmlLightElement.TEXT).Value = text;
            return e;
        }

        private void WriteErrorHtml(TextWriter wtr, string message)
        {
            XmlLightElement root = MakeElement(null, "div", "searchresults", null);
            MakeElement(root, "div", "searcherr", message);
            WriteXmlNode(wtr, root);
        }

        private void WriteResultHtml(TextWriter wtr, string terms, int page, int pageSize, int total, IEnumerable<ContentSearchResult> results)
        {
            XmlLightElement href, parent;
            XmlLightElement root = MakeElement(null, "div", "searchresults", null);

            foreach(ContentSearchResult result in results)
            {
                parent = MakeElement(root, "div", "searchitem", null);
                href = MakeElement(parent, "a", "searchentry", result.Title);
                href.Attributes["href"] = result.Uri;
                href.Attributes["title"] = result.Uri;

                MakeElement(parent, "div", "searchdesc", result.Blurb);
                parent = MakeElement(parent, "div", "searchinfo", null);

                MakeElement(parent, "div", "searchrank", "Rank: " + result.Ranking + "% \xa0 ");
                MakeElement(parent, "div", "searchsize", "Size: " + (result.Record.ContentLength / 1024.0).ToString("n2") + "KB \xa0 ");
                
                parent = MakeElement(parent, "div", "searchurl", "Url: ");
                href = MakeElement(parent, "a", "searchlink", result.Uri);
                href.Attributes["href"] = result.Uri;
                href.Attributes["title"] = result.Uri;
            }

            XmlLightElement nav = MakeElement(root, "div", "searchnavbar", null);
            if(total > 0)
            {
                int maxPage = Math.Max(1, (total + pageSize - 1) / pageSize);

                parent = nav;
                href = MakeElement(parent, "a", "searchlink" + (page == 1 ? " disabled" : ""), " << Prev ".ToNbsp());
                href.Attributes["href"] = SearchPath.AddQuery("page", Math.Max(1, page-1)).AddQuery("query", terms);
                href.Attributes["title"] = "Previous Page";
                href.Attributes["accesskey"] = "p";

                for (int i = page - 4; i <= page + 4; i++)
                {
                    string text = "   ";
                    bool enabled = (i >= 1 && i <= maxPage);
                    if (enabled)
                        text = String.Format(" {0} ", i);
                    string exclass = !enabled ? " disabled" : page == i ? " selected" : "";
                    href = MakeElement(parent, "a", "searchlink" + exclass, text.ToNbsp());
                    href.Attributes["href"] = !enabled ? "#" : SearchPath.AddQuery("page", Math.Min(maxPage, i)).AddQuery("query", terms);
                    if (!enabled)
                        href.Attributes["style"] = "visibility: hidden;";
                    else
                    {
                        href.Attributes["title"] = "Go to page " + text.Trim();
                        href.Attributes["accesskey"] = (i%10).ToString();
                    }
                }

                href = MakeElement(parent, "a", "searchlink" + (page == maxPage ? " disabled" : ""), " Next >> ".ToNbsp());
                href.Attributes["href"] = SearchPath.AddQuery("page", Math.Min(maxPage, page + 1)).AddQuery("query", terms);
                href.Attributes["title"] = "Next Page";
                href.Attributes["accesskey"] = "n";
            }

            WriteXmlNode(wtr, root);
        }

        private static void WriteXmlNode(TextWriter wtr, XmlLightElement root)
        {
            using (XmlWriter xwtr = XmlWriter.Create(wtr, 
                new XmlWriterSettings()
                  {
                      CheckCharacters = false,
                      CloseOutput = false,
                      ConformanceLevel = ConformanceLevel.Fragment,
                      OmitXmlDeclaration = true,
                      Indent = false
                  }))
            {
                root.WriteXml(xwtr);
                xwtr.Flush();
            }
        }

        private void RenderTemplate(TextWriter wtr, string terms, IDictionary<string, Action<TextWriter>> values)
        {
            string[] parts = GetTemplate();
            if (((parts.Length - 1) % 4) != 0)
                throw new ApplicationException("Invalid template format.");

            if (_valuePartIndex >= 0 && _valuePartOffset >= 0)
            {
                parts = (string[])parts.Clone();
                parts[_valuePartIndex] = parts[_valuePartIndex].Insert(_valuePartOffset, HttpRequestUtil.HtmlEncode(terms));
            }

            wtr.Write(parts[0]);
            for (int ix = 1; ix < parts.Length; ix += 4)
            {
                if (parts[ix + 2] != '/' + parts[ix])
                    throw new ApplicationException("Invalid template format.");

                Action<TextWriter> fn;
                if (values.TryGetValue(parts[ix], out fn))
                    fn(wtr);
                else
                    wtr.Write(parts[ix + 1]);
                
                wtr.Write(parts[ix + 3]);
            }
        }

        private int _valuePartIndex, _valuePartOffset;
        private string[] _parts;
        private string[] GetTemplate()
        {
            if (_parts != null)
                return _parts;

            ContentRecord templateRec;
            if (!_data.TryGetValue(TemplatePath, out templateRec) || !templateRec.HasContentStoreId)
                return _parts = new string[] { String.Empty };

            string text = Encoding.UTF8.GetString(_data.ReadContent(templateRec, true));
            
            List<string> parts = new List<string>();
            int offset = 0;

            foreach(Match m in TemplatePattern.Matches(text))
            {
                parts.Add(text.Substring(offset, m.Index - offset));
                parts.Add(m.Groups["closed"].Value + m.Groups["name"]);
                offset = m.Index + m.Length;
            }

            parts.Add(text.Substring(offset, text.Length - offset));

            _parts = parts.ToArray();

            _valuePartOffset = -1;
            MatchCollection[] found = new MatchCollection[_parts.Length];
            for (int i = 0; i < _parts.Length; i++)
            {
                found[i] = FormInputPattern.Matches(_parts[i]);
                if(found[i].Count == 1)
                    _valuePartIndex = i;
            }

            found = found.Where(mc => mc.Count > 0).ToArray();
            if (found.Length == 1 && found[0].Count == 1)
            {
                //we have a single match, good.
                Match m = found[0][0];
                Group grpValue = m.Groups["value"];
                if (grpValue.Success && grpValue.Captures.Count == 1)
                {
                    _valuePartOffset = grpValue.Index + 1;
                }
            }

            if (_valuePartOffset == -1)
                _valuePartIndex = -1;

            return _parts;
        }
    }
}
