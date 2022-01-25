/*
MIT License

Copyright (c) 2021 Tomas Weinfurt

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

//#define DIAG

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace NtlmHttp
{
    public class NtlmHttpMessageHandler : DelegatingHandler
    {
        private readonly bool _disposeHandler;
        public NetworkCredential NetworkCredential { get; set; }

        public NtlmHttpMessageHandler() : this(CreateInnerHandler?.Invoke() ?? new HttpClientHandler(), true)
        {
        }

        public static Func<HttpMessageHandler> CreateInnerHandler { get; set; }

        public NtlmHttpMessageHandler(HttpMessageHandler innerHandler, bool disposeHandler = true) : base(innerHandler)
        {
            _disposeHandler = disposeHandler;
        }
        protected override void Dispose(bool disposing)
        {
            if (_disposeHandler)
                InnerHandler.Dispose();
            base.Dispose(disposing);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var result = await base.SendAsync(request, cancellationToken);

            if (result.StatusCode == HttpStatusCode.Unauthorized)
            {
                bool canDoNtlm = false;
                bool canDoNegotiate = false;

                foreach (AuthenticationHeaderValue header in result.Headers.WwwAuthenticate)
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(header.Scheme, "NTLM"))
                    {
                        canDoNtlm = true;
                    }
                    else if (StringComparer.OrdinalIgnoreCase.Equals(header.Scheme, "Negotiate"))
                    {
                        canDoNegotiate = true;
                    }
#if DIAG
                    else
                    {
                        Debug.WriteLine($"{request.RequestUri} offers {header.Scheme} authentication");
                    }
#endif
                }

#if DIAG
                Debug.WriteLine("{0} {1} do NTLM authentication", request.RequestUri, canDoNtlm ? "can" : "cannot");
                Debug.WriteLine("{0} {1} do Negotiate authentication", request.RequestUri, canDoNegotiate ? "can" : "cannot");
#endif

                if (canDoNtlm)
                {
                    try
                    {
                        result = await SendAuthenticated(request, cancellationToken, true);

                        if (result.StatusCode != HttpStatusCode.Unauthorized)
                        {
                            return result;
                        }
                        // Else maybe try canDoNegotiate
                    }
                    catch (Exception ex)
                    {
#if DIAG
                        Debug.WriteLine("NTLM Authentication failed");
                        Debug.WriteLine(ex);
#endif
                    }
                }

                if (canDoNegotiate)
                {
                    try
                    {
                        result = await SendAuthenticated(request, cancellationToken, false);
                    }
                    catch (Exception ex)
                    {
#if DIAG
                        Debug.WriteLine("Negotiate Authentication failed");
                        Debug.WriteLine(ex);
#endif
                    }
                }
            }
            else
            {
#if DIAG
                Debug.WriteLine($"{request.RequestUri} did not ask for authentication.");
                Debug.WriteLine(result);
#endif
            }

            return result;
        }

        private async Task<HttpResponseMessage> SendAuthenticated(HttpRequestMessage request, CancellationToken cancellationToken, bool useNtlm = true)
        {
            // We would rather not create a new HttpClient for this of course
            using var client = new HttpClient(InnerHandler, false);

            request = await request.CloneAsync();
            var ntlm = new Ntlm(NetworkCredential);

            request.Headers.Authorization = CreateAuthenticationHeaderValue(ntlm.CreateNegotiateMessage(spnego: !useNtlm));

#if DIAG
            Debug.WriteLine(request);
#endif

            // var response = await base.SendAsync(request, cancellationToken);     TODO we would like to do this but doesn't work ?!?!?
            var response = await client.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                foreach (AuthenticationHeaderValue header in response.Headers.WwwAuthenticate)
                {
                    string blob = ntlm.ProcessChallenge(header);
                    if (!string.IsNullOrEmpty(blob))
                    {
                        request = await request.CloneAsync();
                        request.Headers.Authorization = CreateAuthenticationHeaderValue(blob);
#if DIAG
                        Debug.WriteLine(request);
#endif
                        // response = await base.SendAsync(request, cancellationToken); // TODO we would like to do this
                        response = await client.SendAsync(request, cancellationToken);
                    }
                }
            }

#if DIAG
            Debug.WriteLine(response);
#endif
            return response;
        }

        private AuthenticationHeaderValue CreateAuthenticationHeaderValue(string authorizationValue)
        {
            var values = authorizationValue.Split(' ');

            return new AuthenticationHeaderValue(values[0], values[1]);
        }
    }
}