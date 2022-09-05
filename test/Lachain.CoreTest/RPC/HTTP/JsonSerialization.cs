using NUnit.Framework;
using AustinHarris.JsonRpc;
using Lachain.Core.RPC.HTTP;
using Newtonsoft.Json.Linq;

namespace Lachain.CoreTest.RPC.HTTP
{
    public class JsonSerialization
    {
        [SetUp]
        public void Setup()
        {
           
        }

        [TearDown]
        public void Teardown()
        {
        }

        
        [Test]
        public void Test_JsonSerialization()
        {
            Assert.AreEqual("", HttpService.SerializeParams(null));
            Assert.AreEqual("{}", HttpService.SerializeParams(new JObject()));
            var obj = new JObject()
            {
                ["test"] = "test"
            };
            Assert.AreEqual("testtest", HttpService.SerializeParams(obj));
            obj = new JObject()
            {
                ["test"] = 3
            };
            Assert.AreEqual("test3", HttpService.SerializeParams(obj));
            obj = new JObject()
            {
                ["test"] = 3, 
                ["test2"]=4
            };
            Assert.AreEqual("test3test24", HttpService.SerializeParams(obj));
            obj = new JObject()
            {
                ["test"] = new JArray(1, 2, 3, 4), 
                ["test2"]= new JObject
                {
                    ["level2"] = 5
                }
                    
            };
            Assert.AreEqual("test1234test2level25", HttpService.SerializeParams(obj));
            var arr = new JArray(1, 2, 3, 4, 5);
            Assert.AreEqual("12345", HttpService.SerializeParams(arr));
        }
    }
}