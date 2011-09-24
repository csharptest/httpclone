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
using System.Linq;

namespace CSharpTest.Net.HttpClone
{
    class ConfirmPrompt
    {
        bool _cancelled;

        public ConfirmPrompt()
        {
            Console.TreatControlCAsInput = false;
            Console.CancelKeyPress += ConsoleCancelKeyPress;
        }

        void ConsoleCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            _cancelled = true;
            e.Cancel = true;
        }

        public bool Continue(string question)
        {
            string response = Question(question, "y", "n", "c");
            if (response == "c")
                throw new OperationCanceledException();
            return response == "y";
        }

        public string Question(string question, params string[] answers)
        {
            if (_cancelled)
            {
                _cancelled = false;
                throw new OperationCanceledException();
            }

            while (true)
            {
                Console.Write("{0}? ({1}): ", question, String.Join("/", answers));

                string response;
                if (answers.Max(x => x.Length) == 1)
                {
                    response = Console.ReadKey().Key.ToString();
                    Console.WriteLine();
                }
                else
                    response = Console.ReadLine();

                foreach (string selected in answers.Where(x => StringComparer.OrdinalIgnoreCase.Equals(x, response)))
                    return selected;
            }
        }
    }
}