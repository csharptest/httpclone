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
using CSharpTest.Net.Serialization;
using Google.ProtocolBuffers;

namespace CSharpTest.Net.HttpClone.Storage
{
    class ProtoSerializer<TM, TB> : ISerializer<TM>
        where TM : IMessageLite<TM, TB>
        where TB : IBuilderLite<TM, TB>, new()
    {
        TM ISerializer<TM>.ReadFrom(System.IO.Stream stream)
        {
            return new TB().MergeDelimitedFrom(stream).Build();
        }

        void ISerializer<TM>.WriteTo(TM value, System.IO.Stream stream)
        {
            value.WriteDelimitedTo(stream);
        }
    }
}
