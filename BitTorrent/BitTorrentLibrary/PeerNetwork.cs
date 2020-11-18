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
    internal static class SocketExtensions
    {
        /// <summary>
        /// Connects the specified socket with a timeout.
        /// </summary>
        /// <param name="socket">The socket.</param>
        /// <param name="endpoint">The IP endpoint.</param>
        /// <param name="timeout">The timeout.</param>
        public static void Connect(this Socket socket, EndPoint endpoint, TimeSpan timeout)
        {
            var result = socket.BeginConnect(endpoint, null, null);
            bool success = result.AsyncWaitHandle.WaitOne(timeout, true);
            if (success)
            {
                socket.EndConnect(result);
            }
            else
            {
                socket.Close();
                throw new SocketException((int)SocketError.TimedOut);
            }
        }
    }
    /// <summary>
    /// Peer to peer network IO code.
    /// </summary>
    internal class PeerNetwork
    {
        private static Socket _listenerSocket;                  // Connection listener socket
        private int _bytesRead;                                 // Bytes read in read request
        private bool _lengthRead;                               // == true packet length has been read
        internal static int listenPort = Host.DefaultPort;      // Listener port
        public byte[] ReadBuffer { get; set; }                  // Read buffer
        public int PacketLength { get; set; }                   // Current packet length
        public Socket PeerSocket { get; set; }                  // Socket for I/O
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
        /// Asynchronous remote peer connection callback. When a remote connection
        /// arrives try to add it to the swarm then prime the callback for the next
        /// peers connection attempt.
        /// </summary>
        /// <param name="ar"></param>
        public static void AcceptConnectionAsyncHandler(IAsyncResult ar)
        {
            Agent agent = (Agent)ar.AsyncState;
            try
            {
                Socket acceptedSocket = _listenerSocket.EndAccept(ar);
                agent.AddRemoteConnectedPeerToSpawn(acceptedSocket);
                _listenerSocket.BeginAccept(new AsyncCallback(AcceptConnectionAsyncHandler), agent);
            }
            catch (Exception ex)
            {
                Log.Logger.Debug("BitTorrent (PeerNetwork) Error (Ignored): "+ex.Message);
                Log.Logger.Info("(PeerNetwork) Port connection listener terminated.");
            }
        }
        /// <summary>
        /// Peer read packet asynchronous callback.
        /// </summary>
        /// <param name="readAsyncState">Read async state.</param>
        private void ReadPacketAsyncHandler(IAsyncResult readAsyncState)
        {
            Peer remotePeer = (Peer)readAsyncState.AsyncState;
            try
            {
                int bytesRead = PeerSocket.EndReceive(readAsyncState, out SocketError socketError);
                ThrowOnError("Socket recieve error.", (bytesRead <= 0) || !PeerSocket.Connected, socketError);
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
                            Log.Logger.Debug("(PeerNetwork) Resizing readBuffer ...");
                            ReadBuffer = new byte[PacketLength];
                        }
                    }
                }
                else if (_bytesRead == PacketLength)
                {
                    PWP.RemotePeerMessageProcess(remotePeer);
                    _lengthRead = false;
                    _bytesRead = 0;
                    PacketLength = Constants.SizeOfUInt32;
                }
                PeerSocket.BeginReceive(ReadBuffer, _bytesRead, (PacketLength - _bytesRead), SocketFlags.None, new AsyncCallback(ReadPacketAsyncHandler), remotePeer);
            }
            catch (Exception ex)
            {
                Log.Logger.Debug("BitTorrent (PeerNetwork) Error (Ignored): "+ex.Message);
                Log.Logger.Info($"(PeerNetwork) Read packet handler {Encoding.ASCII.GetString(remotePeer.RemotePeerID)} terminated.");
                remotePeer.QueueForClosure();
            }
        }
        /// <summary>
        ///  Setup data and resources needed by PeerNetwork.
        /// </summary>
        public PeerNetwork(Socket remotePeerSocket)
        {
            ReadBuffer = new byte[Constants.BlockSize + (2 * Constants.SizeOfUInt32) + 1]; // Maximum possible packet size
            PeerSocket = remotePeerSocket;
        }
        /// <summary>
        /// Connect with remote peer and return the socket.
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public static Socket Connect(string ip, int port)
        {
            IPAddress localPeerIP = Dns.GetHostEntry("localhost").AddressList[0];
            IPAddress remotePeerIP = System.Net.IPAddress.Parse(ip);
            Socket socket = new Socket(localPeerIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(new IPEndPoint(remotePeerIP, port), new TimeSpan(0, 0, Constants.ReadSocketTimeout));
            return socket;
        }
        /// <summary>
        /// Send packet to remote peer. Again do not check for connected as used in intial handshake with peer and
        /// when not checks happen at a higher level.
        /// </summary>
        /// <param name="buffer">Buffer.</param>
        public void Write(byte[] buffer)
        {
            int bytesWritten = PeerSocket.Send(buffer, 0, buffer.Length, SocketFlags.None, out SocketError socketError);
            ThrowOnError("Socket send error.", (bytesWritten <= 0) || !PeerSocket.Connected, socketError);
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
            int bytesRead = PeerSocket.Receive(buffer, 0, length, SocketFlags.None, out SocketError socketError);
            ThrowOnError("Socket recieve error.", (bytesRead <= 0) || !PeerSocket.Connected, socketError);
            return bytesRead;
        }
        /// <summary>
        /// Start Async reading of network socket.
        /// </summary>
        /// <param name="remotePeer"></param>
        public void StartReads(Peer remotePeer)
        {
            Log.Logger.Info($"(PeerNetwork) Read packet handler {Encoding.ASCII.GetString(remotePeer.RemotePeerID)} started...");
            PeerSocket.BeginReceive(ReadBuffer, 0, Constants.SizeOfUInt32, SocketFlags.None, new AsyncCallback(ReadPacketAsyncHandler), remotePeer);
        }
        /// <summary>
        /// Close socket connection
        /// </summary>
        public void Close()
        {
            if (PeerSocket.Connected)
            {
                PeerSocket.Close();
                PeerSocket = null;
            }
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
        public static void StartListening(Agent agent)
        {
            try
            {
                _listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _listenerSocket.Bind(new IPEndPoint(IPAddress.Any, listenPort));
                _listenerSocket.Listen(100);
                _listenerSocket.BeginAccept(new AsyncCallback(AcceptConnectionAsyncHandler), agent);
                Log.Logger.Info("(PeerNetwork) Port connection listener started...");
            }
            catch (Exception ex)
            {
                throw new Exception("(PeerNetwork) Error:" + ex);
            }
        }
        /// <summary>
        /// Connect to listen port to shutdown listener task.
        /// </summary>
        public static void ShutdownListener(int port)
        {
            try
            {
                IPAddress localPeerIP = Dns.GetHostEntry(Host.GetIP()).AddressList[0];
                Socket socket = new Socket(localPeerIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(new IPEndPoint(localPeerIP, port));
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex.Message);
            }
        }
    }
}
