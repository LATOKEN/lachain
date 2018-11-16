﻿using System;
using System.Threading;
using Phorkus.Core.Network.Proto;

namespace Phorkus.Core.Network
{
    public interface IMessageListener
    {
        event EventHandler<Message> OnMessageHandled;
        event EventHandler<IPeer> OnRateLimited;
        
        void StartFor(IPeer peer, CancellationToken cancellationToken);
    }
}