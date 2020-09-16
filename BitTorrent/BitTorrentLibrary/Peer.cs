//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Peer to peer network I/O functionality.
//
// Copyright 2019.
//
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace BitTorrent
{
    public static class SocketExtensions
    {
        /// <summary>
        /// Connects the specified socket.
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
                throw new Error("Error: Peer connect timed out.");
            }
        }
    }
    /// <summary>
    /// Peer.
    /// </summary>
    public class Peer
    {
        private string _ip;
        private UInt32 _port;
        private ManualResetEvent _peerChoking;
        private bool _interested = true;
        private Socket _peerSocket;
        private byte[] _infoHash;
        private bool _connected;
        private byte[] _remotePeerID;
        private FileDownloader _torrentDownloader;
        private PieceBuffer _assembledPiece;
        private byte[] _readBuffer;
        private UInt32 _bytesRead;
        private UInt32 _packetLength;
        private bool _lengthRead;
        private ManualResetEvent _bitfieldReceived;
        private byte[] _remotePieceBitfield;
        private ManualResetEvent _waitForPieceAssembly;
        private bool _active;
        private Int64 _transferingPiece = -1;

        public byte[] ReadBuffer { get => _readBuffer; set => _readBuffer = value; }
        public bool Connected { get => _connected; set => _connected = value; }
        public byte[] RemotePeerID { get => _remotePeerID; set => _remotePeerID = value; }
        public FileDownloader TorrentDownloader { get => _torrentDownloader; set => _torrentDownloader = value; }
        public bool Interested { get => _interested; set => _interested = value; }
        public byte[] RemotePieceBitfield { get => _remotePieceBitfield; set => _remotePieceBitfield = value; }
        public uint PacketLength { get => _packetLength; set => _packetLength = value; }
        public PieceBuffer AssembledPiece { get => _assembledPiece; set => _assembledPiece = value; }
        public ManualResetEvent WaitForPieceAssembly { get => _waitForPieceAssembly; set => _waitForPieceAssembly = value; }
        public ManualResetEvent PeerChoking { get => _peerChoking; set => _peerChoking = value; }
        public ManualResetEvent BitfieldReceived { get => _bitfieldReceived; set => _bitfieldReceived = value; }
        public bool Active { get => _active; set => _active = value; }
        public Int64 TransferingPiece { get => _transferingPiece; set => _transferingPiece = value; }

        public string Ip { get => _ip; set => _ip = value; }

        /// <summary>
        /// Send packet to remote peer.
        /// </summary>
        /// <param name="buffer">Buffer.</param>
        public void PeerWrite(byte[] buffer)
        {
            _peerSocket.Send(buffer);
        }

        /// <summary>
        /// Read packet from remote peer.
        /// </summary>
        /// <returns>The read.</returns>
        /// <param name="buffer">Buffer.</param>
        /// <param name="length">Length.</param>
        public int PeerRead(byte[] buffer, int length)
        {
            return (_peerSocket.Receive(buffer, length, SocketFlags.None));
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

                UInt32 bytesRead = (UInt32)remotePeer._peerSocket.EndReceive(readAsyncState);

                remotePeer._bytesRead += bytesRead;

                if (!remotePeer._lengthRead)
                {
                    if (remotePeer._bytesRead == Constants.SizeOfUInt32)
                    {
                        remotePeer.PacketLength = ((UInt32)remotePeer._readBuffer[0]) << 24;
                        remotePeer.PacketLength |= ((UInt32)remotePeer._readBuffer[1]) << 16;
                        remotePeer.PacketLength |= ((UInt32)remotePeer._readBuffer[2]) << 8;
                        remotePeer.PacketLength |= remotePeer._readBuffer[3];
                        remotePeer._lengthRead = true;
                        remotePeer._bytesRead = 0;
                    }
                }
                else if (remotePeer._bytesRead == remotePeer.PacketLength)
                {
                    PWP.RemotePeerMessageProcess(remotePeer);
                    remotePeer._lengthRead = false;
                    remotePeer._bytesRead = 0;
                    remotePeer.PacketLength = Constants.SizeOfUInt32;
                }

                remotePeer._peerSocket.BeginReceive(remotePeer._readBuffer, (Int32)remotePeer._bytesRead,
                           (Int32)(remotePeer.PacketLength - remotePeer._bytesRead), 0, ReadPacketAsyncHandler, remotePeer);

            }
            catch (Error)
            {
                throw;
            }
            catch (System.ObjectDisposedException)
            {
                Log.Logger.Info($"ReadPacketCallBack()  {Encoding.ASCII.GetString(remotePeer.RemotePeerID)} terminated.");
            }
            catch (Exception ex)
            {
                Log.Logger.Debug("Internal ReadPacketCallBack() error : " + ex.Message);
                Log.Logger.Debug(ex);
            }
        }

        /// <summary>
        /// Gets the local host ip.
        /// </summary>
        /// <returns>The local host ip.</returns>
        static public string GetLocalHostIP()
        {
            string localHostIP="127.0.0.1";
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                localHostIP = endPoint.Address.ToString();
            }
            return(localHostIP);

        }

        /// <summary>
        /// Initializes a new instance of Peer class.
        /// </summary>
        /// <param name="fileDownloader">File downloader.</param>
        /// <param name="ip">Ip.</param>
        /// <param name="port">Port.</param>
        /// <param name="infoHash">Info hash.</param>
        public Peer(FileDownloader fileDownloader, string ip, UInt32 port, byte[] infoHash)
        {
            _ip = ip;
            _port = port;
            _infoHash = infoHash;
            _torrentDownloader = fileDownloader;
            _readBuffer = new byte[Constants.BlockSize + (2 * Constants.SizeOfUInt32) + 1]; // Maximum possible packet size
            _assembledPiece = new PieceBuffer(fileDownloader.Dc.pieceLength);
            _waitForPieceAssembly = new ManualResetEvent(false);
            _peerChoking = new ManualResetEvent(false);
            _bitfieldReceived = new ManualResetEvent(false);
        }

        /// <summary>
        /// Connect to a remote peer and perform initial Peer to Peer handshake.
        /// </summary>
        public void Connect()
        {

            try
            {
                IPAddress localPeerIP = Dns.GetHostEntry("localhost").AddressList[0];
                IPAddress remotePeerIP = System.Net.IPAddress.Parse(_ip);

                _peerSocket = new Socket(localPeerIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                _peerSocket.Connect(new IPEndPoint(remotePeerIP, (Int32)_port), new TimeSpan(0, 0, Constants.ReadSocketTimeout));

                ValueTuple<bool, byte[]> peerResponse = PWP.intialHandshake(this, _infoHash);

                if (peerResponse.Item1)
                {
                    RemotePeerID = peerResponse.Item2;
                    _connected = true;
                    _peerSocket.BeginReceive(_readBuffer, 0, Constants.SizeOfUInt32, 0, ReadPacketAsyncHandler, this);
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
            }

        }

        /// <summary>
        /// Release  any peer class resources.
        /// </summary>
        public void Close()
        {
            _peerSocket.Close();

        }

        /// <summary>
        /// Check downloaded bitfield to see if a piece is present on a remote peer.
        /// </summary>
        /// <returns><c>true</c>, if piece on remote peer was ised, <c>false</c> otherwise.</returns>
        /// <param name="pieceNumber">Piece number.</param>
        public bool IsPieceOnRemotePeer(UInt32 pieceNumber)
        {
            return ((_remotePieceBitfield[pieceNumber >> 3] & 0x80 >> (Int32)(pieceNumber & 0x7)) != 0);
        }

        /// <summary>
        /// Sets piece bit in local bitfield to signify its presence.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        public void SetPieceOnRemotePeer(UInt32 pieceNumber)
        {
            _remotePieceBitfield[pieceNumber >> 3] |= (byte)(0x80 >> (Int32)(pieceNumber & 0x7));
        }

        /// <summary>
        /// Place a block into the current piece being assembled.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="blockOffset">Block offset.</param>
        public void PlaceBlockIntoPiece(UInt32 pieceNumber, UInt32 blockOffset)
        {
            try
            {
                UInt32 blockNumber = blockOffset / Constants.BlockSize;

                Log.Logger.Trace($"placeBlockIntoPiece({pieceNumber},{blockOffset},{_packetLength - 9})");

                Buffer.BlockCopy(_readBuffer, 9, _assembledPiece.Buffer, (Int32)blockOffset, (Int32)_packetLength - 9);

                _torrentDownloader.Dc.BlockPieceDownloaded(pieceNumber, blockNumber, true);
                _torrentDownloader.Dc.BlockPieceRequested(pieceNumber, blockNumber, false);

                if (TorrentDownloader.Dc.HasPieceBeenAssembled(pieceNumber))
                {
                    _assembledPiece.Number = pieceNumber;
                    _torrentDownloader.Dc.totalBytesDownloaded += _torrentDownloader.Dc.GetPieceLength(pieceNumber);
                    _waitForPieceAssembly.Set();
                }

            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
            }
        }

    }
}
