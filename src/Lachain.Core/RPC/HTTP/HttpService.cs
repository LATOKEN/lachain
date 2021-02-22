using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using AustinHarris.JsonRpc;
using Newtonsoft.Json.Linq;
using Lachain.Logger;

namespace Lachain.Core.RPC.HTTP
{
    public class HttpService
    {
        private static readonly ILogger<HttpService> Logger =
            LoggerFactory.GetLoggerForClass<HttpService>();
        
        private HttpListener? _httpListener;
        private Thread _listenerLoop;
        private Thread[] _requestProcessors;
        private BlockingCollection<HttpListenerContext> _messages;
        private string? _apiKey;
        private readonly List<string> _privateMethods = new List<string>
        {
            "validator_start",
            "validator_stop",
            "fe_sendTransaction",
        };

        public void Start(RpcConfig rpcConfig)
        {
            Logger.LogTrace($"Start");
            if (!HttpListener.IsSupported)
                throw new Exception("Your platform doesn't support [HttpListener]");
            
            Logger.LogTrace($"Processor threads: {rpcConfig.Threads}");
            _requestProcessors = new Thread[rpcConfig.Threads > 0 ? rpcConfig.Threads : 5];
            _messages = new BlockingCollection<HttpListenerContext>();
            _httpListener = new HttpListener();

            _listenerLoop = new Thread(() =>  _Worker(rpcConfig));
            foreach (var host in rpcConfig.Hosts ?? throw new InvalidOperationException())
                _httpListener?.Prefixes.Add($"http://{host}:{rpcConfig.Port}/");
            _httpListener!.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
            _apiKey = rpcConfig.ApiKey ?? throw new InvalidOperationException();
            _httpListener.Start();
            _listenerLoop.Start();
            
            for (int i = 0; i < _requestProcessors.Length; i++)
            {
                _requestProcessors[i] = new Thread(() => Processor());
                _requestProcessors[i].Start();
            }
        }

        public void Stop()
        {
            Logger.LogTrace($"Stop");
            _messages.CompleteAdding();

            foreach (Thread worker in _requestProcessors) worker.Join();

            _httpListener?.Stop();
            _listenerLoop.Join();        
        }

        private void _Worker(RpcConfig rpcConfig)
        {
            Logger.LogTrace($"_Worker");
            try
            {
                while (_httpListener!.IsListening)
                {
                    try
                    {
                        _messages.Add(_httpListener.GetContext());
                        //_Handle(_httpListener.GetContext());
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e.Message);
            }
        }
        
        private void Processor()
        {
            Logger.LogTrace($"Processor");
            try
            {
                for (;;)
                {
                    HttpListenerContext context = _messages.Take();
                    _Handle (context);
                }
            } 
            catch (Exception e)
            {
                Logger.LogError(e.Message);
            }
        }

        private bool _Handle(HttpListenerContext context)
        {
            Logger.LogTrace($"_Handle");
            var request = context.Request;
            Logger.LogInformation($"{request.HttpMethod}");
            var response = context.Response;
            /* check is request options pre-flight */
            if (request.HttpMethod == "OPTIONS")
            {

                if (request.Headers["Origin"] != null)
                {
                    response.AddHeader("Access-Control-Allow-Origin", request.Headers["Origin"]);
                }
                response.AddHeader("Access-Control-Allow-Headers", "*");
                response.AddHeader("Access-Control-Allow-Methods", "GET, POST");
                response.AddHeader("Access-Control-Max-Age", "1728000");
                response.AddHeader("Access-Control-Allow-Credentials", "true");
                response.Close();
                return true;
            }
            using var reader = new StreamReader(request.InputStream);
            var body = reader.ReadToEnd();
            Logger.LogInformation($"Body: [{body}]");
            var isArray = body.StartsWith("[");
                    
            if (request.Headers["Origin"] != null)
                response.AddHeader("Access-Control-Allow-Origin", request.Headers["Origin"]);
            response.Headers.Add("Access-Control-Allow-Methods", "POST, GET");
            response.Headers.Add("Access-Control-Allow-Credentials", "true");
            var rpcResultHandler = new AsyncCallback(result =>
            {
                if (!(result is JsonRpcStateAsync jsonRpcStateAsync))
                    return;
                
                var resultString = jsonRpcStateAsync.Result;

                if (isArray && !jsonRpcStateAsync.Result.StartsWith("["))
                    resultString = "[" + resultString + "]";
                    
                var output = Encoding.UTF8.GetBytes(resultString);
                Logger.LogInformation($"output: [{resultString}]");
                response.OutputStream.Write(output, 0, output.Length);
                response.OutputStream.Flush();
                response.Close();
            });
            var async = new JsonRpcStateAsync(rpcResultHandler, null)
            {
                JsonRpc = body
            };

            var requests = isArray ? JArray.Parse(body) : new JArray{JObject.Parse(body)};

            foreach (var item in requests)
            {
                var requestObj = (JObject) item;
                if (!_CheckAuth(requestObj, context))
                {
                    var error = new JObject
                    {
                        ["code"] = -32600,
                        ["message"] = "Invalid API key",
                    };
                    var res = new JObject
                    {
                        ["jsonrpc"] = "2.0",
                        ["error"] = error,
                        ["id"] = ulong.Parse(requestObj["id"]!.ToString()),
                    };
                    var output = Encoding.UTF8.GetBytes(res.ToString());
                    Logger.LogInformation($"output: [{res.ToString()}]");
                    response.OutputStream.Write(output, 0, output.Length);
                    response.OutputStream.Flush();
                    response.Close();
                    return false;
                }
            }
            

            JsonRpcProcessor.Process(async);
            return true;
        }

        private bool _CheckAuth(JObject body, HttpListenerContext context)
        {
            if (context.Request.IsLocal) return true;
            if (_privateMethods.Contains(body["method"]!.ToString()))
            {
                return !(body["key"] is null) && Equals(body["key"]!.ToString(), _apiKey);
            }

            return true;
        }
    }
}