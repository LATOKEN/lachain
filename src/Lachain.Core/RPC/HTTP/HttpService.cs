using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using AustinHarris.JsonRpc;
using Google.Protobuf;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Blockchain.Interface;
using Lachain.Crypto;
using Newtonsoft.Json.Linq;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;
using Secp256k1Net;
using Nethereum.Signer;
using Nethereum.Signer.Crypto;

namespace Lachain.Core.RPC.HTTP
{
    public class HttpService
    {
        private static readonly ILogger<HttpService> Logger =
            LoggerFactory.GetLoggerForClass<HttpService>();

        private readonly IBlockManager _blockManager;

        public HttpService(IBlockManager blockManager)
        {
            _blockManager = blockManager;
        }

        public void Start(RpcConfig rpcConfig)
        {
            Logger.LogInformation("Starting HttpService");

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
            // TODO: remove this comment after the working auth
            //"validator_stop",
            "fe_sendTransaction",
            "deleteTransactionPoolRepository",
            "clearInMemoryPool",
            "eth_sendTransaction",
            "eth_signTransaction",
        };

        public void Stop()
        {
            _httpListener?.Stop();
        }

        private void _Worker(RpcConfig rpcConfig)
        {
            Logger.LogInformation("Worker started");

            if(!HttpListener.IsSupported)
                throw new Exception("Your platform doesn't support [HttpListener]");
            _httpListener = new HttpListener();
            foreach(var host in rpcConfig.Hosts ?? throw new InvalidOperationException())
                {
                    Logger.LogInformation($"****** Host:: ********************:: {host} ");
                    Logger.LogInformation($"Url: http://{host}:{rpcConfig.Port}/");

                    _httpListener.Prefixes.Add($"http://{host}:{rpcConfig.Port}/");
                }
            _httpListener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
            _apiKey = rpcConfig.ApiKey ?? throw new InvalidOperationException();
            _httpListener.Start();
            while(_httpListener.IsListening)
            {
                try
                {
                    _Handle(_httpListener.GetContext());
                }
                catch(Exception e)
                {
                    Console.Error.WriteLine(e);
                }
            }

            _httpListener.Stop();
        }

        private bool _Handle(HttpListenerContext context)
        {
            Logger.LogInformation("_Handle started");

            var request = context.Request;

            // Get signature from request header
            var signatures = request.Headers.GetValues("Signature");
            var signature = string.Empty;
            if(signatures != null && signatures.Length > 0)
            {
                signature = signatures[0];
            }

            Logger.LogInformation($"signature: {signature}");

            // Get timestamp from request header
            var timestamps = request.Headers.GetValues("Timestamp");
            

            var timestamp = string.Empty;
            if(timestamps != null && timestamps.Length > 0)
            {
                timestamp = timestamps[0];
            }
            Logger.LogInformation($"timestamp: {timestamp}");

            Logger.LogInformation($"{request.HttpMethod}");

            var response = context.Response;
            /* check is request options pre-flight */
            if(request.HttpMethod == "OPTIONS")
            {
                Logger.LogInformation($"request.httpmethod == option");

                if(request.Headers["Origin"] != null)
                {
                    response.AddHeader("Access-Control-Allow-Origin", request.Headers["Origin"]!);
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

            if(request.Headers["Origin"] != null){
                response.AddHeader("Access-Control-Allow-Origin", request.Headers["Origin"]!);
                Logger.LogInformation($"NULL origin");
            }
            response.Headers.Add("Access-Control-Allow-Methods", "POST, GET");
            response.Headers.Add("Access-Control-Allow-Credentials", "true");

            var rpcResultHandler = new AsyncCallback(result =>
            {
                Logger.LogInformation($"result :: {result}");

                if(!(result is JsonRpcStateAsync jsonRpcStateAsync))
                    return;

                var resultString = jsonRpcStateAsync.Result;

                Logger.LogInformation($"resultString :: {resultString}");

                if(isArray && !jsonRpcStateAsync.Result.StartsWith("["))
                    resultString = "[" + resultString + "]";

                var output = Encoding.UTF8.GetBytes(resultString);
                Logger.LogInformation($"output: [{resultString}]");
                response.OutputStream.Write(output, 0, output.Length);
                response.OutputStream.Flush();
                response.Close();
            });

            Logger.LogInformation($"After RPC handler completed");

            var async = new JsonRpcStateAsync(rpcResultHandler, null)
            {
                JsonRpc = body
            };

            Logger.LogInformation($"json body check");
            Logger.LogInformation($"body:: {body}");
            Logger.LogInformation($"isArray:: {isArray}");

            var jobj = new JArray { JObject.Parse(body) };
            Logger.LogInformation($"jobj:: {jobj}");

            var requests = isArray ? JArray.Parse(body) : new JArray { JObject.Parse(body) };

            Logger.LogInformation($"requests :: {requests}");

            foreach(var item in requests)
            {
                var requestObj = (JObject)item;
                
                Logger.LogInformation($"request {requestObj.ToString()}");

                if(requestObj == null) return false;

                if(!_CheckAuth(requestObj, context, signature, timestamp))
                {
                    Logger.LogInformation("_CheckAuth function called:");

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
                    Logger.LogInformation($"output: [{res}]");
                    response.OutputStream.Write(output, 0, output.Length);
                    response.OutputStream.Flush();
                    response.Close();
                    return false;
                }
            }

            JsonRpcProcessor.Process(async);
            return true;
        }

        private bool _CheckAuth(JObject body, HttpListenerContext context, string signature, string timestamp)
        {

            // if(context.Request.IsLocal) return true;

            if(_privateMethods.Contains(body["method"]!.ToString()))
            {
                Logger.LogInformation($"private method list contains: {body["method"]!.ToString()}");

                if(string.IsNullOrEmpty(signature)) return false;
                if(string.IsNullOrEmpty(timestamp)) return false;
                // If unix timestamp diff is longer than 30 minutes, we dont handle it
                if(!long.TryParse(timestamp.Trim(), out long unixTimestamp)) return false;
                TimeSpan timeSpan = DateTimeOffset.Now.Subtract(DateTimeOffset.FromUnixTimeSeconds(unixTimestamp));
                if(timeSpan.TotalMinutes >= 30)
                    return false;

                // Serialize params (format: key1value1key2value2...)
                string serializedParams = string.Empty;
                if(body["params"] != null && !string.IsNullOrEmpty(body["params"]!.ToString()))
                {
                    var paramsObject = (JObject)body["params"]!;
                    if(paramsObject != null)
                    {
                        foreach(var param in paramsObject)
                        {
                            serializedParams += param.Key + param.Value?.ToString();
                        }
                        
                    }
                }

                var messageToSign = body["method"]!.ToString() + serializedParams + timestamp;

                var messageBytes = Encoding.UTF8.GetBytes(messageToSign);
                var tx = new Proto.Transaction
                {
                    GasLimit = 0,
                    GasPrice = 0,
                    Nonce = 0,
                    Invocation = ByteString.CopyFrom(messageBytes),
                    To = new UInt160(),
                    Value = new UInt256()
                };
                
                var signatureBytes = signature.HexToBytes();
                var publicKey = _apiKey!.HexToBytes();
                var crypto = CryptoProvider.GetCrypto();
                var useNewChainId = HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight());
                var messageHash = tx.RawHash(useNewChainId).ToBytes();
                var pubKey = crypto.RecoverSignatureHashed(messageHash, signatureBytes, useNewChainId);
                var decompressed = crypto.DecodePublicKey(pubKey, false).TakeLast(64).ToArray();
                if (!decompressed.SequenceEqual(publicKey))
                    return false;
                return crypto.VerifySignatureHashed(messageHash, signatureBytes, pubKey, useNewChainId);
            }

            return true;
        }
    }
}