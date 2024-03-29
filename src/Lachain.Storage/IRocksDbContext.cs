﻿using System;
using System.Collections.Generic;
using RocksDbSharp;

namespace Lachain.Storage
{
    public interface IRocksDbContext : IDisposable
    {
        ulong EstimateNumberOfKeys();
        ulong EstimateDirSize();
        byte[] Get(byte[] key);
        IDictionary<byte[], byte[]> GetMany(IEnumerable<byte[]> keys);
        void Save(byte[] key, byte[] content);
        void SaveBatch(WriteBatch batch);
        void Save(IEnumerable<byte> key, IEnumerable<byte> content);
        void Delete(byte[] key);
        Iterator? GetIteratorWithUpperBound(byte[] prefix, byte[] upperBound);
        Iterator? GetIteratorForValidKeys(byte[] prefix);
        void CompactAll();
    }
}