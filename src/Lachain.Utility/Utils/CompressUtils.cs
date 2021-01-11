using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Lachain.Utility.Utils
{
    public static class CompressUtils
    {
        public static IEnumerable<byte> DeflateCompress(byte[] data)
        {
            using var stream = new MemoryStream(100 * 1024);
            var deflate = new DeflateStream(stream, CompressionLevel.Optimal);
            deflate.Write(data);
            deflate.Flush();
            deflate.Close();
            return stream.ToArray().Length < data.Length
                ? new byte[] {1}.Concat(stream.ToArray())
                : new byte[] {0}.Concat(data);
        }

        public static IEnumerable<byte> DeflateDecompress(byte[] compressed)
        {
            if (compressed[0] == 0) return compressed.Skip(1);
            using var stream = new MemoryStream(compressed.Skip(1).ToArray(), false);
            var deflate = new DeflateStream(stream, CompressionMode.Decompress);
            using var outputStream = new MemoryStream(100 * 1024);
            deflate.CopyTo(outputStream);
            return outputStream.ToArray();
        }
    }
}