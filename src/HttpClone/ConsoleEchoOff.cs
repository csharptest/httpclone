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
using System.Runtime.InteropServices;

namespace CSharpTest.Net.HttpClone
{
    /// <summary>
    /// Use in a using(new ConsoleEchoOff()) statement to temporarily disable the console local-echo
    /// so that a password can be read securely from the command-line.  This has the disadvantage that
    /// the back-space character will no longer work.
    /// </summary>
    internal sealed class ConsoleEchoOff : IDisposable
    {
        private readonly IntPtr _hIn;
        private readonly int _mode;
        private bool _restore;

        [DllImport("Kernel32.dll")]
        public static extern IntPtr GetStdHandle(int handleId);
        [DllImport("Kernel32.dll")]
        public static extern bool GetConsoleMode(IntPtr hConsoleHandle, out int lpMode);
        [DllImport("Kernel32.dll")]
        public static extern bool SetConsoleMode(IntPtr hConsoleHandle, int mode);

        public ConsoleEchoOff()
        {
            _mode = 0;
            _restore = false;
            _hIn = GetStdHandle(-10);
            if (_hIn != IntPtr.Zero)
            {
                if (GetConsoleMode(_hIn, out _mode))
                    _restore = SetConsoleMode(_hIn, _mode & ~0x0004);
            }
        }

        void IDisposable.Dispose()
        {
            if (_restore)
            {
                _restore = false;
                SetConsoleMode(_hIn, _mode);
            }
        }
    }
}
