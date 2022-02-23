﻿using System;
using System.Collections.Generic;
using Lachain.Proto;
using Lachain.Utility.Utils;
using System.Linq;


namespace Lachain.Core.BlockchainFilter
{
    public class BlockchainEventFilterParams
    {
        
        public BlockchainEvent EventType;
        public ulong LastSyncedBlock;

        public ulong PollingTime;

        public ulong? FromBlock, ToBlock;

        public List<UInt160> AddressList = new List<UInt160>();
        public List<List<UInt256>> TopicLists = new List<List<UInt256>>();
        public List<UInt256> PendingTransactionList = new List<UInt256>();

        public BlockchainEventFilterParams(BlockchainEvent eventType, ulong lastSyncedBlock)
        {
            EventType = eventType;
            LastSyncedBlock = lastSyncedBlock;
            PollingTime = TimeUtils.CurrentTimeMillis();
        }

        public BlockchainEventFilterParams(BlockchainEvent eventType, ulong? fromBlock, ulong? toBlock, List<UInt160> addresses, List<List<UInt256>> topics)
        {
            EventType = eventType;
            FromBlock = fromBlock;
            ToBlock = toBlock;
            AddressList = new List<UInt160>(addresses);
            TopicLists = new List<List<UInt256>>(topics);
            PollingTime = TimeUtils.CurrentTimeMillis();
        }

        public BlockchainEventFilterParams(BlockchainEvent eventType, UInt256[] txHashes)
        {
            EventType = eventType;
            // sorting the tx hashes for further optimization
            Array.Sort(txHashes, (x,y) => UInt256Utils.Compare(x,y));
            PendingTransactionList = new List<UInt256>(txHashes.ToList());
            PollingTime = TimeUtils.CurrentTimeMillis();
        }
    }
}