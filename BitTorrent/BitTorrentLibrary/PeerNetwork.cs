//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Peer to peer network I/O layer.
//
// Copyright 2020.
//
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace BitTorrentLibrary
{
    public static class SocketExtensions
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
                throw new Error("BitTorrent (Connect) Error: Peer connect timed out.");
            }
        }
    }
    /// <summary>
    /// 
    /// </summary>
    public class PeerNetwork
    {
        private Socket _peerSocket;                                      // Socket for I/O
        private UInt32 _bytesRead;                                       // Bytes read in read request
        private bool _lengthRead;                                        // == true packet length has been read
        public byte[] ReadBuffer { get; set; }                           // Read buffer
        public uint PacketLength { get; set; }                           // Current packet length

        /// <summary>
        /// Peer read packet asynchronous callback.
        /// </summary>
        /// <param name="readAsyncState">Read async state.</param>
        private void ReadPacketAsyncHandler(IAsyncResult readAsyncState)
        {
            Peer remotePeer = (Peer)readAsyncState.AsyncState;
            try
            {
                Int32 bytesRead = (Int32)_peerSocket.EndReceive(readAsyncState, out SocketError socketError);

                if ((bytesRead <= 0) || (socketError != SocketError.Success))
                {
                    remotePeer.Close();
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

                _peerSocket.BeginReceive(ReadBuffer, (Int32)_bytesRead,
                               (Int32)(PacketLength - _bytesRead), 0, new AsyncCallback(ReadPacketAsyncHandler), remotePeer);
            }
            catch (System.ObjectDisposedException)
            {
                Log.Logger.Info($"ReadPacketCallBack()  {Encoding.ASCII.GetString(remotePeer.RemotePeerID)} terminated.");
                remotePeer.Close();
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex.Message);
                remotePeer.Close();
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public PeerNetwork(Socket remotePeerSocket)
        {
            ReadBuffer = new byte[Constants.BlockSize + (2 * Constants.SizeOfUInt32) + 1]; // Maximum possible packet size
            _peerSocket = remotePeerSocket;
        }

        /// <summary>
        /// Send packet to remote peer.
        /// </summary>
        /// <param name="buffer">Buffer.</param>
        public void Write(byte[] buffer)
        {
            _peerSocket.Send(buffer);
        }

        /// <summary>
        /// Read packet from remote peer.
        /// </summary>
        /// <returns>The read.</returns>
        /// <param name="buffer">Buffer.</param>
        /// <param name="length">Length.</param>
        public int Read(byte[] buffer, int length)
        {
            return _peerSocket.Receive(buffer, length, SocketFlags.None);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="remotePeer"></param>
        public void StartReads(Peer remotePeer)
        {
            _peerSocket.BeginReceive(ReadBuffer, 0, Constants.SizeOfUInt32, 0, ReadPacketAsyncHandler, remotePeer);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="remotePeer"></param>
        public void Connect(string ip, uint port)
        {
            IPAddress localPeerIP = Dns.GetHostEntry("localhost").AddressList[0];
            IPAddress remotePeerIP = System.Net.IPAddress.Parse(ip);

            _peerSocket = new Socket(localPeerIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            _peerSocket.Connect(new IPEndPoint(remotePeerIP, (Int32)port), new TimeSpan(0, 0, Constants.ReadSocketTimeout));

        }
        /// <summary>
        /// 
        /// </summary>
        public void Close()
        {
            _peerSocket.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="socket"></param>
        public void SetSocket(Socket socket)
        {
            _peerSocket = socket;
        }
        /// <summary>
        /// 
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
        /// 
        /// </summary>
        /// <param name="listener"></param>
        /// <returns></returns>
        public static Socket WaitForConnection(Socket listener)
        {
            return (listener.Accept());
        }
        /// <summary>
        /// 
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