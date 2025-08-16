using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace ccxc_backend.Functions
{
    public static class HttpRequest
    {
        private static IHttpClientFactory _httpClientFactory;

        public static IHttpClientFactory HttpClientFactory
        {
            get
            {
                if (_httpClientFactory == null)
                {
                    ServiceCollection services = new ServiceCollection();
                    services.AddHttpClient("default").ConfigurePrimaryHttpMessageHandler((Func<IServiceProvider, HttpMessageHandler>)delegate
                    {
                        HttpClientHandler httpClientHandler = new HttpClientHandler();
                        httpClientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
                        return httpClientHandler;
                    });
                    _httpClientFactory = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
                }
                return _httpClientFactory;
            }
        }

        public static async Task<string> Get(string url, Dictionary<string, string> headers = null)
        {
            HttpRequestMessage httpRequestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(url)
            };

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    httpRequestMessage.Headers.Add(header.Key, header.Value);
                }
            }

            var client = HttpClientFactory.CreateClient("default");
            var response = await client.SendAsync(httpRequestMessage);
            return await response.Content.ReadAsStringAsync();
        }

        public static async Task<T> Get<T>(string url, Dictionary<string, string> urlQuerys, Dictionary<string, string> headers = null)
        {
            if (urlQuerys != null && urlQuerys.Count > 0)
            {
                var queryString = string.Join("&", urlQuerys.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
                url += (url.Contains('?') ? "&" : "?") + queryString;
            }
            HttpRequestMessage httpRequestMessage = new()
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(url)
            };
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    httpRequestMessage.Headers.Add(header.Key, header.Value);
                }
            }
            var client = HttpClientFactory.CreateClient("default");
            var response = await client.SendAsync(httpRequestMessage);
            var content = await response.Content.ReadAsStringAsync();
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(content);
        }

        public static async Task<string> Post(string url, string body, Dictionary<string, string> headers = null)
        {
            HttpRequestMessage httpRequestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(url),
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    httpRequestMessage.Headers.Add(header.Key, header.Value);
                }
            }
            var client = HttpClientFactory.CreateClient("default");
            var response = await client.SendAsync(httpRequestMessage);
            return await response.Content.ReadAsStringAsync();
        }

        public static async Task<string> PostForm(string uri, Dictionary<string, string> form, Dictionary<string, string> headers = null, int timeout = 10000)
        {
            HttpRequestMessage httpRequestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(uri),
                Content = new FormUrlEncodedContent(form)
            };
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    httpRequestMessage.Headers.Add(header.Key, header.Value);
                }
            }
            var client = HttpClientFactory.CreateClient("default");
            client.Timeout = TimeSpan.FromMilliseconds(timeout);
            var response = await client.SendAsync(httpRequestMessage);
            return await response.Content.ReadAsStringAsync();
        }
    }
}
