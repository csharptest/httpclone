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
using System.Web;
using CSharpTest.Net.HttpClone.Services;

namespace CSharpTest.Net.HttpClone
{
    public class IISHandler : IHttpHandler, IHttpModule, IDisposable
    {
        private readonly EventHandler _beginRequestHandler;
        private readonly EventHandler _endRequestHandler;
        private readonly EventHandler _preSendRequestHeadersHandler;
        private readonly EventHandler _preSendRequestContentHandler;
        private readonly SimpleHttpHandler _handler;

        public IISHandler()
        {
            _beginRequestHandler = BeginRequest;
            _endRequestHandler = EndRequest;
            _preSendRequestHeadersHandler = PreSendRequestHeaders;
            _preSendRequestContentHandler = PreSendRequestContent;
            _handler = new SimpleHttpHandler();
        }

        void IDisposable.Dispose() { _handler.Dispose(); }
        void IHttpModule.Dispose() { _handler.Dispose(); }
        bool IHttpHandler.IsReusable { get { return true; } }

        void IHttpHandler.ProcessRequest(HttpContext context)
        {
            if(!ProcessRequest(context))
            {
                context.Response.StatusCode = 404;
                context.Response.StatusDescription = "Page Not Found";
                context.Response.Write("404 - Page Not Found");
            }
        }

        public bool ProcessRequest(HttpContext context)
        {
            SimpleHttpRequest request = new SimpleHttpRequest(
                context.Request.HttpMethod, context.Request.RawUrl, context.Request.Headers, context.Request.InputStream);
            IISResponse response = new IISResponse(context.Response);

            return _handler.ProcessRequest(request, response);
        }

        #region IISResponse
        private class IISResponse : SimpleHttpResponse
        {
            private readonly HttpResponse _response;

            public IISResponse(HttpResponse response)
            {
                _response = response;
            }

            protected override void SetStatusCode(int code, string desc)
            {
                _response.StatusCode = code;
                _response.StatusDescription = desc;
            }

            public override void AddHeader(string name, string value)
            {
                _response.AddHeader(name, value);
            }

            public override void Write(byte[] bytes, int offset, int length)
            {
                _response.OutputStream.Write(bytes, offset, length);
            }

            public override void TransmitFile(string contentFile, long offset, long length)
            {
                _response.TransmitFile(contentFile, offset, length);
            }
        }
        #endregion

        void IHttpModule.Init(HttpApplication context)
        {
            context.BeginRequest += _beginRequestHandler;
            context.PreSendRequestHeaders += _preSendRequestHeadersHandler;
            context.PreSendRequestContent += _preSendRequestContentHandler;
            context.EndRequest += _endRequestHandler;
        }

        void BeginRequest(object sender, EventArgs e)
        {
            HttpApplication application = (HttpApplication)sender;
            application.Context.Trace.Write("Request processing started.");

            if (System.IO.File.Exists(application.Context.Request.PhysicalPath))
                return;
            if (StringComparer.OrdinalIgnoreCase.Equals(application.Context.Request.Path, "/trace.axd"))
                return;

            if (ProcessRequest(application.Context))
                application.CompleteRequest();
        }

        void PreSendRequestHeaders(object sender, EventArgs e)
        {
            HttpApplication application = (HttpApplication)sender;
            if(HttpRuntime.UsingIntegratedPipeline)
            {
                application.Response.Headers.Remove("Server");
                application.Response.Headers.Remove("Expires");
                application.Response.Headers.Remove("X-Powered-By");
                application.Response.Headers.Remove("X-AspNet-Version");
                application.Response.Headers.Remove("Cache-Control");
            }
            application.Context.Trace.Write("Writing response headers.");

            //By default all content is cached.  This will undo that for only the places we desire:
            bool nocache = false;
            if (StringComparer.OrdinalIgnoreCase.Equals(application.Context.Request.Path, "/trace.axd"))
                nocache = true;

            if (nocache)
            {
                application.Response.AddHeader("Cache-Control", "no-cache, max-age=0, private");
                application.Response.AddHeader("Expires", "-3600");
            }
            else
            {
                application.Response.AddHeader("Cache-Control", "max-age=3600, public");
            }
        }

        void PreSendRequestContent(object sender, EventArgs e)
        {
            HttpApplication application = (HttpApplication)sender;
            application.Context.Trace.Write("Writing response content.");
        }

        void EndRequest(object sender, EventArgs e)
        {
            HttpApplication application = (HttpApplication)sender;
            application.Context.Trace.Write("Request processing complete.");
        }
    }
}
