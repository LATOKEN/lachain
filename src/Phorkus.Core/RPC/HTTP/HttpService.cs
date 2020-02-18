using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using AustinHarris.JsonRpc;

namespace Phorkus.Core.RPC.HTTP
{
    public class HttpService
    {
        private const int RequestMaxSize = 64 * 1024;

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
            var buffer = new byte[RequestMaxSize];
            var length = request.InputStream.Read(buffer, 0, buffer.Length);
            if (length <= 0 || length >= buffer.Length && request.InputStream.CanRead)
                return false;
            var body = Encoding.UTF8.GetString(buffer, 0, length);
            if (request.HttpMethod == "OPTIONS")
            {
                response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
                response.AddHeader("Access-Control-Allow-Methods", "GET, POST");
                response.AddHeader("Access-Control-Max-Age", "1728000");
                return true;
            }

            response.Headers.Add("Access-Control-Allow-Origin", "*");
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
            JsonRpcProcessor.Process(async);
            return true;
        }
    }
}