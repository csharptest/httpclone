#region Copyright 2010-2011 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using System.Linq;
using CSharpTest.Net.Commands;
using System.Collections.Generic;
using CSharpTest.Net.HttpClone.Commands;

namespace CSharpTest.Net.HttpClone
{
    static class Program
    {
        [STAThread]
        static int Main(string[] arguments)
        {
            LogConfig.Configure();
            try
            {
                using (CommandLine commands = new CommandLine())
                {
                    CommandInterpreter ci = new CommandInterpreter(DefaultCommands.None, commands);
                    if (arguments.Length > 0)
                    {
                        List<string> args = new List<string>(arguments);
                        int position = 0;
                        while (position < args.Count)
                        {
                            int next = args.IndexOf("&&", position);
                            if (next < 0) next = args.Count;

                            ci.Run(arguments.Skip(position).Take(next - position).ToArray());
                            position = next + 1;

                            if (ci.ErrorLevel != 0)
                                break;
                        }
                    }
                    else
                        ci.Run(Console.In);

                    Environment.ExitCode = ci.ErrorLevel;
                }
            }
            catch (ApplicationException ae)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(ae.Message);
                Environment.ExitCode = -1;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(e.ToString());
                Environment.ExitCode = -1;
            }

            return Environment.ExitCode;
        }
    }
}
