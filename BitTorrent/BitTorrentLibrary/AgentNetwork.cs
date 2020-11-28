//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Agent network I/O layer (WIP).
//
// Copyright 2020.
//
using System;
using System.Net;
using System.Net.Sockets;
namespace BitTorrentLibrary
{
    /// <summary>
    /// Agent connector is used to link the peer network socket listener/connect
    /// and an agent. It contains any data that is filled in and passed
    /// between them and also the callback that is called on a accept/connect.
    /// </summary>
    /// <param name="callBackData"></param>
    internal delegate void ConnectorCallBack(Object callBackData);
    internal struct AgentConnector
    {
        // Shallow copy connector
        public AgentConnector Copy()
        {
            return (AgentConnector)new AgentConnector
            {
                peerDetails = peerDetails,
                socket = socket,
                callBack = callBack
            };
        }
        public PeerDetails peerDetails;     // Connecting peer details
        public Socket socket;               // Connecting socket
        public ConnectorCallBack callBack;  // Connecting callback
    }
    internal interface IAgentNetwork
    {
        void AcceptAsyncHandler(IAsyncResult ar);
        void Connect(AgentConnector connector);
        void ConnectionAsyncHandler(IAsyncResult ar);
        PeerDetails GetConnectingPeerDetails(Socket socket);
        void ShutdownListener();
        void StartListening(AgentConnector connector);
    }
    internal class AgentNetwork : IAgentNetwork
    {
        private Socket _listenerSocket;                         // Connection listener socket
        internal static int listenPort = Host.DefaultPort;      // Listener port
        /// <summary>
        ///  Setup data and resources needed by AgentNetwork.
        /// </summary>
        public AgentNetwork()
        {
        }
        /// <summary>
        /// Asynchronous peer connection callback. When a connection
        /// is completed try to add it to the swarm via the connector 
        /// passed as a parameter.
        /// </summary>
        /// <param name="ar"></param>
        public void ConnectionAsyncHandler(IAsyncResult ar)
        {
            AgentConnector connector = (AgentConnector)ar.AsyncState;
            try
            {
                connector.socket.EndConnect(ar);
                connector.callBack(connector);
            }
            catch (Exception ex)
            {
                Log.Logger.Error("Error (Ignored): " + ex.Message);
                connector.socket?.Close(); ;
            }
        }
        /// <summary>
        /// Asynchronous remote peer connection callback. When a remote connection
        /// arrives try to add it to the swarm via the connector passed then prime 
        /// the accept callback for the next peers connection attempt.
        /// </summary>
        /// <param name="ar"></param>
        public void AcceptAsyncHandler(IAsyncResult ar)
        {
            AgentConnector connector = (AgentConnector)ar.AsyncState;
            try
            {
                connector.socket = _listenerSocket.EndAccept(ar);
                connector.callBack(connector.Copy());
                _listenerSocket.BeginAccept(new AsyncCallback(AcceptAsyncHandler), connector);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex);
                Log.Logger.Info("Port connection listener terminated.");
            }
        }
        /// <summary>
        /// Connect with remote peer and add it to the swarm when its callback handler is called.
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public void Connect(AgentConnector connector)
        {
            IPAddress localPeerIP = Dns.GetHostEntry("localhost").AddressList[0];
            IPAddress remotePeerIP = System.Net.IPAddress.Parse(connector.peerDetails.ip);
            connector.socket = new Socket(localPeerIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            connector.socket.BeginConnect(new IPEndPoint(remotePeerIP, connector.peerDetails.port), new AsyncCallback(ConnectionAsyncHandler), connector);
        }
        /// <summary>
        /// Return IP and Port of remote connection.
        /// </summary>
        /// <param name="socket"></param>
        /// <returns></returns>
        public PeerDetails GetConnectingPeerDetails(Socket socket)
        {
            PeerDetails peerDetails = new PeerDetails
            {
                ip = ((IPEndPoint)(socket.RemoteEndPoint)).Address.ToString(),
                port = ((IPEndPoint)(socket.RemoteEndPoint)).Port
            };
            return peerDetails;
        }
        /// <summary>
        /// Start asychronouse peer port connection listener.
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="port"></param>
        public void StartListening(AgentConnector connector)
        {
            try
            {
                _listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _listenerSocket.Bind(new IPEndPoint(IPAddress.Any, listenPort));
                _listenerSocket.Listen(100);
                _listenerSocket.BeginAccept(new AsyncCallback(AcceptAsyncHandler), connector);
                Log.Logger.Info("Port connection listener started...");
            }
            catch (Exception ex)
            {
                throw new Exception("Error:" + ex);
            }
        }
        /// <summary>
        /// Connect to listen port to shutdown listener task.
        /// </summary>
        public void ShutdownListener()
        {
            try
            {
                _listenerSocket?.Close();
                _listenerSocket = null;
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex);
            }
        }
    }
}