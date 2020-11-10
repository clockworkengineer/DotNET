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
            }
        }
    }

    /// <summary>
    /// Peer to peer network IO code.
    /// </summary>
    internal class PeerNetwork
    {
        private UInt32 _bytesRead;              // Bytes read in read request
        private bool _lengthRead;               // == true packet length has been read
        public byte[] ReadBuffer { get; set; }  // Read buffer
        public uint PacketLength { get; set; }  // Current packet length
        public Socket PeerSocket { get; set; }  // Socket for I/O

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

                if ((bytesRead <=0) || (socketError != SocketError.Success) || !PeerSocket.Connected)
                {

                    remotePeer.QueueForClosure();
                    return;
                }

                _bytesRead += (UInt32)bytesRead;
                if (!_lengthRead)
                {
                    if (_bytesRead == Constants.SizeOfUInt32)
                    {
                        PacketLength = Util.UnPackUInt32(ReadBuffer, 0);
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
                Log.Logger.Info($"ReadPacketCallBack()  {Encoding.ASCII.GetString(remotePeer.RemotePeerID)} terminated.");
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
        /// Send packet to remote peer.
        /// </summary>
        /// <param name="buffer">Buffer.</param>
        public void Write(byte[] buffer)
        {
            if (PeerSocket.Connected)
            {
                PeerSocket.Send(buffer);
            }
        }
        /// <summary>
        /// Read packet from remote peer.
        /// </summary>
        /// <returns>The read.</returns>
        /// <param name="buffer">Buffer.</param>
        /// <param name="length">Length.</param>
        public int Read(byte[] buffer, int length)
        {
            if (PeerSocket.Connected)
            {
                return PeerSocket.Receive(buffer, length, SocketFlags.None);
            }
            return 0;
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
        public void Connect(string ip, uint port)
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
        /// Get an local endpoint socket to listen for connections on 
        /// </summary>
        /// <returns></returns>
        public static Socket GetListeningConnection()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Host.GetIP());
            IPEndPoint localEndPoint = new IPEndPoint(ipHostInfo.AddressList[0], (int)Host.DefaultPort);
            Socket listener = new Socket(ipHostInfo.AddressList[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            listener.Bind(localEndPoint);
            listener.Listen(100);

            return listener;

        }
        /// <summary>
        /// Connect to listen port to shutdown listener task.
        /// </summary>
        public static void ShutdownListener()
        {

            try
            {
                IPAddress localPeerIP = Dns.GetHostEntry(Host.GetIP()).AddressList[0];
                Socket socket = new Socket(localPeerIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(new IPEndPoint(localPeerIP, (Int32)Host.DefaultPort));
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex.Message);
            }

        }
        /// <summary>
        /// Wait for remote connection on socket.
        /// </summary>
        /// <param name="listener"></param>
        /// <returns></returns>
        public static async Task<Socket> WaitForConnectionAsync(Socket listener)
        {
            return await listener.AcceptAsync();
        }
        /// <summary>
        /// Return IP and Port of remote connection.
        /// </summary>
        /// <param name="socket"></param>
        /// <returns></returns>
        public static ValueTuple<string, UInt32> GetConnectionEndPoint(Socket socket)
        {
            string remotePeerIP = ((IPEndPoint)(socket.RemoteEndPoint)).Address.ToString();
            UInt32 remotePeerPort = (UInt32)((IPEndPoint)(socket.RemoteEndPoint)).Port;

            return (remotePeerIP, remotePeerPort);
        }


    }
}
