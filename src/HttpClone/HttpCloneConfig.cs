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
using System.Xml.Serialization;
using System.ComponentModel;
using System.Collections.Generic;

namespace CSharpTest.Net.HttpClone
{
    [XmlRoot("HttpCloneConfig")]
    public class HttpCloneConfig
    {
        [XmlElement("exclude")]
        public string[] ExcludedPaths;
        [XmlElement("include")]
        public string[] IncludedPaths;

        [XmlElement("bad-name-chars-expression"), DefaultValue(@"[^\w]+")]
        public string BadNameCharsExpression;

        [XmlElement("search")]
        public HttpCloneSearching Searching;

        [XmlElement("doc-type")]
        public HttpCloneDocType[] DocumentTypes;
    }

    public enum ContentFormat
    {
        [XmlEnum("unknown")] Unknown,
        [XmlEnum("xml")] Xml,
        [XmlEnum("html")] Html,
        [XmlEnum("text")] Text,
        [XmlEnum("binary")] Binary,
    };

    public class HttpCloneDocType : HttpCloneFileType
    {
        private HttpCloneDocumentTag[] _documentTags;

        [XmlAttribute("type")]
        public ContentFormat Type;

        [XmlAttribute("mime")]
        public override String MimeType
        {
            get { return base.MimeType ?? "application/binary"; }
            set
            {
                base.MimeType = value;
                if (Type == ContentFormat.Text && value == "text/html")
                    Type = ContentFormat.Html;
                else if (Type == ContentFormat.Text && value == "text/xml")
                    Type = ContentFormat.Xml;
            }
        }

        [XmlAttribute("relative-links"), DefaultValue(false)]
        public bool UsesRelativePaths;

        [XmlAttribute("text-links"), DefaultValue(false)]
        public bool ProcessPlainTextLinks;

        [XmlElement("title")]
        public HttpCloneMatch TitleExpression;

        [XmlElement("alias")]
        public HttpCloneFileType[] Aliases;

        [XmlElement("match")]
        public HttpCloneMatch[] Matches;

        [XmlElement("tag")]
        public HttpCloneDocumentTag[] DocumentTags
        {
            get { return _documentTags; }
            set 
            { 
                _documentTags = value;
                if (value != null && Type == ContentFormat.Text)
                    throw new InvalidOperationException("Please set the type attribute to Xml or Html for " + MimeType);
            }
        }

        [XmlElement("optimizing")]
        public HttpCloneOptimizations Optimizations;

        [XmlIgnore]
        public bool Indexed { get { return Type == ContentFormat.Html; } }
    }

    public class HttpCloneFileType
    {
        private string _mimeType;

        [XmlAttribute("ext")]
        public string FileExtension;

        [XmlAttribute("mime")]
        public virtual string MimeType
        {
            get { return _mimeType; }
            set { _mimeType = value; }
        }
    }

    public class HttpCloneOptimizations
    {
        private HttpCloneOptimizationItem _remove;
        private HttpCloneOptimizationReplace[] _replace;

        [XmlElement("remove")]
        public HttpCloneOptimizationItem Remove
        {
            get { return _remove ?? new HttpCloneOptimizationItem(); }
            set { _remove = value; }
        }

        [XmlElement("replace")]
        public HttpCloneOptimizationReplace[] Replace
        {
            get { return _replace; }
            set { _replace = value; }
        }

        public IEnumerable<HttpCloneOptimizationReplace> AllItems()
        {
            List<HttpCloneOptimizationReplace> items = new List<HttpCloneOptimizationReplace>();
            if (_remove != null)
            {
                HttpCloneOptimizationReplace remove = new HttpCloneOptimizationReplace();
                remove.ReplaceItem = _remove.ReplaceItem;
                items.Add(remove);
            }
            if (_replace != null)
                items.AddRange(_replace);
            return items;
        }
    }

    public class HttpCloneOptimizationReplace : HttpCloneOptimizationItem
    {
        [XmlAttribute("value"), DefaultValue("")]
        public string ReplacementValue;

        [XmlAttribute("expand"), DefaultValue(false)]
        public bool ExpandValue;
    }

    public class HttpCloneOptimizationItem
    {
        [XmlElement("tag", typeof(HttpCloneTag))]
        [XmlElement("match", typeof(HttpCloneMatch))]
        [XmlElement("xpath", typeof(HttpCloneXPath))]
        public object[] ReplaceItem;
    }

    public class HttpCloneXPath
    {
        [XmlAttribute("xpath")]
        public string XPath;
    }

    public class HttpCloneMatch
    {
        [XmlAttribute("expression")]
        public string Expression;

        [XmlAttribute("group_id"), DefaultValue(1)]
        public int GroupId;
    }

    public class HttpCloneTag
    {
        [XmlAttribute("ancestor")]
        public String Ancestor;

        [XmlAttribute("name")]
        public String TagName;

        [XmlAttribute("where")]
        public String Condition;
    }

    public class HttpCloneDocumentTag : HttpCloneTag
    {
        [XmlAttribute("mime")]
        public String ContentType;

        [XmlAttribute("follow")]
        public String Attribute;

        [XmlElement("attribute")]
        public HttpCloneTagAttribute[] Attributes;
    }

    public class HttpCloneTagAttribute
    {
        [XmlAttribute("name")]
        public String Name;

        [XmlAttribute("mime")]
        public String ContentType;
    }

    public class HttpCloneSearching
    {
        [XmlAttribute("template-uri")]
        public string TemplateUri;

        [XmlElement("xpath-base")]
        public HttpCloneXPath XPathBase;

        [XmlElement("form")]
        public HttpCloneXPath FormXPath;

        [XmlElement("title")]
        public HttpCloneXPathBlurb TitlePath;

        [XmlElement("blurb")]
        public HttpCloneXPathBlurb BlubXPath;

        [XmlElement("date")]
        public HttpCloneXPathDate DateXPath;

        [XmlElement("terms-xpath")]
        public HttpCloneXPathReplace TermsXPath;

        [XmlElement("results-xpath")]
        public HttpCloneXPathReplace ResultXPath;

        [XmlArray("index")]
        [XmlArrayItem("add")]
        public HttpCloneXPath[] Indexed;

        [XmlArray("exclude")]
        [XmlArrayItem("if")]
        public HttpCloneXPath[] Conditions;
    }

    public enum ReplaceOption
    {
        [XmlEnum("replace")]
        Replace,
        [XmlEnum("append")]
        Append,
    }

    public class HttpCloneXPathReplace : HttpCloneXPath
    {
        [XmlAttribute("replace")]
        public ReplaceOption ReplaceOption;
    }
    
    public class HttpCloneXPathBlurb : HttpCloneXPath
    {
        [XmlAttribute("max-length"), DefaultValue(512)]
        public int MaxLength;
    }

    public class HttpCloneXPathDate : HttpCloneXPath
    {
        [XmlAttribute("format")]
        public string DateFormat;
    }
}
