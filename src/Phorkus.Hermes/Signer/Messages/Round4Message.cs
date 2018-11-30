using System.Collections.Generic;
using System.Linq;
using Org.BouncyCastle.Math;

namespace Phorkus.Hermes.Signer.Messages
{
    public class Round4Message
    {
        public IEnumerable<BigInteger> openRiWi;
        public Zkpi2 zkp2;

        public Round4Message() {

        }

        public Round4Message(IEnumerable<BigInteger> openRiWi, Zkpi2 zkp2) {
            this.openRiWi = openRiWi;
            this.zkp2 = zkp2;
        }

        public string serialize()
        {
            var riwi = string.Join("Z", openRiWi.Select(value => value.ToString()));
            return riwi + "!" + zkp2.serialize();
        }

        public void deserialize(string s) {
            var arr = s.Split('!');
            openRiWi = new BigInteger[2];
            var values = arr[0].Split('Z');
            openRiWi = new[]
            {
                new BigInteger(values[0]),
                new BigInteger(values[1])
            };
            zkp2 = new Zkpi2();
            zkp2.deserialize(arr[1]);
        }
    }
}