﻿#region Copyright 2011 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using CSharpTest.Net.Utils;
using System.IO;
using System.Xml;

namespace CSharpTest.Net.HttpClone
{
    class Config : XmlConfiguration<HttpCloneConfig>
    {
        public const string SCHEMA_NAME = "HttpCloneConfig.xsd";
        public Config() : base(SCHEMA_NAME) { }

        [Obsolete("Do not use.", true)]
        public static new HttpCloneConfig ReadConfig(string section)
        { throw new NotSupportedException(); }
        
        public static HttpCloneConfig ReadConfig(Uri site, string directory)
        {
            if (Directory.Exists(directory))
            {
                string test = Path.Combine(directory, "HttpClone.config");
                if(File.Exists(test))
                {
                    using (XmlReader rdr = new XmlTextReader(test))
                        return ReadXml(SCHEMA_NAME, rdr);
                }
            }
            return XmlConfiguration<HttpCloneConfig>.ReadConfig("HttpCloneConfig");
        }
    }
}
