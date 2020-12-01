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
using System.Net.Sockets;
using System.Text;
namespace BitTorrentLibrary
{
    internal interface IPeerNetwork
    {
        byte[] ReadBuffer { get; set; }
        int PacketLength { get; set; }
        void Close();
        int Read(byte[] buffer, int length);
        void StartReads(Peer remotePeer);
        void Write(byte[] buffer);
    }
    internal class PeerNetwork : IPeerNetwork
    {
        private int _bytesRead;                                 // Bytes read in read request
        private bool _lengthRead;                               // == true packet length has been read
        private Socket _socket;                                 // Socket for I/O
        public byte[] ReadBuffer { get; set; }                  // Read buffer
        public int PacketLength { get; set; }                   // Current packet length
        /// <summary>
        /// Throw an exception if an send error is reported.The error can either
        /// be because the errorReported boolean is true or an actual socket error
        /// reported has been reported by the system.
        /// </summary>
        /// <param name="errorReported"></param>
        /// <param name="socketError"></param>
        void ThrowOnError(bool errorReported, SocketError socketError)
        {
            if (errorReported || socketError != SocketError.Success)
            {
                string errorMessage = ("Connection to remote peer has been closed down.");
                if (socketError != SocketError.Success)
                {
                    errorMessage += "Socket Error: " + socketError.ToString();
                }
                throw new Exception(errorMessage);
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
                SocketError socketError = SocketError.Success;
                int? bytesRead = _socket?.EndReceive(readAsyncState, out socketError);
                ThrowOnError((_socket is null) || (bytesRead <= 0) || !_socket.Connected, socketError);
                _bytesRead += (int) bytesRead;
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
                    remotePeer.ProtocolHandler(remotePeer);
                    _lengthRead = false;
                    _bytesRead = 0;
                    PacketLength = Constants.SizeOfUInt32;
                }
                _socket?.BeginReceive(ReadBuffer, _bytesRead, (PacketLength - _bytesRead), SocketFlags.None, new AsyncCallback(ReadAsyncHandler), remotePeer);
                ThrowOnError((_socket is null) || (bytesRead <= 0) || !_socket.Connected, socketError);
            }
            catch (Exception ex)
            {
                Log.Logger.Debug("Error (Ignored): " + ex.Message);
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
        /// Send packet to remote peer. Again do not check for connected as used in intial handshake with peer and
        /// when not checks happen at a higher level.
        /// </summary>
        /// <param name="buffer">Buffer.</param>
        public void Write(byte[] buffer)
        {
            SocketError socketError = SocketError.Success;
            int?  bytesWritten = _socket?.Send(buffer, 0, buffer.Length, SocketFlags.None, out socketError);
            ThrowOnError((_socket is null) || (bytesWritten <= 0) || !_socket.Connected, socketError);
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
            SocketError socketError = SocketError.Success;
            int? bytesRead = _socket?.Receive(buffer, 0, length, SocketFlags.None, out socketError);
            ThrowOnError((_socket is null) || (bytesRead <= 0) || !_socket.Connected, socketError);
            return (int)bytesRead;
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
    }
}
