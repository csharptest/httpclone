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
using System.Text;
using System.Threading;
using CSharpTest.Net.Crypto;
using CSharpTest.Net.Logging;

namespace CSharpTest.Net.HttpClone
{
    public class LogConfig
    {
        private static LogConfig Instance = new LogConfig();
        private readonly Mutex _logFileLock;

        public static bool Configure() { return Instance != null; }

        private LogConfig()
        {
            string logPath = AppDomain.CurrentDomain.BaseDirectory;
            logPath = Path.Combine(logPath, Settings.LogPath);
            if(!Directory.Exists(logPath))
                Directory.CreateDirectory(logPath);

            string logpathinfo = Path.Combine(logPath, Path.GetFileNameWithoutExtension(Constants.ProcessFile));
            string filename = logpathinfo;
            for( int ix=0; ix < 50; ix++ )
            {
                filename = String.Format("{0}-{1}", logpathinfo, ix);

                string lockname = BitConverter.ToString(Hash.MD5(Encoding.UTF8.GetBytes(filename)).ToArray());
                bool aquired;
                _logFileLock = new Mutex(true, lockname, out aquired);

                if (!aquired)
                {
                    try { aquired = _logFileLock.WaitOne(0, false); }
                    catch (AbandonedMutexException) { aquired = true; }
                }

                if(aquired)
                    break;
            }

            Log.Config.LogFileMaxHistory = 10;
            Log.Config.LogFileMaxSize = 1024 * 1024 * 5;
            Log.Config.LogFile = filename + "-{0}.txt";
            Log.Config.Level = Settings.LogLevel;
            Log.Config.Options = LogOptions.LogNearestCaller | LogOptions.GZipLogFileOnRoll
#if DEBUG
                | LogOptions.LogAddFileInfo
#endif
;
            Log.Write("AppDomain '{0}' startup at {1}", Constants.AppDomainName, DateTime.Now);
            //Log.AppStart("AppDomain '{0}'", Constants.AppDomainName);
        }
    }
}
