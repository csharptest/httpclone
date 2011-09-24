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
using System.Net;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.ServiceModel.Description;
using CSharpTest.Net.HttpClone.Storage;

namespace CSharpTest.Net.HttpClone.Services
{
    [ServiceContract]
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class WcfHttpHost : IDisposable
    {
        private readonly WebServiceHost _host;
        private readonly SimpleHttpHandler _handler;
        private readonly Uri _uri;

        public WcfHttpHost(ContentStorage storage, int port)
        {
            _uri = new Uri(String.Format("http://localhost:{0}/", port));
            _handler = new SimpleHttpHandler(storage);
            
            _host = new WebServiceHost(this);
            ServiceEndpoint pages = _host.AddServiceEndpoint(GetType(), new WebHttpBinding(), _uri);
            pages.Behaviors.Add(new WebHttpBehavior());
            _host.Open();
        }

        public Uri Uri { get { return _uri; } }

        public void Dispose()
        {
            if (_host != null)
                _host.Close();
            if (_handler != null)
                _handler.Dispose();
        }

        private void NotFound(WcfResponse response)
        {
        }

        [OperationContract]
        [WebInvoke(UriTemplate = "/{*arguments}", Method = "HEAD", BodyStyle = WebMessageBodyStyle.Bare)]
        public Stream Head(string arguments)
        {
            WcfResponse response = new WcfResponse();
            if (!_handler.ProcessRequest(new WcfRequest(), response))
                NotFound(response);
            return response.ToStream();
        }

        [OperationContract]
        [WebInvoke(UriTemplate = "/{*arguments}", Method = "GET", BodyStyle = WebMessageBodyStyle.Bare)]
        public Stream Get(string arguments)
        {
            WcfResponse response = new WcfResponse();
            if (!_handler.ProcessRequest(new WcfRequest(), response))
                NotFound(response);
            return response.ToStream();
        }

        [OperationContract]
        [WebInvoke(UriTemplate = "/{*arguments}", Method = "POST", BodyStyle = WebMessageBodyStyle.Bare)]
        public Stream Post(string arguments, Stream body)
        {
            WcfResponse response = new WcfResponse();
            if (!_handler.ProcessRequest(new WcfRequest(body), response))
                NotFound(response);
            return response.ToStream();
        }

        private class WcfRequest : SimpleHttpRequest
        {
            public WcfRequest() : this(Stream.Null) { }
            public WcfRequest(Stream body)
                : base(
                WebOperationContext.Current.IncomingRequest.Method,
                WebOperationContext.Current.IncomingRequest.UriTemplateMatch.RequestUri.PathAndQuery, 
                WebOperationContext.Current.IncomingRequest.Headers, body)
            {
            }
        }

        private class WcfResponse : SimpleHttpResponse
        {
            private OutgoingWebResponseContext _response = WebOperationContext.Current.OutgoingResponse;
            private MemoryStream _content = new MemoryStream();

            protected override void SetStatusCode(int code, string desc)
            {
                _response.StatusCode = (HttpStatusCode)code;
                _response.StatusDescription = desc;
            }

            public override void AddHeader(string name, string value)
            {
                _response.Headers.Add(name, value);
            }

            public override void Write(byte[] bytes, int offset, int length)
            {
                _content.Write(bytes, offset, length);
            }

            public Stream ToStream()
            {
                Stream result = _content;
                _content = null;
                result.Position = 0;
                return result;
            }
        }
    }
}
