using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace NeoSharp.VM
{
    public abstract class IExecutionEngine : IDisposable
    {
        #region Public fields from arguments

        /// <summary>
        /// Gas ratio for computation
        /// </summary>
        public const ulong GasRatio = 100000;

        /// <summary>
        /// Trigger
        /// </summary>
        public ETriggerType Trigger { get; private set; }

        /// <summary>
        /// Interop service
        /// </summary>
        public InteropService InteropService { get; private set; }

        /// <summary>
        /// Script table
        /// </summary>
        public IScriptTable ScriptTable { get; private set; }

        /// <summary>
        /// Logger
        /// </summary>
        public ExecutionEngineLogger Logger { get; private set; }

        /// <summary>
        /// Message Provider
        /// </summary>
        public IMessageProvider MessageProvider { get; private set; }

        #endregion

        /// <summary>
        /// Virtual Machine State
        /// </summary>
        public abstract EVMState State { get; }

        /// <summary>
        /// InvocationStack
        /// </summary>
        public abstract IStack<IExecutionContext> InvocationStack { get; }

        /// <summary>
        /// ResultStack
        /// </summary>
        public abstract IStackItemsStack ResultStack { get; }

        /// <summary>
        /// Is disposed
        /// </summary>
        public abstract bool IsDisposed { get; }

        /// <summary>
        /// Consumed Gas
        /// </summary>
        public abstract uint ConsumedGas { get; }

        #region Shortcuts

        public IExecutionContext CurrentContext
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return InvocationStack.TryPeek(0, out IExecutionContext i) ? i : null; }
        }

        public IExecutionContext CallingContext
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return InvocationStack.TryPeek(1, out IExecutionContext i) ? i : null; }
        }

        public IExecutionContext EntryContext
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return InvocationStack.TryPeek(-1, out IExecutionContext i) ? i : null; }
        }

        #endregion

        /// <summary>
        /// For unit testing only
        /// </summary>
        protected IExecutionEngine() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="e">Arguments</param>
        protected IExecutionEngine(ExecutionEngineArgs e)
        {
            if (e == null) return;

            InteropService = e.InteropService;
            ScriptTable = e.ScriptTable;
            MessageProvider = e.MessageProvider;
            Trigger = e.Trigger;
            Logger = e.Logger;
        }

        #region Load Script

        /// <summary>
        /// Load script
        /// </summary>
        /// <param name="script">Script</param>
        /// <returns>Script index in script cache</returns>
        public abstract int LoadScript(byte[] script);

        /// <summary>
        /// Load script
        /// </summary>
        /// <param name="scriptIndex">Script Index</param>
        /// <returns>True if is loaded</returns>
        public abstract bool LoadScript(int scriptIndex);

        #endregion

        #region Execution

        /// <summary>
        /// Increase gas
        /// </summary>
        /// <param name="gas">Gas</param>
        public abstract bool IncreaseGas(uint gas);

        /// <summary>
        /// Clean Execution engine state
        /// </summary>
        /// <param name="iteration">Iteration</param>
        public abstract void Clean(uint iteration = 0);

        /// <summary>
        /// Execute (until x of Gas)
        /// </summary>
        /// <param name="gas">Gas</param>
        public abstract bool Execute(uint gas = uint.MaxValue);

        /// <summary>
        /// Step Into
        /// </summary>
        /// <param name="steps">Steps</param>
        public abstract void StepInto(int steps = 1);

        /// <summary>
        /// Step Out
        /// </summary>
        public abstract void StepOut();

        /// <summary>
        /// Step Over
        /// </summary>
        public abstract void StepOver();

        #endregion

        #region Creates

        /// <summary>
        /// Create Map StackItem
        /// </summary>
        public abstract IMapStackItem CreateMap();

        /// <summary>
        /// Create Array StackItem
        /// </summary>
        /// <param name="items">Items</param>
        public abstract IArrayStackItem CreateArray(IEnumerable<IStackItem> items = null);

        /// <summary>
        /// Create Struct StackItem
        /// </summary>
        /// <param name="items">Items</param>
        public abstract IArrayStackItem CreateStruct(IEnumerable<IStackItem> items = null);

        /// <summary>
        /// Create ByteArrayStackItem
        /// </summary>
        /// <param name="data">Buffer</param>
        public abstract IByteArrayStackItem CreateByteArray(byte[] data);

        /// <summary>
        /// Create InteropStackItem
        /// </summary>
        /// <param name="obj">Object</param>
        public abstract IInteropStackItem CreateInterop(object obj);

        /// <summary>
        /// Create BooleanStackItem
        /// </summary>
        /// <param name="value">Value</param>
        public abstract IBooleanStackItem CreateBool(bool value);

        /// <summary>
        /// Create IntegerStackItem
        /// </summary>
        /// <param name="value">Value</param>
        public abstract IIntegerStackItem CreateInteger(int value);

        /// <summary>
        /// Create IntegerStackItem
        /// </summary>
        /// <param name="value">Value</param>
        public abstract IIntegerStackItem CreateInteger(long value);

        /// <summary>
        /// Create IntegerStackItem
        /// </summary>
        /// <param name="value">Value</param>
        public abstract IIntegerStackItem CreateInteger(BigInteger value);

        /// <summary>
        /// Create IntegerStackItem
        /// </summary>
        /// <param name="value">Value</param>
        public abstract IIntegerStackItem CreateInteger(byte[] value);

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Dispose logic
        /// </summary>
        /// <param name="disposing">False for cleanup native objects</param>
        protected virtual void Dispose(bool disposing) { }

        /// <summary>
        /// Destructor
        /// </summary>
        ~IExecutionEngine()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // uncomment the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}