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
using CSharpTest.Net.Commands;
using CSharpTest.Net.HttpClone.Common;

namespace CSharpTest.Net.HttpClone.Commands
{
    partial class CommandLine
    {
        public void PingbackReplay(string site, string pullFrom)
        {
            PingbackClient client = new PingbackClient(site);
            client.LogError += Console.Error.WriteLine;
            client.LogInfo += Console.Out.WriteLine;

            client.Playback(
                new Uri(new Uri(pullFrom, UriKind.Absolute), "/api/pingback"), 
                Path.Combine(StoragePath(site), "playback.bin"));
        }

        public void Pingback(string source, string target)
        {
            PingbackClient client = new PingbackClient(target);
            client.LogError += Console.Error.WriteLine;
            client.LogInfo += Console.Out.WriteLine;

            int code;
            string message;
            if (client.SendPingback(source, out code, out message))
            {
                Console.WriteLine("Success: {0}", message);
            }
            else
            {
                Console.Error.WriteLine("FAILURE({0}): {1}", code, message);
            }
        }
    }
}
