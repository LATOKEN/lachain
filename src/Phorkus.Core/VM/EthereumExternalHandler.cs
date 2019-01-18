using System.Collections.Generic;
using Phorkus.WebAssembly;

namespace Phorkus.Core.VM
{
    public class EthereumExternalHandler : IExternalHandler
    {
        private const string EthereumModule = "ethereum";
        
        /// <summary>
        /// Subtracts an amount to the gas counter
        /// </summary>
        /// <param name="amount">i64 the amount to subtract to the gas counter</param>
        public static void Handler_Ethereum_UseGas(long amount)
        {
        }
        
        /// <summary>
        /// Gets address of currently executing account and stores it in memory at the given offset.
        /// Trap:
        ///  * store to memory at resultOffset results in out of bounds access.
        /// </summary>
        /// <param name="resultOffset">i32ptr the memory offset at which the address is to be stored (address)</param>
        public static void Handler_Ethereum_GetAddress(int resultOffset)
        {
        }

        /// <summary>
        /// Gets balance of the given account and loads it into memory at the given offset.
        /// Trap:
        ///  * load from memory at addressOffset results in out of bounds access
        ///  * store to memory at resultOffset results in out of bounds access
        /// </summary>
        /// <param name="addressOffset">i32ptr the memory offset to load the address from (address)</param>
        /// <param name="resultOffset">i32ptr the memory offset to load the balance into (u128)</param>
        public static void Handler_Ethereum_GetExternalBalance(int addressOffset, int resultOffset)
        {
        }

        /// <summary>
        /// Gets the hash of one of the 256 most recent complete blocks.
        /// Trap:
        ///  * store to memory at resultOffset results in out of bounds access (also checked on failure)
        /// Note: in case of failure, the output memory pointed by resultOffset is unchanged. 
        /// </summary>
        /// <param name="blockNumber">i64 which block to load</param>
        /// <param name="resultOffset">i32ptr the memory offset to load the hash into (bytes32)</param>
        /// <returns>i32 Returns 0 on success and 1 on failure</returns>
        public static int Handler_Ethereum_GetBlockHash(long blockNumber, int resultOffset)
        {
            // Returns 0 on success and 1 on failure
            return 0;
        }

        /// <summary>
        /// Sends a message with arbitrary data to a given address path.
        /// Trap:
        ///  * load address from memory at addressOffset results in out of bounds access
        ///  * load u128 from memory at valueOffset results in out of bounds access
        ///  * load dataLength number of bytes from memory at dataOffset results in out of bounds access
        /// </summary>
        /// <param name="gasLimit">i64 the gas limit</param>
        /// <param name="addressOffset">i32ptr the memory offset to load the address from (address)</param>
        /// <param name="valueOffset">i32ptr the memory offset to load the value from (u128)</param>
        /// <param name="dataOffset">i32ptr the memory offset to load data from (bytes)</param>
        /// <param name="dataLength">i32 the length of data</param>
        /// <returns>i32 Returns 0 on success, 1 on failure and 2 on revert</returns>
        public static int Handler_Ethereum_Call(long gasLimit, int addressOffset, int valueOffset, int dataOffset, int dataLength)
        {
            // Returns 0 on success, 1 on failure and 2 on revert
            return 0;
        }
        
        /// <summary>
        /// Copies the input data in current environment to memory. This pertains to the input data passed with the message call instruction or transaction.
        /// Trap:
        ///  * load length number of bytes from input data buffer at dataOffset results in out of bounds access
        ///  * store length number of bytes to memory at resultOffset results in out of bounds access
        /// </summary>
        /// <param name="resultOffset">i32ptr the memory offset to load data into (bytes)</param>
        /// <param name="dataOffset">i32 the offset in the input data</param>
        /// <param name="dataLength">i32 the length of data to copy</param>
        public static void Handler_Ethereum_CallDataCopy(int resultOffset, int dataOffset, int dataLength)
        {
        }

        /// <summary>
        /// Get size of input data in current environment. This pertains to the input data passed with the message call instruction or transaction.
        /// </summary>
        /// <returns>i32 call data size</returns>
        public static int Handler_Ethereum_GetCallDataSize()
        {
            // Get size of input data in current environment. This pertains to the input data passed with the message call instruction or transaction.
            return 0;
        }

        /// <summary>
        /// Message-call into this account with an alternative account's code.
        /// Trap:
        ///  * load address from memory at addressOffset results in out of bounds access
        ///  * load u128 from memory at valueOffset results in out of bounds access
        ///  * load dataLength number of bytes from memory at dataOffset results in out of bounds access
        /// </summary>
        /// <param name="gasLimit">i64 the gas limit</param>
        /// <param name="addressOffset">i32ptr the memory offset to load the address from (address)</param>
        /// <param name="valueOffset">i32ptr the memory offset to load the value from (u128)</param>
        /// <param name="dataOffset">i32ptr the memory offset to load data from (bytes)</param>
        /// <param name="dataLength">i32 the length of data</param>
        /// <returns>i32 Returns 0 on success, 1 on failure and 2 on revert</returns>
        public static int Handler_Ethereum_CallCode(long gasLimit, int addressOffset, int valueOffset, int dataOffset, int dataLength)
        {
            // Returns 0 on success, 1 on failure and 2 on revert
            return 0;
        }
        
        /// <summary>
        /// Message-call into this account with an alternative account’s code, but persisting the current values for sender and value.
        /// Trap:
        ///  * load address from memory at addressOffset results in out of bounds access
        ///  * load dataLength number of bytes from memory at dataOffset results in out of bounds access
        /// </summary>
        /// <param name="gasLimit">i64 the gas limit</param>
        /// <param name="addressOffset">i32ptr the memory offset to load the address from (address)</param>
        /// <param name="dataOffset">i32ptr the memory offset to load data from (bytes)</param>
        /// <param name="dataLength">i32 the length of data</param>
        /// <returns>i32 Returns 0 on success, 1 on failure and 2 on revert</returns>
        public static int Handler_Ethereum_CallDelegate(long gasLimit, int addressOffset, int dataOffset, int dataLength)
        {
            // Returns 0 on success, 1 on failure and 2 on revert
            return 0;
        }

        /// <summary>
        /// Sends a message with arbitrary data to a given address path, but disallow state modifications. This includes log, create, selfdestruct and call with a non-zero value.
        /// Trap:
        ///  * load address from memory at addressOffset results in out of bounds access
        ///  * load dataLength number of bytes from memory at dataOffset results in out of bounds access
        /// </summary>
        /// <param name="gasLimit">i64 the gas limit</param>
        /// <param name="addressOffset">i32ptr the memory offset to load the address from (address)</param>
        /// <param name="dataOffset">i32ptr the memory offset to load data from (bytes)</param>
        /// <param name="dataLength">i32 the length of data</param>
        /// <returns>i32 Returns 0 on success, 1 on failure and 2 on revert</returns>
        public static int Handler_Ethereum_CallStatic(long gasLimit, int addressOffset, int dataOffset, int dataLength)
        {
            // Returns 0 on success, 1 on failure and 2 on revert
            return 0;
        }

        /// <summary>
        /// Store 256-bit a value in memory to persistent storage
        /// Trap:
        ///  * load bytes32 from memory at pathOffset results in out of bounds access
        ///  * load bytes32 from memory at valueOffset results in out of bounds access
        /// </summary>
        /// <param name="pathOffset">i32ptr the memory offset to load the path from (bytes32)</param>
        /// <param name="valueOffset">i32ptr the memory offset to load the value from (bytes32)</param>
        public static void Handler_Ethereum_StorageStore(int pathOffset, int valueOffset)
        {
        }

        /// <summary>
        /// Loads a 256-bit a value to memory from persistent storage.
        /// Trap:
        ///  * load bytes32 from memory at pathOffset results in out of bounds access
        ///  * store bytes32 to memory at resultOffset results in out of bounds access
        /// </summary>
        /// <param name="pathOffset">i32ptr the memory offset to load the path from (bytes32)</param>
        /// <param name="valueOffset">i32ptr the memory offset to store the result at (bytes32)</param>
        public static void Handler_Ethereum_StorageLoad(int pathOffset, int valueOffset)
        {
        }

        /// <summary>
        /// Gets caller address and loads it into memory at the given offset. This is the address of the account that is directly responsible for this execution.
        /// Trap:
        ///  * store address to memory at resultOffset results in out of bounds access
        /// </summary>
        /// <param name="resultOffset">i32ptr the memory offset to load the address into (address)</param>
        public static void Handler_Ethereum_GetCaller(int resultOffset)
        {
        }

        /// <summary>
        /// Gets the deposited value by the instruction/transaction responsible for this execution and loads it into memory at the given location.
        /// Trap:
        ///  * store u128 to memory at resultOffset results in out of bounds access
        /// </summary>
        /// <param name="resultOffset">resultOffset i32ptr the memory offset to load the value into (u128)</param>
        public static void Handler_Ethereum_GetCallValue(int resultOffset)
        {
        }

        /// <summary>
        /// Copies the code running in current environment to memory.
        /// Trap:
        ///  * load length number of bytes from the current code buffer at codeOffset results in out of bounds access
        ///  * store length number of bytes to memory at resultOffset results in out of bounds access
        /// </summary>
        /// <param name="resultOffset">i32ptr the memory offset to load the result into (bytes)</param>
        /// <param name="codeOffset">the offset within the code</param>
        /// <param name="codeLength">i32 the length of code to copy</param>
        public static void Handler_Ethereum_CodeCopy(int resultOffset, int codeOffset, int codeLength)
        {
        }

        /// <summary>
        /// Gets the size of code running in current environment.
        /// </summary>
        /// <returns>i32 the size of the code</returns>
        public static int Handler_Ethereum_GetCodeSize()
        {
            return 0;
        }

        /// <summary>
        /// Gets the block’s beneficiary address and loads into memory.
        /// Trap:
        ///  * store address to memory at resultOffset results in out of bounds access
        /// </summary>
        /// <param name="resultOffset">i32ptr the memory offset to load the coinbase address into (address)</param>
        public static void Handler_Ethereum_GetBlockCoinbase(int resultOffset)
        {
        }

        /// <summary>
        /// Creates a new contract with a given value.
        /// Trap:
        ///  * load u128 from memory at valueOffset results in out of bounds access
        ///  * load dataLength number of bytes from memory at dataOffset results in out of bounds access
        ///  * store address to memory at resultOffset results in out of bounds access
        /// Note: create will clear the return buffer in case of success or may fill it with data coming from revert.
        /// </summary>
        /// <param name="valueOffset">i32ptr the memory offset to load the value from (u128)</param>
        /// <param name="dataOffset">i32ptr the memory offset to load the code for the new contract from (bytes)</param>
        /// <param name="dataLength">i32 the data length</param>
        /// <param name="resultOffset">i32ptr the memory offset to write the new contract address to (address)</param>
        /// <returns>i32 Returns 0 on success, 1 on failure and 2 on revert</returns>
        public static int Handler_Ethereum_Create(int valueOffset, int dataOffset, int dataLength, int resultOffset)
        {
            return 0;
        }

        /// <summary>
        /// Get the block’s difficulty.
        /// Trap:
        ///  * store u256 to memory at resultOffset results in out of bounds access
        /// </summary>
        /// <param name="resultOffset">i32ptr the memory offset to load the difficulty into (u256)</param>
        public static void Handler_Ethereum_GetBlockDifficulty(int resultOffset)
        {
        }

        /// <summary>
        /// Copies the code of an account to memory.
        /// Trap:
        ///  * load address from memory at addressOffset results in out of bounds access
        ///  * load length number of bytes from the account code buffer at codeOffset results in out of bounds access
        ///  * store length number of bytes to memory at resultOffset results in out of bounds access
        /// </summary>
        /// <param name="addressOffset">i32ptr the memory offset to load the address from (address)</param>
        /// <param name="resultOffset">i32ptr the memory offset to load the result into (bytes)</param>
        /// <param name="codeOffset">i32 the offset within the code</param>
        /// <param name="codeLength">i32 the length of code to copy</param>
        public static void Handler_Ethereum_ExternalCodeCopy(int addressOffset, int resultOffset, int codeOffset, int codeLength)
        {
        }

        /// <summary>
        /// Get size of an account’s code.
        /// Trap:
        ///  * load address from memory at addressOffset results in out of bounds access
        /// </summary>
        /// <param name="addressOffset">i32ptr the memory offset to load the address from (address)</param>
        /// <returns>i32 external code size</returns>
        public static int Handler_Ethereum_GetExternalCodeSize(int addressOffset)
        {
            return 0;
        }
        
        /// <summary>
        /// Returns the current gas counter.
        /// </summary>
        /// <returns>i64 gas left</returns>
        public static long Handler_Ethereum_GetGasLeft()
        {
            return 0;
        }

        /// <summary>
        /// Get the block’s gas limit.
        /// </summary>
        /// <returns>i64 block gas limit</returns>
        public static long Handler_Ethereum_GetBlockGasLimit()
        {
            return 0;
        }

        /// <summary>
        /// Gets price of gas in current environment.
        /// Trap:
        ///  * store u128 to memory at resultOffset results in out of bounds access
        /// </summary>
        /// <param name="resultOffset">i32ptr the memory offset to write the value to (u128)</param>
        public static void Handler_Ethereum_GetTxGasPrice(int resultOffset)
        {
        }
        
        /// <summary>
        /// Creates a new log in the current environment.
        /// Trap:
        ///  * load dataLength number of bytes from memory at dataOffset results in out of bounds access
        ///  * numberOfTopics is greater than 4
        ///  * load bytes32 from memory at topic1 results in out of bounds access
        ///  * load bytes32 from memory at topic2 results in out of bounds access
        ///  * load bytes32 from memory at topic3 results in out of bounds access
        ///  * load bytes32 from memory at topic4 results in out of bounds access
        /// </summary>
        /// <param name="dataOffset">i32ptr the memory offset to load data from (bytes)</param>
        /// <param name="dataLength">i32 the data length</param>
        /// <param name="numberOfTopics">i32 the number of topics following (0 to 4)</param>
        /// <param name="topic1Offset">i32ptr the memory offset to load topic1 from (bytes32)</param>
        /// <param name="topic2Offset">i32ptr the memory offset to load topic2 from (bytes32)</param>
        /// <param name="topic3Offset">i32ptr the memory offset to load topic3 from (bytes32)</param>
        /// <param name="topic4Offset">i32ptr the memory offset to load topic4 from (bytes32)</param>
        public static void Handler_Ethereum_Log(int dataOffset, int dataLength, int numberOfTopics, int topic1Offset, int topic2Offset, int topic3Offset, int topic4Offset)
        {
        }

        /// <summary>
        /// Get the block’s number.
        /// </summary>
        /// <returns>i64 block number</returns>
        public static long Handler_Ethereum_GetBlockNumber()
        {
            return 0;
        }
        
        /// <summary>
        /// Gets the execution's origination address and loads it into memory at the given offset. This is the sender of original transaction; it is never an account with non-empty associated code.
        /// Trap:
        ///  * store address to memory at resultOffset results in out of bounds access
        /// </summary>
        /// <param name="resultOffset">i32ptr the memory offset to load the origin address from (address)</param>
        public static void Handler_Ethereum_GetTxOrigin(int resultOffset)
        {
        }

        /// <summary>
        /// Set the returning output data for the execution. This will cause a trap and the execution will be aborted immediately.
        /// Trap:
        ///  * load dataLength number of bytes from memory at dataOffset results in out of bounds access
        /// </summary>
        /// <param name="dataOffset">i32ptr the memory offset of the output data (bytes)</param>
        /// <param name="dataLength">i32 the length of the output data</param>
        public static void Handler_Ethereum_Finish(int dataOffset, int dataLength)
        {
        }

        /// <summary>
        /// Set the returning output data for the execution. This will cause a trap and the execution will be aborted immediately.
        /// Trap:
        ///  * load dataLength number of bytes from memory at dataOffset results in out of bounds access
        /// </summary>
        /// <param name="dataOffset">i32ptr the memory offset of the output data (bytes)</param>
        /// <param name="dataLength">i32 the length of the output data</param>
        public static void Handler_Ethereum_Revert(int dataOffset, int dataLength)
        {
        }

        /// <summary>
        /// Get size of current return data buffer to memory. This contains the return data from the last executed call, callCode, callDelegate, callStatic or create.
        /// Note: create only fills the return data buffer in case of a failure.
        /// </summary>
        /// <returns>i32 return data size</returns>
        public static int Handler_Ethereum_GetReturnDataSize()
        {
            return 0;
        }

        /// <summary>
        /// Copies the current return data buffer to memory. This contains the return data from last executed call, callCode, callDelegate, callStatic or create.
        /// Trap:
        ///  * load length number of bytes from input data buffer at dataOffset results in out of bounds access
        ///  * store length number of bytes to memory at resultOffset results in out of bounds access
        /// Note: create only fills the return data buffer in case of a failure.
        /// </summary>
        /// <param name="resultOffset">i32ptr the memory offset to load data into (bytes)</param>
        /// <param name="dataOffset">i32 the offset in the return data</param>
        /// <param name="dataLength">i32 the length of data to copy</param>
        public static void Handler_Ethereum_ReturnDataCopy(int resultOffset, int dataOffset, int dataLength)
        {
        }
        
        /// <summary>
        /// Mark account for later deletion and give the remaining balance to the specified beneficiary address. This will cause a trap and the execution will be aborted immediately.
        /// Trap:
        ///  * load address from memory at addressOffset results in out of bounds access
        /// </summary>
        /// <param name="addressOffset">i32ptr the memory offset to load the address from (address)</param>
        public static void Handler_Ethereum_SelfDestruct(int addressOffset)
        {
        }
        
        /// <summary>
        /// Get the block’s timestamp.
        /// </summary>
        /// <returns>i64 block timestamp</returns>
        public static long Handler_Ethereum_GetBlockTimestamp()
        {
            return 0;
        }
        
        /// <summary>
        /// Gets balance of the given account and loads it into memory at the given offset.
        /// Trap:
        ///  * load from memory at addressOffset results in out of bounds access
        ///  * store to memory at resultOffset results in out of bounds access
        /// </summary>
        /// <param name="addressOffset">i32ptr the memory offset to load the address from (address)</param>
        /// <param name="resultOffset">i32ptr the memory offset to load the balance into (u128)</param>
        public static void Handler_Ethereum_GetBalance(int addressOffset, int resultOffset)
        {
        }
        
        public IEnumerable<FunctionImport> GetFunctionImports()
        {
            return new[]
            {
                new FunctionImport(EthereumModule, "useGas", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_UseGas))),
                new FunctionImport(EthereumModule, "getAddress", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_GetAddress))),
                new FunctionImport(EthereumModule, "getExternalBalance", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_GetExternalBalance))),
                new FunctionImport(EthereumModule, "getBlockHash", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_GetBlockHash))),
                new FunctionImport(EthereumModule, "call", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_Call))),
                new FunctionImport(EthereumModule, "callDataCopy", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_CallDataCopy))),
                new FunctionImport(EthereumModule, "getCallDataSize", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_GetCallDataSize))),
                new FunctionImport(EthereumModule, "callCode", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_CallCode))),
                new FunctionImport(EthereumModule, "callDelegate", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_CallDelegate))),
                new FunctionImport(EthereumModule, "callStatic", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_CallStatic))),
                new FunctionImport(EthereumModule, "storageStore", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_StorageStore))),
                new FunctionImport(EthereumModule, "storageLoad", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_StorageLoad))),
                new FunctionImport(EthereumModule, "getCaller", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_GetCaller))),
                new FunctionImport(EthereumModule, "getCallValue", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_GetCallValue))),
                new FunctionImport(EthereumModule, "codeCopy", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_CodeCopy))),
                new FunctionImport(EthereumModule, "getCodeSize", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_GetCodeSize))),
                new FunctionImport(EthereumModule, "getBlockCoinbase", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_GetBlockCoinbase))),
                new FunctionImport(EthereumModule, "create", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_Create))),
                new FunctionImport(EthereumModule, "getBlockDifficulty", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_GetBlockDifficulty))),
                new FunctionImport(EthereumModule, "externalCodeCopy", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_ExternalCodeCopy))),
                new FunctionImport(EthereumModule, "getExternalCodeSize", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_GetExternalCodeSize))),
                new FunctionImport(EthereumModule, "getGasLeft", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_GetGasLeft))),
                new FunctionImport(EthereumModule, "getBlockGasLimit", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_GetBlockGasLimit))),
                new FunctionImport(EthereumModule, "getTxGasPrice", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_GetTxGasPrice))),
                new FunctionImport(EthereumModule, "log", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_Log))),
                new FunctionImport(EthereumModule, "getBlockNumber", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_GetBlockNumber))),
                new FunctionImport(EthereumModule, "getTxOrigin", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_GetTxOrigin))),
                new FunctionImport(EthereumModule, "finish", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_Finish))),
                new FunctionImport(EthereumModule, "revert", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_Revert))),
                new FunctionImport(EthereumModule, "getReturnDataSize", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_GetReturnDataSize))),
                new FunctionImport(EthereumModule, "returnDataCopy", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_ReturnDataCopy))),
                new FunctionImport(EthereumModule, "selfDestruct", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_SelfDestruct))),
                new FunctionImport(EthereumModule, "getBlockTimestamp", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_GetBlockTimestamp))),
                new FunctionImport(EthereumModule, "getBalance", typeof(EthereumExternalHandler).GetMethod(nameof(Handler_Ethereum_GetBalance)))
            };
        }
    }
}