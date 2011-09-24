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
using System.Threading;

namespace CSharpTest.Net.HttpClone.Publishing
{
    class TaskCounter
    {
        private long _counter;
        private readonly Action<Action> _next;
        private readonly ManualResetEvent _mre;

        public TaskCounter(Action<Action> next)
        {
            _mre = new ManualResetEvent(false);
            _next = next;
        }

        public long Count { get { return Interlocked.Read(ref _counter); } }

        public void Run(Action task)
        {
            Interlocked.Increment(ref _counter);
            _next(new Decrement(this, task).RunTask);
        }

        public void WaitOne()
        {
            long count = Count;
            if(count > 0)
            {
                _mre.Reset();
                while (Count == count && !_mre.WaitOne(1000, false))
                { }
            }
        }

        class Decrement
        {
            private readonly TaskCounter _counter;
            private readonly Action _task;

            public Decrement(TaskCounter counter, Action task)
            {
                _counter = counter;
                _task = task;
            }

            public void RunTask()
            {
                try
                {
                    _task();
                }
                finally
                {
                    Interlocked.Decrement(ref _counter._counter);
                    _counter._mre.Set();
                }
            }
        }
    }
}
