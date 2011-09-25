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
using System.Text.RegularExpressions;
using CSharpTest.Net.Utils;
using CSharpTest.Net.HttpClone.Storage;
using CSharpTest.Net.Html;
using System.IO;
using System.Xml;

namespace CSharpTest.Net.HttpClone.Publishing
{
    public class ContentParser
    {
        private static readonly Regex IsHtml = new Regex(@"<html[\s>]", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private const string MimeTagDivide = ";tag=";
        private readonly ContentStorage _content;
        private readonly HttpCloneConfig _config;
        private readonly Dictionary<string, HttpCloneDocType> _documentTypes;
        private readonly Dictionary<string, List<HttpCloneDocumentTag>> _documentTags;
        private readonly Uri _baseUri;

        public ContentParser(ContentStorage content, Uri baseUri)
            : this(content, baseUri, baseUri)
        { }

        public ContentParser(ContentStorage content, Uri configUri, Uri baseUri)
        {
            _baseUri = baseUri;
            _config = Config.ReadConfig(configUri, content.StorageDirectory);
            _content = content;

            _documentTypes = new Dictionary<string, HttpCloneDocType>(StringComparer.OrdinalIgnoreCase);
            foreach (HttpCloneDocType dtype in _config.DocumentTypes)
            {
                _documentTypes.Add(dtype.MimeType, dtype);
                if(dtype.FileExtension != null)
                    _documentTypes.Add(dtype.FileExtension, dtype);

                foreach (var alias in dtype.Aliases.SafeEnumeration())
                {
                    _documentTypes.Add(alias.MimeType, dtype);
                    if (alias.FileExtension != null)
                        _documentTypes.Add(alias.FileExtension, dtype);
                }
            }
            _documentTags = new Dictionary<string, List<HttpCloneDocumentTag>>(StringComparer.OrdinalIgnoreCase);
            foreach (HttpCloneDocType dtype in _config.DocumentTypes)
            {
                if (dtype.DocumentTags == null)
                    continue;
                foreach (HttpCloneDocumentTag tag in dtype.DocumentTags)
                {
                    List<HttpCloneDocumentTag> tags;
                    string key = dtype.MimeType + MimeTagDivide + tag.TagName;
                    if (!_documentTags.TryGetValue(key, out tags))
                    {
                        _documentTags.Add(key, tags = new List<HttpCloneDocumentTag>());

                        foreach (var alias in dtype.Aliases.SafeEnumeration())
                            _documentTags.Add(alias.MimeType + MimeTagDivide + tag.TagName, tags);
                    }
                    tags.Add(tag);
                }
            }
        }

        public bool RewriteAll = false;
        public bool Reformat = false;
        public bool RelativeUri = false;
        public String IndentChars = String.Empty;

        public event Action<Uri> VisitUri;
        public event Converter<Uri, Uri> RewriteUri;
        private Uri OnRewriteUri(Uri uri) { if(VisitUri != null) VisitUri(uri); return InvokeList(RewriteUri, uri); }

        public event Converter<XmlLightElement, XmlLightElement> RewriteElement;
        private XmlLightElement OnRewriteElement(XmlLightElement e) { return InvokeList(RewriteElement, e); }

        public event Converter<XmlLightDocument, bool> RewriteXmlDocument;
        private bool OnRewriteDocument(XmlLightDocument doc) { return InvokeList(RewriteXmlDocument, doc); }

        public event Action<ContentRecord> ContextChanged;
        private void OnContextChanged(ContentRecord rec) { if (ContextChanged != null) ContextChanged(rec); }

        public event Converter<string, string> RewriteContent;
        private string OnRewriteContent(string content) { return InvokeList(RewriteContent, content); }

        public delegate Uri MakeRelativeUriMethod(Uri source, Uri target);
        public MakeRelativeUriMethod MakeRelativeUri = (s, d) => s.MakeRelativeUri(d);

        private bool OnSkipContent(ContentRecord record, out byte[] bytes)
        {
            if (RewriteAll && record.HasContentStoreId)
            {
                bytes = _content.ReadContent(record);
                return true;
            }
            bytes = null;
            return false;
        }

        private bool InvokeList<T>(Converter<T, bool> invoke, T instance) where T : class
        {
            bool modified = false;
            if (invoke != null)
            {
                foreach (Converter<T, bool> one in invoke.GetInvocationList())
                    modified = one(instance) | modified;
            }
            return modified;
        }

        private T InvokeList<T>(Converter<T, T> invoke, T instance) where T : class
        {
            if (invoke != null)
            {
                foreach (Converter<T, T> one in invoke.GetInvocationList())
                {
                    instance = one(instance);
                    if (instance == null)
                        break;
                }
            }
            return instance;
        }

        public void ProcessAll()
        { Process(f=>true, _content.WriteContent); }

        public void ProcessAll(Action<ContentRecord, byte[]> saveChanges)
        { Process(null, saveChanges); }

        public void ProcessType(string mimeType)
        { Process(r => r.IsMimeType(mimeType), _content.WriteContent); }

        public void Process(Predicate<ContentRecord> filter)
        { Process(filter, _content.WriteContent); }

        public void Process(Predicate<ContentRecord> filter, Action<ContentRecord, byte[]> saveChanges)
        {
            foreach(KeyValuePair<string, ContentRecord> record in _content)
            {
                try
                {
                    if (filter == null || filter(record.Value))
                        ProcessFile(record.Value, _content.ReadContent, saveChanges);
                }
                catch(Exception e)
                {
                    Log.Error(e);
                    Console.Error.WriteLine("Fatal error {0} processing {1}", e.Message, record.Key);
                }
            }
        }

        public byte[] ProcessFile(ContentRecord record, byte[] contentBytes)
        {
            byte[] bytes = contentBytes;
            ProcessFile(record, r => bytes, (r,b) => bytes = b);
            return bytes;
        }

        private void ProcessFile(ContentRecord record, Func<ContentRecord, byte[]> readBytes, Action<ContentRecord, byte[]> writeBytes)
        {
            string mime = record.MimeType;

            OnContextChanged(record);

            HttpCloneDocType type;
            if(_documentTypes.TryGetValue(mime, out type))
            {
                string content = Encoding.UTF8.GetString(readBytes(record));
                content = OnRewriteContent(content);
                string result = ProcessFileText(record.ContentUri, mime, RelativeUri && type.UsesRelativePaths, content);
                if (!ReferenceEquals(content, result) || RewriteAll)
                    writeBytes(record, Encoding.UTF8.GetBytes(result));
            }
            else 
            {
                byte[] bytes;
                if(OnSkipContent(record, out bytes))
                {
                    writeBytes(record, bytes);
                }
            }
        }

        private string ProcessFileText(string path, string mime, bool useRelativePaths, string content)
        {
            HttpCloneDocType type;
            if (_documentTypes.TryGetValue(mime, out type))
            {
                useRelativePaths &= type.UsesRelativePaths;
                if ((type.DocumentTags != null || RewriteElement != null)
                    && (type.Type == ContentFormat.Html || type.Type == ContentFormat.Xml))
                {
                    XmlLightDocument document = type.Type == ContentFormat.Html
                                                    ? new HtmlLightDocument(content)
                                                    : new XmlLightDocument(content);

                    bool modified = OnRewriteDocument(document);
                    modified = Visit(path, mime, document.Children, useRelativePaths, type) | modified;
                    if (modified || Reformat)
                        return RewriteDocument(document);
                }

                content = ProcessFileText(path, useRelativePaths, content, type);
            }
            return content;
        }

        private string ProcessFileText(string path, bool useRelativePaths, string content, HttpCloneDocType docType)
        {
            if (docType.Type == ContentFormat.Text && docType.ProcessPlainTextLinks)
            {
                content = ProcessTextMatch(path, useRelativePaths, content, RegexPatterns.HttpUrl, 0);
            }

            foreach (HttpCloneMatch exp in docType.Matches.SafeEnumeration())
                content = ProcessTextMatch(path, useRelativePaths, content, new Regex(exp.Expression), exp.GroupId);

            return content;
        }

        private string ProcessTextMatch(string path, bool useRelativePaths, string content, Regex regexp, int groupId)
        {
            Uri current = new Uri(_baseUri, path);
            string newcontent = regexp.Replace(content,
                match =>
                    {
                        Uri uri;
                        if (match.Success && match.Groups[groupId].Success
                            && Uri.TryCreate(current, match.Groups[groupId].Value, out uri))
                        {
                            Uri rewrite = OnRewriteUri(uri);
                            if (!ReferenceEquals(rewrite, uri))
                            {
                                string rewriteLink = rewrite.AbsoluteUri;
                                if (useRelativePaths)
                                {
                                    Uri cur = new Uri(OnRewriteUri(current), "./");
                                    rewriteLink = MakeRelativeUri(cur, rewrite).OriginalString;
                                    if (String.IsNullOrEmpty(rewriteLink) || rewriteLink[0] == '?')
                                    {
                                        rewriteLink = "./" + rewriteLink;
                                    }
                                }

                                string prefix = match.Value.Substring(0,
                                    match.Groups[groupId].Index - match.Index
                                    );
                                string suffix = match.Value.Substring(
                                    match.Groups[groupId].Index
                                    + match.Groups[groupId].Length
                                    - match.Index);
                                return prefix + rewriteLink + suffix;
                            }
                        }
                        return match.Value;
                    }
                );

            if (newcontent != content)
                return newcontent;
            return content;
        }

        private string RewriteDocument(XmlLightDocument document)
        {
            using (StringWriter sw = new StringWriter())
            {
                if (Reformat)
                {
                    XmlWriterSettings settings = new XmlWriterSettings();
                    settings.OmitXmlDeclaration = true;
                    settings.Indent = IndentChars.Length > 0;
                    settings.IndentChars = IndentChars;
                    settings.NewLineChars = settings.Indent ? Environment.NewLine : "";
                    settings.NewLineHandling = NewLineHandling.None;
                    settings.Encoding = Encoding.UTF8;
                    settings.CloseOutput = false;
                    settings.CheckCharacters = false;

                    using (XmlWriter xw = XmlWriter.Create(sw, settings))
                        document.WriteXml(xw);
                }
                else
                {
                    document.WriteUnformatted(sw);
                }
                return sw.ToString().Trim();
            }
        }

        private bool Visit(string path, string mime, List<XmlLightElement> children, bool useRelativePaths, HttpCloneDocType docType)
        {
            bool modified = false;
            for(int i=0; i < children.Count; i++)
            {
                XmlLightElement child = OnRewriteElement(children[i]);
                if (child == null)
                {
                    children[i].Parent = null;
                    children.RemoveAt(i);
                    i--;
                    modified = true;
                    continue;
                }
                if (!ReferenceEquals(child, children[i]))
                {
                    child.Parent = children[i].Parent;
                    children[i].Parent = null;
                    children[i] = child;
                    modified = true;
                }

                List<HttpCloneDocumentTag> tags;
                if (_documentTags.TryGetValue(mime + MimeTagDivide + child.TagName, out tags))
                    modified = ProcessTagUri(path, child, tags, useRelativePaths, docType) | modified;

                modified = Visit(path, mime, child.Children, useRelativePaths, docType) | modified;

                if (docType.ProcessPlainTextLinks && child.IsText)
                {
                    string content = child.OriginalTag;
                    string newcontent = ProcessTextMatch(path, false, content, RegexPatterns.HttpUrl, 0);
                    if (!ReferenceEquals(content, newcontent))
                    {
                        child.OriginalTag = newcontent;
                        modified = true;
                    }
                }
            }
            return modified;
        }

        private bool ProcessTagUri(string path, XmlLightElement child, IEnumerable<HttpCloneDocumentTag> tags, bool useRelativePaths, HttpCloneDocType docType)
        {
            Uri current = new Uri(_baseUri, path);
            bool modified = false;
            string value;

            foreach (HttpCloneDocumentTag tag in tags)
            {
                if (!IsTagMatch(child, tag))
                    continue;

                if (!String.IsNullOrEmpty(tag.ContentType))
                {
                    foreach(XmlLightElement eText in child.FindElement(x => x.TagName == XmlLightElement.TEXT || x.TagName == XmlLightElement.CDATA))
                    {
                        value = eText.Value;

                        bool isHtmlFragment = tag.ContentType == "text/html" && IsHtml.IsMatch(value) == false;
                        if (isHtmlFragment)
                            value = "<html><body>" + value + "</body></html>";

                        string newcontent = ProcessFileText(path, tag.ContentType, useRelativePaths, value);
                        if (newcontent != value)
                        {
                            if (isHtmlFragment)
                            {
                                int ixStart = newcontent.IndexOf("<body>");
                                int ixEnd = newcontent.IndexOf("</body>");
                                if (ixStart < 0 || ixEnd < 0)
                                    throw new ApplicationException("Unable to obtain html/body content.");
                                ixStart += "<body>".Length;
                                newcontent = newcontent.Substring(ixStart, ixEnd - ixStart);
                            }

                            modified = true;
                            eText.Value = newcontent;
                        }
                    }
                }

                if (!String.IsNullOrEmpty(tag.Attribute) && child.Attributes.ContainsKey(tag.Attribute))
                {
                    value = child.Attributes[tag.Attribute];
                    Uri uri;
                    if (!value.StartsWith("#") && Uri.TryCreate(current, value, out uri))
                    {
                        Uri rewrite = OnRewriteUri(uri);
                        if (!ReferenceEquals(rewrite, uri) || useRelativePaths)
                        {
                            string relPath = rewrite.AbsoluteUri;
                            if (useRelativePaths)
                            {
                                Uri cur = new Uri(OnRewriteUri(current), "./");
                                relPath = MakeRelativeUri(cur, rewrite).OriginalString;
                                if (String.IsNullOrEmpty(relPath) || relPath[0] == '?')
                                {
                                    relPath = "./" + relPath;
                                }
                            }

                            child.Attributes[tag.Attribute] = relPath;
                            modified = true;
                        }
                    }
                }

                foreach (HttpCloneTagAttribute atag in tag.Attributes.SafeEnumeration())
                {
                    if (!String.IsNullOrEmpty(atag.Name) && !String.IsNullOrEmpty(atag.ContentType)
                        && child.Attributes.ContainsKey(atag.Name))
                    {
                        string content = child.Attributes[atag.Name];
                        string replace = ProcessFileText(path, useRelativePaths, content, _documentTypes[atag.ContentType]);
                        if (!ReferenceEquals(replace, content))
                        {
                            child.Attributes[atag.Name] = replace;
                            modified = true;
                        }
                    }
                }
            }
            return modified;
        }

        public bool IsTagMatch(XmlLightElement e, HttpCloneTag tag)
        {
            if(!StringComparer.OrdinalIgnoreCase.Equals(e.TagName, tag.TagName))
                return false;

            if (!String.IsNullOrEmpty(tag.Ancestor))
            {
                XmlLightElement ancestor = e;
                while ((ancestor = ancestor.Parent) != null)
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(ancestor.TagName, tag.Ancestor))
                        break;
                }
                if (ancestor == null)
                    return false;
            }

            if (!String.IsNullOrEmpty(tag.Condition))
            {
                string value;
                string[] test = tag.Condition.Split('=', '|');
                bool inverse = test[0].EndsWith("!");
                if(inverse)
                    test[0] = test[0].TrimEnd('!');

                if (!e.Attributes.TryGetValue(test[0], out value) ||
                    !test.Skip(1).Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    if (inverse) return true;
                    return false;
                }
                else if (inverse)
                    return false;
            }

            return true;
        }
    }
}
