using System.Collections.Generic;
using Lachain.Storage.State;

namespace Lachain.Storage.Repositories
{
    public static class CheckpointUtils
    {
        public static List<CheckpointType> GetAllCheckpointTypes()
        {
            var checkpointTypes = new List<CheckpointType>();
            checkpointTypes.Add(CheckpointType.BlockHeight);
            checkpointTypes.Add(CheckpointType.BlockHash);
            checkpointTypes.Add(CheckpointType.BalanceStateHash);
            checkpointTypes.Add(CheckpointType.ContractStateHash);
            checkpointTypes.Add(CheckpointType.EventStateHash);
            checkpointTypes.Add(CheckpointType.StorageStateHash);
            checkpointTypes.Add(CheckpointType.TransactionStateHash);
            checkpointTypes.Add(CheckpointType.ValidatorStateHash);
            checkpointTypes.Add(CheckpointType.CheckpointExist);
            return checkpointTypes;
        }

        public static string GetSnapshotNameForCheckpointType(CheckpointType checkpointType)
        {
            switch (checkpointType)
            {
                case CheckpointType.BalanceStateHash:
                    return "Balances";
                
                case CheckpointType.BlockHash:
                    return "Blocks";

                case CheckpointType.ContractStateHash:
                    return "Contracts";

                case CheckpointType.EventStateHash:
                    return "Events";

                case CheckpointType.StorageStateHash:
                    return "Storage";

                case CheckpointType.TransactionStateHash:
                    return "Transactions";

                case CheckpointType.ValidatorStateHash:
                    return "Validators";

                default:
                    return "";
            }
        }

        public static RepositoryType? GetSnapshotTypeForSnapshotName(string snapshotName)
        {
            switch (snapshotName)
            {
                case "Balances":
                    return RepositoryType.BalanceRepository;

                case "Blocks":
                    return RepositoryType.BlockRepository;

                case "Contracts":
                    return RepositoryType.ContractRepository;
                
                case "Events":
                    return RepositoryType.EventRepository;

                case "Storage":
                    return RepositoryType.StorageRepository;

                case "Transactions":
                    return RepositoryType.TransactionRepository;

                case "Validators":
                    return RepositoryType.ValidatorRepository;

                default:
                    return null;
            }
        }

        public static CheckpointType? GetCheckpointTypeForSnapshotType(RepositoryType repositoryType)
        {
            switch (repositoryType)
            {
                case RepositoryType.BalanceRepository:
                    return CheckpointType.BalanceStateHash;

                case RepositoryType.BlockRepository:
                    return CheckpointType.BlockHash;

                case RepositoryType.ContractRepository:
                    return CheckpointType.ContractStateHash;

                case RepositoryType.EventRepository:
                    return CheckpointType.EventStateHash;

                case RepositoryType.StorageRepository:
                    return CheckpointType.StorageStateHash;

                case RepositoryType.TransactionRepository:
                    return CheckpointType.TransactionStateHash;

                case RepositoryType.ValidatorRepository:
                    return CheckpointType.ValidatorStateHash;

                default:
                    return null;
            }
        }
        
        public static CheckpointType? GetCheckpointTypeForSnapshotName(string snapshotName)
        {
            var snapshotType = GetSnapshotTypeForSnapshotName(snapshotName);
            if (snapshotType is null) return null;
            return GetCheckpointTypeForSnapshotType(snapshotType.Value);
        }

        public static RepositoryType? GetSnapshotTypeForCheckpointType(CheckpointType checkpointType)
        {
            var snapshotName = GetSnapshotNameForCheckpointType(checkpointType);
            if (snapshotName == "") return null;
            return GetSnapshotTypeForSnapshotName(snapshotName);
        }

        public static string GetSnapshotNameForSnapshotType(RepositoryType repositoryType)
        {
            var checkpointType = GetCheckpointTypeForSnapshotType(repositoryType);
            if (checkpointType is null) return "";
            return GetSnapshotNameForCheckpointType(checkpointType.Value);
        }

    }
}