using System;
using System.Globalization;
using System.Linq;
using NeoSharp.Application.Attributes;
using NeoSharp.Application.Client;
using NeoSharp.Core.Network;

namespace NeoSharp.Application.Controllers
{
    public class PromptNetworkController : IPromptController
    {
        #region Private fields

        private readonly IServerContext _serverContext;
        private readonly INetworkManager _networkManager;
        private readonly IConsoleHandler _consoleHandler;

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="serverContext">Server context</param>
        /// <param name="networkManager">Network manages</param>
        /// <param name="consoleHandler">Console writter</param>
        public PromptNetworkController(IServerContext serverContext, INetworkManager networkManager, IConsoleHandler consoleHandler)
        {
            _serverContext = serverContext;
            _networkManager = networkManager;
            _consoleHandler = consoleHandler;
        }

        [PromptCommand("network nodes", Category = "Network")]
        // ReSharper disable once UnusedMember.Local
        public void NetworkStatusCommand()
        {
            _consoleHandler.Write("Version: ");
            _consoleHandler.WriteLine("" + _serverContext.Version.Version, ConsoleOutputStyle.DarkRed);
            
            _consoleHandler.Write("Peers: ");
            _consoleHandler.WriteLine(_serverContext.ConnectedPeers.Count + "/" + _serverContext.MaxConnectedPeers, ConsoleOutputStyle.DarkRed);
            foreach (var entry in _serverContext.ConnectedPeers)
            {   
                var peer = entry.Value;
                
                _consoleHandler.Write("Peer: ");
                _consoleHandler.WriteLine(peer.EndPoint.ToString(), ConsoleOutputStyle.DarkRed);
                
                _consoleHandler.Write(" - Version: ");
                _consoleHandler.WriteLine(peer.Version.Version.ToString(), ConsoleOutputStyle.DarkRed);
                
                _consoleHandler.Write(" - Connected: ");
                _consoleHandler.WriteLine(peer.IsConnected.ToString(), ConsoleOutputStyle.DarkRed);
                
                _consoleHandler.Write(" - Ready: ");
                _consoleHandler.WriteLine(peer.IsReady.ToString(), ConsoleOutputStyle.DarkRed);
                
                _consoleHandler.Write(" - Connected: ");
                _consoleHandler.WriteLine(peer.ConnectionDate.ToString(CultureInfo.InvariantCulture), ConsoleOutputStyle.DarkRed);
            }
        }
        
        /// <summary>
        /// Start network
        /// </summary>
        [PromptCommand("network start", Category = "Network")]
        // ReSharper disable once UnusedMember.Local
        public void NetworkStartCommand()
        {
            _networkManager?.StartNetwork();
        }

        /// <summary>
        /// Stop network
        /// </summary>
        [PromptCommand("network stop", Category = "Network")]
        public void NetworkStopCommand()
        {
            _networkManager?.StopNetwork();
        }
    }
}
