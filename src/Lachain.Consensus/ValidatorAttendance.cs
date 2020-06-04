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
        private IDictionary<string, ulong> _attendance;
        private IDictionary<string, ulong> _nextAttendance;
        public ulong Cycle { get; set; }

        public ValidatorAttendance(ulong cycle)
        {
            Logger.LogInformation("Creating validator attendance repository with cycle " + cycle);
            Cycle = cycle;
            _attendance = new Dictionary<string, ulong>();
            _nextAttendance = new Dictionary<string, ulong>();
        }

        public ValidatorAttendance(ulong cycle, IDictionary<string, ulong> validatorAttendance, IDictionary<string, ulong> nextValidatorAttendance)
        {
            Cycle = cycle;
            _attendance = validatorAttendance;
            _nextAttendance = nextValidatorAttendance;
        }

        public ulong GetAttendance(byte[] publicKey)
        {
            if (_attendance.ContainsKey(publicKey.ToHex()))
                return _attendance[publicKey.ToHex()];
            return 0;
        }

        public ulong GetNextCycleAttendance(byte[] publicKey)
        {
            if (_nextAttendance.ContainsKey(publicKey.ToHex()))
                return _nextAttendance[publicKey.ToHex()];
            return 0;
        }

        public void NextCycle()
        {
            Cycle++;
            _attendance = _nextAttendance;
            _nextAttendance = new Dictionary<string, ulong>();
        }

        public void IncrementAttendance(byte[] publicKey, ulong cycle)
        {
            if (cycle == Cycle)
                _attendance[publicKey.ToHex()] = GetAttendance(publicKey) + 1;
            else
                _nextAttendance[publicKey.ToHex()] = GetNextCycleAttendance(publicKey) + 1;    
            
        }

        public byte[] ToBytes()
        {
            using var stream = new MemoryStream();
            stream.Write(_attendance.Count.ToBytes().ToArray());
            stream.Write(Cycle.ToBytes().ToArray());
           
            foreach (var (publicKey, attendance) in _attendance)
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

        public static ValidatorAttendance FromBytes(ReadOnlyMemory<byte> bytes, ulong currentCycle)
        {
            var attendanceCount = bytes.Slice(0, 4).Span.ToInt32();
            var cycle = bytes.Slice(4, 8).Span.ToUInt64();
            
            var attendanceDict = bytes.Slice(12, attendanceCount * (33 + 8))
                .Batch(33 + 8)
                .Select(x => new KeyValuePair<string, ulong>(
                    x.Slice(0, 33).ToArray().ToHex(),
                    x.Slice(33, 8).Span.ToUInt64())
                ).ToDictionary(pair => pair.Key, pair => pair.Value);
            
            var nextAttendanceOffset = 12 + attendanceCount * (33 + 8);
            var nextAttendanceDict = bytes.Slice(nextAttendanceOffset)
                .Batch(33 + 8)
                .Select(x => new KeyValuePair<string, ulong>(
                    x.Slice(0, 33).ToArray().ToHex(),
                    x.Slice(33, 8).Span.ToUInt64())
                ).ToDictionary(pair => pair.Key, pair => pair.Value);
            
            if (cycle < currentCycle - 1 && currentCycle > 0)
                return new ValidatorAttendance(currentCycle, new Dictionary<string, ulong>(), new Dictionary<string, ulong>());
            
            return new ValidatorAttendance(cycle, attendanceDict, nextAttendanceDict);
        }

        public bool Equals(ValidatorAttendance? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Cycle == other.Cycle &&
                   _attendance.SequenceEqual(other._attendance);
        }
    }
}