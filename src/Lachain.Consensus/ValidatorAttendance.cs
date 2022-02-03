using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Logger;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Nethereum.RLP;

namespace Lachain.Consensus
{
    public class ValidatorAttendance : IEquatable<ValidatorAttendance>
    {
        private static readonly ILogger<ValidatorAttendance> Logger =
            LoggerFactory.GetLoggerForClass<ValidatorAttendance>();

        private readonly IDictionary<string, ulong> _previousAttendance;
        private readonly IDictionary<string, ulong> _nextAttendance;
        public ulong PreviousCycleNum { get; }
        public ulong NextCycleNum { get; }

        public ValidatorAttendance(ulong previousCycleNum)
        {
            Logger.LogInformation("Creating validator attendance repository with cycle " + previousCycleNum);
            PreviousCycleNum = previousCycleNum;
            NextCycleNum = previousCycleNum + 1;
            _previousAttendance = new Dictionary<string, ulong>();
            _nextAttendance = new Dictionary<string, ulong>();
        }

        private ValidatorAttendance(ulong previousCycleNum, IDictionary<string, ulong> validatorPreviousAttendance,
            IDictionary<string, ulong> nextValidatorAttendance)
        {
            Logger.LogTrace($"ValidatorAttendance({previousCycleNum})");
            PreviousCycleNum = previousCycleNum;
            NextCycleNum = previousCycleNum + 1;
            _previousAttendance = validatorPreviousAttendance;
            _nextAttendance = nextValidatorAttendance;
        }

        public ulong GetAttendanceForCycle(byte[] publicKey, ulong cycle)
        {
            Logger.LogTrace($"GetAttendanceForCycle({publicKey.ToHex()}, {cycle})");
            if (cycle == PreviousCycleNum)
                if (_previousAttendance.ContainsKey(publicKey.ToHex()))
                    return _previousAttendance[publicKey.ToHex()];
            if (cycle == NextCycleNum)
                if (_nextAttendance.ContainsKey(publicKey.ToHex()))
                    return _nextAttendance[publicKey.ToHex()];
            Logger.LogTrace($"Invalid cycle in params: cycle {cycle}, PreviousCycleNum {PreviousCycleNum}, NextCycleNum {NextCycleNum}");
            return 0;
        }

        public void IncrementAttendanceForCycle(byte[] publicKey, ulong cycle)
        {
            Logger.LogTrace($"IncrementAttendanceForCycle({publicKey.ToHex()}, {cycle})");
            if (cycle == PreviousCycleNum)
                _previousAttendance[publicKey.ToHex()] = GetAttendanceForCycle(publicKey, cycle) + 1;
            else if (cycle == NextCycleNum)
                _nextAttendance[publicKey.ToHex()] = GetAttendanceForCycle(publicKey, cycle) + 1;
            else
            {
                Logger.LogTrace($"Invalid cycle in params: cycle {cycle}, PreviousCycleNum {PreviousCycleNum}, NextCycleNum {NextCycleNum}");
            }
        }

        public byte[] ToBytes()
        {
            Logger.LogTrace("ToBytes()");
            var a = new List<byte[]>
            {
                _previousAttendance.Count.ToBytes().ToArray(),
                PreviousCycleNum.ToBytes().ToArray()
            };
            foreach (var (publicKey, attendance) in _previousAttendance)
            {
                a.Add(publicKey.HexToBytes());
                a.Add(attendance.ToBytes().ToArray());
            }

            foreach (var (publicKey, attendance) in _nextAttendance)
            {
                a.Add(publicKey.HexToBytes());
                a.Add(attendance.ToBytes().ToArray());
            }

            return RLP.EncodeList(a.Select(RLP.EncodeElement).ToArray());
        }

        public static ValidatorAttendance FromBytes(ReadOnlyMemory<byte> bytes, ulong currentCycle, bool currentAsNext)
        {
            Logger.LogTrace($"FromBytes(..., {currentCycle}, {currentAsNext})");
            var decoded = (RLPCollection) RLP.Decode(bytes.ToArray());
            var previousAttendanceCount = decoded[0].RLPData.AsReadOnlySpan().ToInt32();
            var previousCycle = decoded[1].RLPData.AsReadOnlySpan().ToUInt64();
            Logger.LogTrace($"previousCycle in bytes is {previousCycle}");

            var previousAttendanceDict = Enumerable.Range(0, previousAttendanceCount)
                .Select(i => (
                    decoded[2 + 2 * i].RLPData.ToHex(),
                    decoded[2 + 2 * i + 1].RLPData.AsReadOnlySpan().ToUInt64())
                )
                .ToDictionary(x => x.Item1, y => y.Item2);

            var nextAttendanceDict = decoded.Skip(2 + 2 * previousAttendanceCount)
                .Select(x => x.RLPData)
                .Batch(2)
                .Select(x =>
                {
                    var t = x.ToArray();
                    return (t[0].ToHex(), t[1].AsReadOnlySpan().ToUInt64());
                })
                .ToDictionary(x => x.Item1, x => x.Item2);

            if (previousCycle == currentCycle)
                return new ValidatorAttendance(previousCycle, previousAttendanceDict, nextAttendanceDict);

            if (previousCycle == currentCycle - 1 && !currentAsNext)
                return new ValidatorAttendance(previousCycle, previousAttendanceDict, nextAttendanceDict);

            if (previousCycle == currentCycle - 1 && currentAsNext)
                return new ValidatorAttendance(currentCycle, nextAttendanceDict, new Dictionary<string, ulong>());

            return new ValidatorAttendance(currentCycle, new Dictionary<string, ulong>(),
                new Dictionary<string, ulong>());
        }

        public bool Equals(ValidatorAttendance? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return PreviousCycleNum == other.PreviousCycleNum &&
                   _previousAttendance.SequenceEqual(other._previousAttendance);
        }
    }
}