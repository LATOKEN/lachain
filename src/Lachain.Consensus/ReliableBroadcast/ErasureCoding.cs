using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lachain.Consensus.ReliableBroadcast.ReedSolomon.ReedSolomon;
using Lachain.Logger;

namespace Lachain.Consensus.ReliableBroadcast
{
    public class ErasureCoding
    {
        private static readonly ILogger<ErasureCoding> Logger = LoggerFactory.GetLoggerForClass<ErasureCoding>();

        private readonly GenericGF _field;
        private int _dataSize;

        public ErasureCoding()
        {
            _field = new GenericGF(285, 256, 0);
            _dataSize = 0;
        }

        public void EncoderInPlace(int[] plainData)
        {
            var rse = new ReedSolomonEncoder(_field);
            rse.Encode(plainData, plainData.Length / 2);
        }

        public int[] EncoderInPlaceNew(int[] plainData, int addInt)
        {
            var res = new int[plainData.Length];
            for (var i = 0; i < plainData.Length; i++)
            {
                res[i] = plainData[i];
            }

            var rse = new ReedSolomonEncoder(_field);
            rse.Encode(res, addInt);
            return res;
        }

        public byte[] EncoderToByte(int[] plainData, int additionalInts)
        {
            _dataSize = plainData.Length;
            var countItems = _dataSize + additionalInts;
            var tmp = new int[countItems];
            for (var i = 0; i < _dataSize; i++)
            {
                tmp[i] = plainData[i];
            }

            for (var i = _dataSize; i < countItems; i++)
            {
                tmp[i] = 0;
            }

            var rse = new ReedSolomonEncoder(_field);

            rse.Encode(tmp, _dataSize);

            return tmp.Select(current => BitConverter.GetBytes(current)[0]).ToArray();
        }

        public int[] Encoder(int[] plainData, int additionalInts)
        {
            _dataSize = plainData.Length;
            var tmp = new int[_dataSize + additionalInts];
            for (var i = 0; i < _dataSize; i++)
            {
                tmp[i] = plainData[i];
            }

            for (var i = _dataSize; i < _dataSize + additionalInts; i++)
            {
                tmp[i] = 0;
            }

            var rse = new ReedSolomonEncoder(_field);

            rse.Encode(tmp, additionalInts);
            return tmp;
        }

        public void Decode(int[] encryptionText, int additionalBits, int[] tips)
        {
            var rsd = new ReedSolomonDecoder(_field);
            if (rsd.Decode(encryptionText, additionalBits, tips)) return;
            Logger.LogError($"Too many errors-erasures to correct. Additional bits = {additionalBits}");
            Logger.LogError("Code: " + string.Join(", ", encryptionText));
            Logger.LogError("Tips: " + string.Join(", ", tips));
        }

        public void Print(string explanation, IEnumerable<int> source)
        {
            // TODO: move to tests
            Logger.LogDebug(explanation);
            Logger.LogDebug(string.Join(" ", source));
        }
    }
}