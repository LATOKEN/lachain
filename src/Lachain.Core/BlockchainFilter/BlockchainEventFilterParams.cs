﻿using System.Collections.Generic;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Core.BlockchainFilter
{
    public class BlockchainEventFilterParams
    {
        public BlockchainEvent EventType;
        public ulong LastSyncedBlock;

        public ulong PollingTime;

        public ulong? FromBlock, ToBlock;

        public List<UInt160> AddressList = new List<UInt160>();
        public List<UInt256> TopicLists = new List<UInt256>();
        public List<UInt256> PendingTransactionList = new List<UInt256>();

        public BlockchainEventFilterParams(BlockchainEvent eventType, ulong lastSyncedBlock)
        {
            EventType = eventType;
            LastSyncedBlock = lastSyncedBlock;
            PollingTime = TimeUtils.CurrentTimeMillis();
        }

        public BlockchainEventFilterParams(BlockchainEvent eventType, ulong? fromBlock, ulong? toBlock, List<UInt160> addresses, List<UInt256> topics)
        {
            EventType = eventType;
            FromBlock = fromBlock;
            ToBlock = toBlock;
            AddressList = new List<UInt160>(addresses);
            TopicLists = new List<UInt256>(topics);
            PollingTime = TimeUtils.CurrentTimeMillis();
        }

        public BlockchainEventFilterParams(BlockchainEvent eventType, List<UInt256> txHashes)
        {
            EventType = eventType;
            PendingTransactionList = new List<UInt256>(txHashes);
            PollingTime = TimeUtils.CurrentTimeMillis();
        }
    }
}