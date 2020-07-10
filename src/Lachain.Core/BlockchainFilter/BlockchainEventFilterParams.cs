﻿namespace Lachain.Core.BlockchainFilter
{
    public class BlockchainEventFilterParams
    {
        public BlockchainEvent EventType;
        public ulong LastSyncedBlock;

        public BlockchainEventFilterParams(BlockchainEvent eventType, ulong lastSyncedBlock)
        {
            EventType = eventType;
            LastSyncedBlock = lastSyncedBlock;
        }
    }
}