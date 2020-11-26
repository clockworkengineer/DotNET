//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Peer to peer network I/O layer (WIP).
//
// Copyright 2020.
//
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Text;
namespace BitTorrentLibrary
{
    /// <summary>
    /// Peer connector is used to link the peer network socket listener/connect
    /// and an agent. It contains any data that is filled in and passed
    /// between them and also the callback that is called on a accept/connect.
    /// </summary>
    /// <param name="callBackData"></param>
    internal delegate void ConnectorCallBack(Object callBackData);
    internal struct PeerConnector
    {
        // Shallow copy connector
        public PeerConnector Copy()
        {
            return (PeerConnector)new PeerConnector
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
    internal class PeerNetwork
    {
        private static Socket _listenerSocket;                  // Connection listener socket
        private int _bytesRead;                                 // Bytes read in read request
        private bool _lengthRead;                               // == true packet length has been read
        private Socket _socket;                                 // Socket for I/O
        internal static int listenPort = Host.DefaultPort;      // Listener port
        public byte[] ReadBuffer { get; set; }                  // Read buffer
        public int PacketLength { get; set; }                   // Current packet length

        /// <summary>
        /// Throw an exception if an send error is reported.The error can either
        /// be because the errorReported boolean is true or an actual socket error
        /// reported has been reported by the system.
        /// </summary>
        /// <param name="errorReported"></param>
        /// <param name="socketError"></param>
        void ThrowOnError(string message, bool errorReported, SocketError socketError)
        {
            if (errorReported || socketError != SocketError.Success)
            {
                if (socketError != SocketError.Success)
                {
                    message += "Socket Error: " + socketError.ToString();
                }
                throw new Exception(message);
            }
        }
        /// <summary>
        /// Asynchronous peer connection callback. When a connection
        /// is completed try to add it to the swarm via the connector 
        /// passed as a parameter.
        /// </summary>
        /// <param name="ar"></param>
        private static void ConnectionAsyncHandler(IAsyncResult ar)
        {
            PeerConnector connector = (PeerConnector)ar.AsyncState;
            try
            {
                connector.socket.EndConnect(ar);
                connector.callBack(connector);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex);
                Log.Logger.Error("Error (Ignored): " + ex.Message);
                connector.socket?.Close();;
            }
        }
        /// <summary>
        /// Asynchronous remote peer connection callback. When a remote connection
        /// arrives try to add it to the swarm via the connector passed then prime 
        /// the accept callback for the next peers connection attempt.
        /// </summary>
        /// <param name="ar"></param>
        public static void AcceptAsyncHandler(IAsyncResult ar)
        {
            PeerConnector connector = (PeerConnector)ar.AsyncState;
            try
            {
                connector.socket = _listenerSocket.EndAccept(ar);
                connector.callBack(connector.Copy());
                _listenerSocket.BeginAccept(new AsyncCallback(AcceptAsyncHandler), connector);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex);
                Log.Logger.Debug("Error (Ignored): " + ex.Message);
                Log.Logger.Info("Port connection listener terminated.");
            }

        }
        /// <summary>
        /// Peer read packet asynchronous callback.
        /// </summary>
        /// <param name="readAsyncState">Read async state.</param>
        private void ReadAsyncHandler(IAsyncResult readAsyncState)
        {
            Peer remotePeer = (Peer)readAsyncState.AsyncState;
            try
            {
                int bytesRead = _socket.EndReceive(readAsyncState, out SocketError socketError);
                ThrowOnError("Socket recieve error.", (bytesRead <= 0) || !_socket.Connected, socketError);
                _bytesRead += bytesRead;
                if (!_lengthRead)
                {
                    if (_bytesRead == Constants.SizeOfUInt32)
                    {
                        PacketLength = (int)Util.UnPackUInt32(ReadBuffer, 0);
                        _lengthRead = true;
                        _bytesRead = 0;
                        if (PacketLength > ReadBuffer.Length)
                        {
                            Log.Logger.Debug("Resizing readBuffer ...");
                            ReadBuffer = new byte[PacketLength];
                        }
                    }
                }
                else if (_bytesRead == PacketLength)
                {
                    remotePeer.protocolHandler(remotePeer);
                    _lengthRead = false;
                    _bytesRead = 0;
                    PacketLength = Constants.SizeOfUInt32;
                }
                _socket.BeginReceive(ReadBuffer, _bytesRead, (PacketLength - _bytesRead), SocketFlags.None, new AsyncCallback(ReadAsyncHandler), remotePeer);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex);
                Log.Logger.Error("Error (Ignored): " + ex.Message);
                Log.Logger.Info($"Read packet handler {Encoding.ASCII.GetString(remotePeer.RemotePeerID)} terminated.");
                remotePeer.Close();
            }
        }
        /// <summary>
        ///  Setup data and resources needed by PeerNetwork.
        /// </summary>
        public PeerNetwork(Socket remotePeerSocket)
        {
            ReadBuffer = new byte[Constants.BlockSize + (2 * Constants.SizeOfUInt32) + 1]; // Maximum possible packet size
            _socket = remotePeerSocket;
        }
        /// <summary>
        /// Connect with remote peer and add it to the swarm when its callback handler is called.
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public static void Connect(PeerConnector connector)
        {
            IPAddress localPeerIP = Dns.GetHostEntry("localhost").AddressList[0];
            IPAddress remotePeerIP = System.Net.IPAddress.Parse(connector.peerDetails.ip);
            connector.socket = new Socket(localPeerIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            connector.socket.BeginConnect(new IPEndPoint(remotePeerIP, connector.peerDetails.port), new AsyncCallback(ConnectionAsyncHandler), connector);

        }
        /// <summary>
        /// Send packet to remote peer. Again do not check for connected as used in intial handshake with peer and
        /// when not checks happen at a higher level.
        /// </summary>
        /// <param name="buffer">Buffer.</param>
        public void Write(byte[] buffer)
        {
            int bytesWritten = _socket.Send(buffer, 0, buffer.Length, SocketFlags.None, out SocketError socketError);
            ThrowOnError("Socket send error.", (bytesWritten <= 0) || !_socket.Connected, socketError);
        }
        /// <summary>
        /// Read direct packet from remote peer. Note this only used in the initial handshake with a peer
        /// so it will not been connected.
        /// </summary>
        /// <returns>The read.</returns>
        /// <param name="buffer">Buffer.</param>
        /// <param name="length">Length.</param>
        public int Read(byte[] buffer, int length)
        {
            int bytesRead = _socket.Receive(buffer, 0, length, SocketFlags.None, out SocketError socketError);
            ThrowOnError("Socket recieve error.", (bytesRead <= 0) || !_socket.Connected, socketError);
            return bytesRead;
        }
        /// <summary>
        /// Start Async reading of network socket.
        /// </summary>
        /// <param name="remotePeer"></param>
        public void StartReads(Peer remotePeer)
        {
            Log.Logger.Info($"Read packet handler {Encoding.ASCII.GetString(remotePeer.RemotePeerID)} started...");
            _socket.BeginReceive(ReadBuffer, 0, Constants.SizeOfUInt32, SocketFlags.None, new AsyncCallback(ReadAsyncHandler), remotePeer);
        }
        /// <summary>
        /// Close socket connection
        /// </summary>
        public void Close()
        {
            if (_socket.Connected)
            {
                _socket.Close();
            }
            _socket = null;
        }
        /// <summary>
        /// Return IP and Port of remote connection.
        /// </summary>
        /// <param name="socket"></param>
        /// <returns></returns>
        public static PeerDetails GetConnectingPeerDetails(Socket socket)
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
        public static void StartListening(PeerConnector connector)
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
        public static void ShutdownListener()
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
