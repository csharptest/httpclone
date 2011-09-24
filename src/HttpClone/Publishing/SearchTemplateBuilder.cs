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
using CSharpTest.Net.HttpClone.Storage;
using CSharpTest.Net.Interfaces;
using CSharpTest.Net.Html;
using System.IO;
using CSharpTest.Net.HttpClone.Common;

namespace CSharpTest.Net.HttpClone.Publishing
{
    class SearchTemplateBuilder
    {
        private const string SearchNamespacePrefix = "search-template";
        private const string SearchNamespaceUri = "uri://csharptest.net/httpclone/search";
        private const string TemplatePath = SearchTemplate.TemplatePath;
        private const string SearchCssPath = "/search.css";

        private readonly Uri _baseUri;
        private readonly ContentStorage _data;
        private readonly HttpCloneConfig _config;

        public SearchTemplateBuilder(ContentStorage data, Uri baseUri)
        {
            _data = data;
            _baseUri = baseUri;
            _config = Config.ReadConfig(_baseUri, data.StorageDirectory);
        }

        public void RebuildTemplate() { UpdateTemplate(true); }
        public void UpdateTemplate() { UpdateTemplate(false); }

        private void UpdateTemplate(bool forced)
        {
            string tempPath = new Uri(_baseUri, _config.Searching.TemplateUri).NormalizedPathAndQuery();
            ContentRecord record;
            ContentRecord.Builder update;
            if (_data.TryGetValue(TemplatePath, out record))
                update = record.ToBuilder();
            else
                update = _data.New(TemplatePath, DateTime.Now);

            ContentRecord template;
            if (_data.TryGetValue(tempPath, out template))
            {
                if (template.HasContentStoreId && (forced || template.HashOriginal != update.HashOriginal))
                {
                    update.SetContentType(template.ContentType);
                    update.SetHashOriginal(template.HashOriginal);
                    update.SetLastCrawled(template.LastCrawled);
                    update.SetLastValid(template.LastValid);
                    update.SetDateModified(DateTime.Now);
                    update.SetHttpStatus(template.HttpStatus);
                    update.ClearContentRedirect();
                    if (template.HasContentRedirect) update.SetContentRedirect(update.ContentRedirect);

                    ContentParser parser = new ContentParser(_data, _baseUri);
                    parser.RelativeUri = true;
                    parser.RewriteUri += uri => new Uri(uri.OriginalString);
                    Uri templateUri = new Uri(_baseUri, SearchTemplate.SearchPath);
                    parser.MakeRelativeUri = (s, d) => templateUri.MakeRelativeUri(d);
                    byte[] mapped = parser.ProcessFile(template, _data.ReadContent(template, true));

                    string templateHtml = CreateTemplate(Encoding.UTF8.GetString(mapped));

                    using (ITransactable trans = _data.WriteContent(update, Encoding.UTF8.GetBytes(templateHtml)))
                    {
                        _data.AddOrUpdate(TemplatePath, update.Build());
                        trans.Commit();
                    }
                }
            }

            if (!_data.TryGetValue(SearchCssPath, out record))
            {
                ContentRecord cssRecord = _data.New(SearchCssPath, DateTime.Now)
                    .SetContentType("text/css")
                    .SetHttpStatus(200)
                    .Build();

                _data.Add(cssRecord.ContentUri, cssRecord);
                _data.WriteContent(cssRecord, Encoding.UTF8.GetBytes(Properties.Resources.search_css));
            }
        }

        private static void InsertTag(XmlLightElement start, string xpath, ReplaceOption where, string insertName)
        {
            XmlLightElement tag = new XmlLightElement(null, SearchNamespacePrefix + ":" + insertName);
            tag.Attributes["xmlns:" + SearchNamespacePrefix] = SearchNamespaceUri;
            tag.IsEmpty = false;

            XmlLightElement node = start.SelectRequiredNode(xpath);
            if (where == ReplaceOption.Append)
            {
                node.Children.Add(tag);
                tag.Parent = node;
            }
            else if (where == ReplaceOption.Replace)
            {
                int ordinal = node.Parent.Children.IndexOf(node);
                node.Parent.Children[ordinal] = tag;
                tag.Parent = node.Parent;

                node.Parent = null;
                tag.Children.Add(node);
                node.Parent = tag;
            }
            else
                throw new ArgumentOutOfRangeException();
        }

        private string CreateTemplate(string html)
        {
            HtmlLightDocument doc = new HtmlLightDocument(html);

            //Add css link:

            XmlLightElement cssLink = new XmlLightElement(doc.SelectRequiredNode("/html/head"), "link");
            cssLink.Attributes["type"] = "text/css";
            cssLink.Attributes["rel"] = "stylesheet";
            cssLink.Attributes["href"] = new Uri(_baseUri, "search.css").AbsoluteUri;

            XmlLightElement startFrom = doc.Root;
            if (_config.Searching.XPathBase != null)
                startFrom = startFrom.SelectRequiredNode(_config.Searching.XPathBase.XPath);

            if(_config.Searching.FormXPath != null)
            {
                XmlLightElement form = startFrom.SelectRequiredNode(_config.Searching.FormXPath.XPath);
                foreach (XmlLightElement textbox in form.Select(".//input[@type='text']"))
                    textbox.Attributes["value"] = String.Empty;
            }
            if(_config.Searching.TermsXPath != null)
            {
                InsertTag(startFrom, _config.Searching.TermsXPath.XPath, _config.Searching.TermsXPath.ReplaceOption, "search-terms");
            }

            if (_config.Searching.ResultXPath != null)
            {
                InsertTag(startFrom, _config.Searching.ResultXPath.XPath, _config.Searching.ResultXPath.ReplaceOption, "search-result");
            }

            using (StringWriter sw = new StringWriter())
            {
                doc.WriteUnformatted(sw);
                return sw.ToString();
            }
        }
    }
}
