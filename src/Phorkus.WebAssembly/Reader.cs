﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Phorkus.WebAssembly
{
    internal sealed class Reader : IDisposable
    {
        private readonly UTF8Encoding utf8 = new UTF8Encoding(false, false);
        private BinaryReader reader;
        private long offset;

        public Reader(Stream input)
        {
            //The UTF8 encoding parameter is not actually used; it's just there so that the leaveOpen parameter can be reached.
            this.reader = new BinaryReader(input, utf8, true);
        }

        public long Offset => this.offset;

        public uint ReadUInt32()
        {
            Debug.Assert(this.reader != null);

            var result = this.reader.ReadUInt32();
            this.offset += 4;
            return result;
        }

        public byte ReadVarUInt1() => (Byte)(this.ReadVarUInt32() & 0b1);

        public bool TryReadVarUInt7(out byte result)
        {
            try
            {
                result = this.ReadVarUInt7();
                return true;
            }
            catch (EndOfStreamException)
            {
                result = 0;
                return false;
            }
        }

        public byte ReadVarUInt7() => (byte)(this.ReadVarUInt32() & 0b1111111);

        public sbyte ReadVarInt7() => (sbyte)(this.ReadVarInt32() & 0b11111111);

        public uint ReadVarUInt32()
        {
            var result = 0u;
            var shift = 0;
            while (true)
            {
                uint value = this.ReadByte();
                result |= ((value & 0x7F) << shift);
                if ((value & 0x80) == 0)
                    break;
                shift += 7;
            }

            return result;
        }

        public int ReadVarInt32()
        {
            var result = 0;
            int current;
            var count = 0;
            var signBits = -1;
            var initialOffset = this.offset;
            do
            {
                current = this.ReadByte();
                result |= (current & 0x7F) << (count * 7);
                signBits <<= 7;
                count++;
            } while (((current & 0x80) == 0x80) && count < 5);

            if ((current & 0x80) == 0x80)
                throw new ModuleLoadException("Invalid LEB128 sequence.", initialOffset);

            if (((signBits >> 1) & result) != 0)
                result |= signBits;

            return result;
        }

        public long ReadVarInt64()
        {
            var result = 0L;
            long current;
            var count = 0;
            var signBits = -1L;
            var initialOffset = this.offset;
            do
            {
                current = this.ReadByte();
                result |= (current & 0x7F) << (count * 7);
                signBits <<= 7;
                count++;
            } while (((current & 0x80) == 0x80) && count < 10);

            if ((current & 0x80) == 0x80)
                throw new ModuleLoadException("Invalid LEB128 sequence.", initialOffset);

            if (((signBits >> 1) & result) != 0)
                result |= signBits;

            return result;
        }

        public float ReadFloat32()
        {
            Debug.Assert(this.reader != null);

            var result = this.reader.ReadSingle();
            this.offset += 4;
            return result;
        }

        public double ReadFloat64()
        {
            Debug.Assert(this.reader != null);

            var result = this.reader.ReadDouble();
            this.offset += 8;
            return result;
        }

        public string ReadString(uint length) => utf8.GetString(this.ReadBytes(length));

        public byte[] ReadBytes(uint length)
        {
            Debug.Assert(this.reader != null);

            var result = this.reader.ReadBytes(checked((int)length));
            this.offset += length;
            return result;
        }

        public byte ReadByte()
        {
            Debug.Assert(this.reader != null);

            var result = this.reader.ReadByte();
            this.offset++;
            return result;
        }

        #region IDisposable Support
        void Dispose(bool disposing)
        {
            if (this.reader == null)
                return;

            try //Tolerate bad dispose implementations.
            {
                this.reader.Dispose();
            }
            catch
            {
            }

            this.reader = null;
        }

        ~Reader() => Dispose(false);

        /// <summary>
        /// Releases unmanaged resources associated with this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}