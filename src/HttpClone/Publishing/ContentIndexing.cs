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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CSharpTest.Net.HttpClone.Storage;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Version = Lucene.Net.Util.Version;
using CSharpTest.Net.Html;
using System.Globalization;
using System.Xml;
using Lucene.Net.Analysis;

namespace CSharpTest.Net.HttpClone.Publishing
{
    class ContentIndexing : IDisposable
    {
        private static readonly Regex EndOfSetence = new Regex(@"\.\s", RegexOptions.Singleline);
        private static readonly Regex WhiteSpaces = new Regex(@"\s+", RegexOptions.Singleline);
        private static readonly Regex NonAlphaNum = new Regex(@"[^\w]+", RegexOptions.Singleline);
        private static readonly Regex DateTimeClean = new Regex(@"[0-9]th|1st|2nd|3rd");
        
        private readonly Uri _baseUri;
        private readonly MimeInfoMap _mimeInfo;
        private readonly ContentStorage _content;
        private readonly IndexWriter _writer;
        private readonly HttpCloneSearching _config;

        public ContentIndexing(string storagePath, string baseUri)
        {
            _baseUri = new Uri(baseUri, UriKind.Absolute);
            _config = Config.ReadConfig(_baseUri, storagePath).Searching;
            _mimeInfo = new MimeInfoMap(_baseUri, storagePath);
            _content = new ContentStorage(storagePath, false);

            string directory = _content.IndexDirectory;
            DirectoryInfo dirInfo = new DirectoryInfo(directory);
            if (dirInfo.Exists)
                dirInfo.Delete(true);
            _writer = new IndexWriter(FSDirectory.Open(dirInfo),
                                 new StandardAnalyzer(Version.LUCENE_29), true,
                                 IndexWriter.MaxFieldLength.LIMITED);

            BlurbLength = (uint)_config.BlubXPath.MaxLength;
        }

        public uint BlurbLength { get; set; }

        public void Dispose()
        {
            _writer.Optimize();
            _writer.Close();
            _content.Dispose();
        }

        public void BuildIndex()
        {
            if(_config == null)
                throw new InvalidOperationException("The <search> element is missing from the configuration.");

            foreach (KeyValuePair<string, ContentRecord> item in _content)
            {
                if (item.Value.HasContentStoreId == false)
                    continue;
                if (!_mimeInfo[item.Value.MimeType].Indexed || _mimeInfo[item.Value.MimeType].Type != ContentFormat.Html)
                    continue;

                string title = null, blurb = null, date = null;
                string content = Encoding.UTF8.GetString(_content.ReadContent(item.Value, true));
                HtmlLightDocument xdoc = new HtmlLightDocument(content);
                XmlLightElement found, selectFrom = _config.XPathBase == null ? xdoc.Root
                    : xdoc.SelectRequiredNode(_config.XPathBase.XPath);

                bool ignore = false;
                foreach(var xpath in _config.Conditions.SafeEnumeration())
                {
                    if(null != selectFrom.SelectSingleNode(xpath.XPath))
                    {
                        ignore = true;
                        break;
                    }
                }
                if (ignore)
                    continue;

                if (_config.TitlePath != null && selectFrom.TrySelectNode(_config.TitlePath.XPath, out found))
                    title = found.InnerText.Trim();
                else if (_config.TitlePath == null && false == _mimeInfo.TryGetTitle(item.Value.MimeType, content, out title))
                    title = null;
                if (String.IsNullOrEmpty(title))
                    continue;

                if (_config.BlubXPath != null)
                {
                    StringBuilder tmp = new StringBuilder();
                    foreach (XmlLightElement e in selectFrom.Select(_config.BlubXPath.XPath))
                        tmp.Append(e.InnerText);
                    if (tmp.Length == 0)
                        tmp.Append(selectFrom.SelectRequiredNode(_config.BlubXPath.XPath).InnerText);
                    blurb = tmp.ToString();
                }
                DateTime dtvalue = item.Value.DateCreated;
                if (_config.DateXPath != null && selectFrom.TrySelectNode(_config.DateXPath.XPath, out found))
                {
                    DateTime contentDate;
                    string dtText = found.InnerText.Trim();
                    dtText = DateTimeClean.Replace(dtText, m => m.Value.Substring(0, m.Length - 2));

                    if (!String.IsNullOrEmpty(_config.DateXPath.DateFormat))
                    {
                        if (DateTime.TryParseExact(dtText, _config.DateXPath.DateFormat, CultureInfo.InvariantCulture,
                                               DateTimeStyles.AllowWhiteSpaces, out contentDate))
                            dtvalue = contentDate;
                        else
                            throw new FormatException("Unable to parse date/time: " + dtText);
                    }
                    else if (DateTime.TryParse(dtText, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out contentDate))
                        dtvalue = contentDate;
                    else
                        throw new FormatException("Unable to parse date/time: " + dtText);
                }
                date = dtvalue.ToString("yyyy-MM-dd HH:mm:ss");

                StringWriter indexed = new StringWriter();
                indexed.WriteLine(title);
                foreach(var xpath in _config.Indexed.SafeEnumeration())
                {
                    foreach (var indexItem in selectFrom.Select(xpath.XPath))
                    {
                        string innerText = indexItem.InnerText;
                        indexed.WriteLine(innerText);
                        indexed.WriteLine(NonAlphaNum.Replace(innerText, " "));//again, removing all special characters.
                    }
                }

                if (String.IsNullOrEmpty(blurb))
                    blurb = indexed.ToString().Substring(title.Length).Trim();

                title = WhiteSpaces.Replace(TrimString(title, _config.TitlePath != null ? (uint)_config.TitlePath.MaxLength : BlurbLength), " ");
                blurb = WhiteSpaces.Replace(TrimString(blurb, _config.BlubXPath != null ? (uint)_config.BlubXPath.MaxLength : BlurbLength), " ");

                string text = indexed.ToString();

                using (TextReader rdr = new StringReader(text))
                    AddToIndex(item.Key, date, title, blurb, rdr);
            }
        }

        private void AddToIndex(string uri, string date, string title, string blurb, TextReader text)
        {
            Document doc = new Document();

            doc.Add(new Field("uri", uri, Field.Store.YES, Field.Index.NOT_ANALYZED));
            doc.Add(new Field("title", title, Field.Store.YES, Field.Index.NO));
            doc.Add(new Field("blurb", blurb, Field.Store.YES, Field.Index.NO));
            doc.Add(new Field("modified", date, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("contents", text, Field.TermVector.WITH_POSITIONS_OFFSETS));

            _writer.AddDocument(doc);
        }

        private string TrimString(string text, uint umax)
        {
            int max = (int)umax;
            if( text.Length > max )
            {
                text = text.Substring(0, max);
                foreach(int start in new int[] { max/2, max /3, max/4})
                {
                    Match m = EndOfSetence.Match(text, start);
                    if (m.Success)
                        return text.Substring(0, m.Index + 1);
                }
            }
            return text;
        }
    }
}
