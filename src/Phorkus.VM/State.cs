using System;
using System.Collections.Generic;

namespace Phorkus.VM
{
    public class State
    {
        public Stack<byte[]> Stack { get; }
        public byte[] Instructions { get; }

        private int _currentInstructionIndex;

        public State(byte[] instructions)
        {
            this.Instructions = instructions;
            this.Stack = new Stack<byte[]>();
        }

        public byte GetCurrentInstruction()
        {
            return Instructions?[_currentInstructionIndex] ?? 0;
        }

        public virtual void SetInstrunctionIndex(int index)
        {
            _currentInstructionIndex = index;

            if (_currentInstructionIndex >= Instructions.Length)
            {
                Stop();
            }
        }

        public bool Stopped { get; private set; }

        public void Stop()
        {
            Stopped = true;
        }

        public void Step()
        {
            Step(1);
        }

        public void Step(int steps)
        {
            SetInstrunctionIndex(_currentInstructionIndex + steps);
        }

        public byte[] Sweep(int number)
        {
            var lastInstrunction = _currentInstructionIndex + number;
            if (lastInstrunction > Instructions.Length)
            {
                Stop();
            }

            var data = new byte[number];
            Array.Copy(Instructions, _currentInstructionIndex, data, 0, number);
            Step(number);

            return data;
        }

        public void StackPush(byte[] stackWord)
        {
            ThrowWhenPushStackOverflows(); 
            Stack.Push(stackWord);
        }

        private void ThrowWhenPushStackOverflows()
        {
            VerifyStackOverflow(0,1);
        }

        public void VerifyStackOverflow(int args, int returns)
        {
            if (Stack.Count - args + returns > MaxStacksize)
            {
                throw new Exception("Stack overflow, maximum size is " + MaxStacksize);
            }
        }

        public const int MaxStacksize = 1024;

        public byte[] StackPop()
        {
            return Stack.Pop();
        }


    }
}