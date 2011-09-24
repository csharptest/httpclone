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
using System.IO;

namespace CSharpTest.Net.HttpClone.Commands
{
    /// <summary>
    /// This class implements the command-line interface.  
    /// </summary>
    /// <remarks>
    /// The actions are grouped and orgainized in the source files located in this directory.
    /// </remarks>
    public partial class CommandLine : IDisposable
    {
        string StoragePath(string site)
        {
            Uri url = new Uri(site, UriKind.Absolute);
            if(url.IsFile || url.IsUnc)
            {
                Check.Assert<DirectoryNotFoundException>(Directory.Exists(url.LocalPath));
                return url.LocalPath;
            }

            string store = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Settings.StorePath);
            store = Path.Combine(store, url.Host);
            if (!url.IsDefaultPort)
                store += "." + url.Port;
            return store;
        }

        void IDisposable.Dispose()
        { }
    }
}
