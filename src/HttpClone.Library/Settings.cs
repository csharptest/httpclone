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
using CSharpTest.Net.Logging;

namespace CSharpTest.Net.HttpClone
{
    public static class Settings 
    {
        static readonly System.Collections.Specialized.NameValueCollection AppSettings = System.Configuration.ConfigurationManager.AppSettings;

        public static string StorePath { get { return Environment.ExpandEnvironmentVariables(AppSettings["StorePath"] ?? "Store"); } }
        public static string LogPath { get { return Environment.ExpandEnvironmentVariables(AppSettings["LogPath"] ?? "Log"); } }

        public static LogLevels LogLevel
        {
            get
            {
                return Enum.IsDefined(typeof(LogLevels), AppSettings["LogLevel"])
                    ? (LogLevels)Enum.Parse(typeof(LogLevels), AppSettings["LogLevel"])
                    : LogLevels.Verbose;
            }
        }

        public static string RegistryPath { get { return AppSettings["RegistryPath"] ?? @"SOFTWARE\CSharpTest.Net\HttpClone"; } }
    }
}
