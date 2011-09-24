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
using System.Collections.Generic;
using HttpStatusCode = System.Net.HttpStatusCode;
using CSharpTest.Net.HttpClone.Storage;

namespace CSharpTest.Net.HttpClone.Services
{
    class SimpleHttpHandler : IDisposable
    {
        private readonly ContentState _content;
        private readonly Dictionary<string, IRequestHandler[]> _handlers;

        public SimpleHttpHandler()
            : this(new ContentState())
        {
        }

        public SimpleHttpHandler(ContentStorage storage)
            : this(new ContentState(storage))
        {
        }

        private SimpleHttpHandler(ContentState state)
        {
            _content = state;
            _handlers = new Dictionary<string, IRequestHandler[]>(StringComparer.OrdinalIgnoreCase);

            ContentRequestHandler content = new ContentRequestHandler(_content);
            SearchRequestHandler search = new SearchRequestHandler(_content);
            PingbackHandler pingback = new PingbackHandler(_content);
            PublishRequestHandler publish = new PublishRequestHandler(_content);

            _handlers.Add("HEAD", new IRequestHandler[] 
            {
                content,
                pingback,
            });
            _handlers.Add("GET", new IRequestHandler[] 
            {
                search, 
                content,
                pingback,
            });
            _handlers.Add("POST", new IRequestHandler[] 
            { 
                publish,
                pingback,
            });
        }

        public void Dispose()
        {
            _content.Dispose();
        }

        public bool ProcessRequest(SimpleHttpRequest request, SimpleHttpResponse response)
        {
            LogConfig.Configure();
            try
            {
                return PerformRequest(request, response);
            }
            catch (CorruptApplicationDomainException ae)
            {
                try
                {
                    Log.Error(ae);
                }
                finally { Environment.Exit(1); }
                throw;
            }
            catch (Exception error)
            {
                Log.Error(error);
                throw;
            }
        }

        bool PerformRequest(SimpleHttpRequest request, SimpleHttpResponse response)
        {
            IRequestHandler[] handlers;

            if (!_handlers.TryGetValue(request.HttpMethod, out handlers))
                handlers = new[] { HttpErrorHandler.MethodNotAllowed };

            using (Log.Start("{0} {1}", request.HttpMethod, request.RawUrl))
            using (_content.BeginRequest())
            {
                IRequestHandler handler = 
                    handlers.FirstOrDefault(h => h.IsMatch(request.HttpMethod, request.RawUrl));

                if (handler == null)
                    return false;

                return PerformRequest(request, response, handler);
            }
        }

        private bool PerformRequest(SimpleHttpRequest request, SimpleHttpResponse response, IRequestHandler handler)
        {
            using (IContentResponse page = handler.GetResponse(
                request.HttpMethod, request.RawUrl, request.Headers, request.InputStream))
            {
                response.StatusCode = page.Status;

                if (page.Status == HttpStatusCode.Redirect || page.Status == HttpStatusCode.Moved ||
                    page.Status == HttpStatusCode.SeeOther)
                {
                    response.RedirectLocation = page.Redirect;
                }

                if (!String.IsNullOrEmpty(page.ETag) && page.LastModified > new DateTime(1900, 1, 1))
                {
                    response.LastModified = page.LastModified;
                    response.ETag = page.ETag;

                    if ((!String.IsNullOrEmpty(request.Headers["If-None-Match"]) &&
                        request.Headers["If-None-Match"].Trim('"') == page.ETag)
                        ||
                        (!String.IsNullOrEmpty(request.Headers["If-Modified-Since"]) &&
                        request.Headers["If-Modified-Since"] == page.LastModified.ToString("r")))
                    {
                        response.StatusCode = HttpStatusCode.NotModified;
                        return true;
                    }
                }
                if (page.Status == HttpStatusCode.OK && (request.HttpMethod == "GET" || request.HttpMethod == "HEAD"))
                {
                    string pb = String.Format("http://{0}{1}", request.Headers["Host"], PingbackHandler.PingbackUrl);
                    response.AddHeader("X-Pingback", pb);
                }

                if (page.ContentLength <= 0)
                {
                    response.ContentLength = 0;
                }
                else
                {
                    response.ContentType = page.ContentType;
                    bool decompress = page.Compressed;

                    if (decompress &&
                        (request.Headers["Accept-Encoding"] ?? "").IndexOf("gzip", StringComparison.OrdinalIgnoreCase) >=
                        0)
                    {
                        decompress = false;
                        response.ContentEncoding = "gzip";
                    }
                    else if (decompress &&
                             (request.Headers["TE"] ?? "").IndexOf("gzip", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        decompress = false;
                        response.TransferEncoding = "gzip";
                    }

                    int contentLength = (page.Compressed && !decompress) ? page.CompressedLength : page.ContentLength;
                    response.ContentLength = contentLength;

                    if (request.HttpMethod == "HEAD")
                    {
                        //no-content
                    }
                    else if (page.Compressed && decompress)
                    {
                        response.Write(page.ContentBytes, 0, contentLength);
                    }
                    else if (page.HasFile)
                    {
                        response.TransmitFile(page.ContentFile, 0, contentLength);
                    }
                    else if (page.Compressed)
                    {
                        response.Write(page.CompressedBytes, 0, contentLength);
                    }
                    else
                    {
                        response.Write(page.ContentBytes, 0, contentLength);
                    }
                }
                return true;
            }
        }
    }
}
