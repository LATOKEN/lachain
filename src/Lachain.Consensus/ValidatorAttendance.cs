using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lachain.Logger;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;

namespace Lachain.Core.ValidatorStatus
{
    public class ValidatorAttendance : IEquatable<ValidatorAttendance>
    {
        private static readonly ILogger<ValidatorAttendance> Logger = LoggerFactory.GetLoggerForClass<ValidatorAttendance>();
        private IDictionary<string, ulong> _previousAttendance;
        private IDictionary<string, ulong> _nextAttendance;
        public ulong PreviousCycleNum { get; set; }
        public ulong NextCycleNum { get; set; }

        public ValidatorAttendance(ulong previousCycleNum)
        {
            Logger.LogInformation("Creating validator attendance repository with cycle " + previousCycleNum);
            PreviousCycleNum = previousCycleNum;
            NextCycleNum = previousCycleNum + 1;
            _previousAttendance = new Dictionary<string, ulong>();
            _nextAttendance = new Dictionary<string, ulong>();
        }

        private ValidatorAttendance(ulong previousCycleNum, IDictionary<string, ulong> validatorPreviousAttendance, IDictionary<string, ulong> nextValidatorAttendance)
        {
            PreviousCycleNum = previousCycleNum;
            NextCycleNum = previousCycleNum + 1;
            _previousAttendance = validatorPreviousAttendance;
            _nextAttendance = nextValidatorAttendance;
        }

        public ulong GetAttendanceForCycle(byte[] publicKey, ulong cycle)
        {
            if (cycle == PreviousCycleNum)
                if (_previousAttendance.ContainsKey(publicKey.ToHex()))
                    return _previousAttendance[publicKey.ToHex()];
            if (cycle == NextCycleNum)
                if (_nextAttendance.ContainsKey(publicKey.ToHex()))
                    return _nextAttendance[publicKey.ToHex()];
            return 0;
        }

        public void IncrementAttendanceForCycle(byte[] publicKey, ulong cycle)
        {
            if (cycle == PreviousCycleNum)
                _previousAttendance[publicKey.ToHex()] = GetAttendanceForCycle(publicKey, cycle) + 1;
            if (cycle == NextCycleNum)
                _nextAttendance[publicKey.ToHex()] = GetAttendanceForCycle(publicKey, cycle) + 1;
            
            Logger.LogDebug($"Attendance incremented: {GetAttendanceForCycle(publicKey, cycle)} cycle {cycle}");
        }

        public byte[] ToBytes()
        {
            using var stream = new MemoryStream();
            stream.Write(_previousAttendance.Count.ToBytes().ToArray());
            stream.Write(PreviousCycleNum.ToBytes().ToArray());
           
            foreach (var (publicKey, attendance) in _previousAttendance)
            {
                stream.Write(publicKey.HexToBytes());
                stream.Write(attendance.ToBytes().ToArray());
            }
           
            foreach (var (publicKey, attendance) in _nextAttendance)
            {
                stream.Write(publicKey.HexToBytes());
                stream.Write(attendance.ToBytes().ToArray());
            }
            
            return stream.ToArray();
        }

        public static ValidatorAttendance FromBytes(ReadOnlyMemory<byte> bytes, ulong currentCycle, bool currentAsNext)
        {
            var previousAttendanceCount = bytes.Slice(0, 4).Span.ToInt32();
            var previousCycle = bytes.Slice(4, 8).Span.ToUInt64();
            
            var previousAttendanceDict = bytes.Slice(12, previousAttendanceCount * (33 + 8))
                .Batch(33 + 8)
                .Select(x => new KeyValuePair<string, ulong>(
                    x.Slice(0, 33).ToArray().ToHex(),
                    x.Slice(33, 8).Span.ToUInt64())
                ).ToDictionary(pair => pair.Key, pair => pair.Value);
            
            var nextAttendanceOffset = 12 + previousAttendanceCount * (33 + 8);
            var nextAttendanceDict = bytes.Slice(nextAttendanceOffset)
                .Batch(33 + 8)
                .Select(x => new KeyValuePair<string, ulong>(
                    x.Slice(0, 33).ToArray().ToHex(),
                    x.Slice(33, 8).Span.ToUInt64())
                ).ToDictionary(pair => pair.Key, pair => pair.Value);
            
            if (previousCycle == currentCycle)
                return new ValidatorAttendance(previousCycle, previousAttendanceDict, nextAttendanceDict);

            if (previousCycle == currentCycle - 1 && !currentAsNext) 
                return new ValidatorAttendance(previousCycle, previousAttendanceDict, nextAttendanceDict);

            if (previousCycle == currentCycle - 1 && currentAsNext) 
                return new ValidatorAttendance(currentCycle, nextAttendanceDict, new Dictionary<string, ulong>());
            
            return new ValidatorAttendance(previousCycle, new Dictionary<string, ulong>(), new Dictionary<string, ulong>());
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