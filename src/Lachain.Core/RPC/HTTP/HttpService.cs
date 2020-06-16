using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using AustinHarris.JsonRpc;
using Lachain.Core.Blockchain.Genesis;
using Newtonsoft.Json.Linq;

namespace Lachain.Core.RPC.HTTP
{
    public class HttpService
    {
        public void Start(RpcConfig rpcConfig)
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    _Worker(rpcConfig);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }
            }, TaskCreationOptions.LongRunning);
        }

        private HttpListener? _httpListener;
        private string? _apiKey;
        
        private readonly List<string> _privateMethods = new List<string>
        {
            "validator_start",
            "validator_stop",
            "fe_sendTransaction",
        };

        public void Stop()
        {
            _httpListener?.Stop();
        }

        private void _Worker(RpcConfig rpcConfig)
        {
            if (!HttpListener.IsSupported)
                throw new Exception("Your platform doesn't support [HttpListener]");
            _httpListener = new HttpListener();
            foreach (var host in rpcConfig.Hosts ?? throw new InvalidOperationException())
                _httpListener.Prefixes.Add($"http://{host}:{rpcConfig.Port}/");
            _httpListener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
            _apiKey = rpcConfig.ApiKey ?? throw new InvalidOperationException();
            _httpListener.Start();
            while (_httpListener.IsListening)
            {
                try
                {
                    _Handle(_httpListener.GetContext());
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }
            }

            _httpListener.Stop();
        }

        private bool _Handle(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            /* check is request options pre-flight */
            if (request.HttpMethod == "OPTIONS")
            {
                if (request.Headers["Origin"] != null)
                    response.AddHeader("Access-Control-Allow-Origin", request.Headers["Origin"]);
                response.AddHeader("Access-Control-Allow-Headers", "*");
                response.AddHeader("Access-Control-Allow-Methods", "GET, POST");
                response.AddHeader("Access-Control-Max-Age", "1728000");
                response.Close();
                return true;
            }
            using var reader = new StreamReader(request.InputStream);
            var body = reader.ReadToEnd();
            if (request.Headers["Origin"] != null)
                response.AddHeader("Access-Control-Allow-Origin", request.Headers["Origin"]);
            response.Headers.Add("Access-Control-Allow-Methods", "POST, GET");
            var rpcResultHandler = new AsyncCallback(result =>
            {
                if (!(result is JsonRpcStateAsync jsonRpcStateAsync))
                    return;
                var output = Encoding.UTF8.GetBytes(jsonRpcStateAsync.Result);
                response.OutputStream.Write(output, 0, output.Length);
                response.OutputStream.Flush();
                response.Close();
            });
            var async = new JsonRpcStateAsync(rpcResultHandler, null)
            {
                JsonRpc = body
            };

            if (!_CheckAuth(JObject.Parse(body)))
            {
                var id = ulong.Parse(JObject.Parse(body)["id"]!.ToString());
                var error = new JObject
                {
                    ["code"] = -32600,
                    ["message"] = "Invalid API key",
                };
                var res = new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["error"] = error,
                    ["id"] = id
                };
                var output = Encoding.UTF8.GetBytes(res.ToString());
                response.OutputStream.Write(output, 0, output.Length);
                response.OutputStream.Flush();
                response.Close();
                return false;
            }

            JsonRpcProcessor.Process(async);
            return true;
        }

        private bool _CheckAuth(JObject body)
        {
            if (_privateMethods.Contains(body["method"]!.ToString()))
            {
                return !(body["key"] is null) && Equals(body["key"]!.ToString(), _apiKey);
            }

            return true;
        }
    }
}