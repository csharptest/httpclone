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

namespace CSharpTest.Net.HttpClone.Storage
{
    partial class ContentRecord
    {
        public bool IsMimeType(string mimeType)
        {
            return CleanMimeType(mimeType) == MimeType; 
        }

        public string MimeType
        {
            get { return CleanMimeType(ContentType); }
        }

        private static string CleanMimeType(string mimeType)
        {
            return !String.IsNullOrEmpty(mimeType)
                           ? mimeType.Split(';')[0].Trim().ToLower()
                           : "application/binary";
        }
    }
}
