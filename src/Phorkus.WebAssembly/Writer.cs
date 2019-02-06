using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Phorkus.WebAssembly
{
    internal sealed class Writer : IDisposable
    {
        private readonly UTF8Encoding utf8 = new UTF8Encoding(false, false);
        private BinaryWriter writer;

        public Writer(Stream output)
        {
            this.writer = new BinaryWriter(output, utf8, true);
        }

        public void Write(uint value)
        {
            Debug.Assert(this.writer != null);

            this.writer.Write(value);
        }

        public void WriteVar(uint value)
        {
            Debug.Assert(this.writer != null);

            var remaining = value >> 7;
            while (remaining != 0)
            {
                this.writer.Write((byte)((value & 0x7f) | 0x80));
                value = remaining;
                remaining >>= 7;
            }
            writer.Write((byte)(value & 0x7f));
        }

        public void WriteVar(ulong value)
        {
            Debug.Assert(this.writer != null);

            var remaining = value >> 7;
            while (remaining != 0)
            {
                writer.Write((byte)((value & 0x7f) | 0x80));
                value = remaining;
                remaining >>= 7;
            }
            writer.Write((byte)(value & 0x7f));
        }

        public void WriteVar(int value)
        {
            Debug.Assert(this.writer != null);

            var remaining = value >> 7;
            var hasMore = true;
            var end = ((value & int.MinValue) == 0) ? 0 : -1;
            do
            {
                hasMore = (remaining != end) || ((remaining & 1) != ((value >> 6) & 1));
                writer.Write((byte)((value & 0x7f) | (hasMore ? 0x80 : 0)));
                value = remaining;
                remaining >>= 7;
            } while (hasMore);
        }

        public void WriteVar(long value)
        {
            Debug.Assert(this.writer != null);

            var remaining = value >> 7;
            var hasMore = true;
            var end = ((value & long.MinValue) == 0) ? 0 : -1;
            do
            {
                hasMore = (remaining != end) || ((remaining & 1) != ((value >> 6) & 1));
                writer.Write((byte)(((byte)value & 0x7f) | (hasMore ? 0x80 : 0)));
                value = remaining;
                remaining >>= 7;
            } while (hasMore);
        }

        public void Write(float value)
        {
            Debug.Assert(this.writer != null);

            this.writer.Write(value);
        }

        public void Write(double value)
        {
            Debug.Assert(this.writer != null);

            this.writer.Write(value);
        }

        public void Write(string value)
        {
            var bytes = this.utf8.GetBytes(value);
            this.WriteVar((uint)bytes.Length);
            this.Write(bytes);
        }

        public void Write(byte[] value)
        {
            Debug.Assert(this.writer != null);

            this.writer.Write(value);
        }

        public void Write(byte[] buffer, int index, int count)
        {
            Debug.Assert(this.writer != null);

            this.writer.Write(buffer, index, count);
        }

        public void Write(byte value)
        {
            Debug.Assert(this.writer != null);

            this.writer.Write(value);
        }

        #region IDisposable Support
        void Dispose(bool disposing)
        {
            if (this.writer == null)
                return;

            try //Tolerate bad dispose implementations.
            {
                this.writer.Dispose();
            }
            catch
            {
            }

            this.writer = null;
        }

        ~Writer() => Dispose(false);

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