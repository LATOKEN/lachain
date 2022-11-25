using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Lachain.Logger;
using Lachain.Consensus.CommonSubset;
using Lachain.Consensus.Messages;
using Lachain.Crypto;
using Lachain.Crypto.TPKE;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Consensus.MaliciousProtocol
{
    public class Spammer
    {
        private static readonly ILogger<Spammer> Logger = LoggerFactory.GetLoggerForClass<Spammer>();
        private readonly IConsensusBroadcaster _broadcaster;
        private readonly Thread _thread;
        private int _validators;
        private bool _terminated = false;
        private int _myId = -1;
        private bool _started = false;
        private int count = 0;
        private readonly long _era;
        public Spammer(IConsensusBroadcaster broadcaster, long era)
        {
            _broadcaster = broadcaster;
            _thread = new Thread(Spam);
            _era = era;
        }

        public void Initiate(int N)
        {
            _validators = N;
            _myId = _broadcaster.GetMyId();
            if (_myId == -1)
                throw new Exception("initialization failed");
        }

        public void StartSpam()
        {
            if (_started)
                throw new Exception("Started already");
            if (_myId == -1)
                throw new Exception("not ready");
            _thread.Start();
            _started = true;
        }

        public void Terminate()
        {
            _terminated = true;
            Logger.LogInformation($"Spammed {count} messages for era {_era} before termination");
        }

        public void Spam()
        {
            while (_terminated == false)
            {
                var msg = SpamHB(count);
                count++;
                for (int i = 0 ; i < _validators; i++)
                {
                    if (i != _myId)
                    {
                        _broadcaster.SendToValidator(msg, i);
                    }
                }
                if (count % 10000 == 0)
                {
                    Logger.LogInformation($"Spammed {count} messages for era {_era}");
                }
            }
        }

        private ConsensusMessage SpamHB(int id)
        {
            return new ConsensusMessage
            {
                Decrypted = new TPKEPartiallyDecryptedShareMessage
                {
                    ShareId = id
                }
            };
        }
    }
}