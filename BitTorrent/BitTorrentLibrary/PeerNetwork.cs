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
        private static Socket _listenerSocket;
        private int _bytesRead;                 // Bytes read in read request
        private bool _lengthRead;               // == true packet length has been read
        public byte[] ReadBuffer { get; set; }  // Read buffer
        public int PacketLength { get; set; }   // Current packet length
        public Socket PeerSocket { get; set; }  // Socket for I/O
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
                _listenerSocket.BeginAccept(AcceptConnectionAsyncHandler, agent);
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex.Message);
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
                Int32 bytesRead = (Int32)PeerSocket.EndReceive(readAsyncState, out SocketError socketError);
                if ((bytesRead <= 0) || (socketError != SocketError.Success) || !PeerSocket.Connected)
                {
                    remotePeer.QueueForClosure();
                    return;
                }
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
                PeerSocket.BeginReceive(ReadBuffer, (Int32)_bytesRead,
                               (Int32)(PacketLength - _bytesRead), 0, new AsyncCallback(ReadPacketAsyncHandler), remotePeer);
            }
            catch (System.ObjectDisposedException)
            {
                Log.Logger.Info($"(PeerNetwork) ReadPacketCallBack()  {Encoding.ASCII.GetString(remotePeer.RemotePeerID)} terminated.");
                remotePeer.QueueForClosure();
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex.Message);
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
        /// Send packet to remote peer. Again do not check for connected as used in intial handshake with peer and
        /// when not checks happen at a higher level.
        /// </summary>
        /// <param name="buffer">Buffer.</param>
        public void Write(byte[] buffer)
        {
            PeerSocket.Send(buffer);
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
            return PeerSocket.Receive(buffer, 0, length, SocketFlags.None);
        }
        /// <summary>
        /// Start Async reading of network socket.
        /// </summary>
        /// <param name="remotePeer"></param>
        public void StartReads(Peer remotePeer)
        {
            if (PeerSocket.Connected)
            {
                PeerSocket.BeginReceive(ReadBuffer, 0, Constants.SizeOfUInt32, 0, ReadPacketAsyncHandler, remotePeer);
            }
        }
        /// <summary>
        /// Connect to remote peer and return connection socket.
        /// </summary>
        /// <param name="remotePeer"></param>
        public void Connect(string ip, int port)
        {
            IPAddress localPeerIP = Dns.GetHostEntry("localhost").AddressList[0];
            IPAddress remotePeerIP = System.Net.IPAddress.Parse(ip);
            PeerSocket = new Socket(localPeerIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            PeerSocket.Connect(new IPEndPoint(remotePeerIP, (Int32)port), new TimeSpan(0, 0, Constants.ReadSocketTimeout));
        }
        /// <summary>
        /// Close socket connection
        /// </summary>
        public void Close()
        {
            if (PeerSocket.Connected)
            {
                PeerSocket.Close();
            }
        }
        /// <summary>
        /// Return IP and Port of remote connection.
        /// </summary>
        /// <param name="socket"></param>
        /// <returns></returns>
        public static PeerDetails GetConnectinPeerDetails(Socket socket)
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
        public static void StartListening(Agent agent, int port)
        {
            try
            {
                _listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _listenerSocket.Bind(new IPEndPoint(IPAddress.Any, port));
                _listenerSocket.Listen(100);
                _listenerSocket.BeginAccept(AcceptConnectionAsyncHandler, agent);
                Log.Logger.Info("(PeerNetwork) Port connection listener started.");
            }
            catch (Exception ex)
            {
                throw new Exception("BitTorrent (PeerNetwork) Error:" + ex);
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
