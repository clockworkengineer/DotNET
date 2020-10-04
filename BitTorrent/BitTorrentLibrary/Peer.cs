
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
using System.Threading.Tasks;

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
    /// Peer.
    /// </summary>
    public class Peer
    {
        private readonly UInt32 _port;                      // Port
        private Socket _peerSocket;                         // Socket for I/O
        private readonly byte[] _infoHash;                  // Info Hash of torrent
        private UInt32 _bytesRead;                          // Bytes read in read request
        private bool _lengthRead;                           // == true packet length has been read
        public byte[] ReadBuffer { get; set; }              // Read buffer
        public bool Connected { get; set; }                 // == true connected to remote peer
        public byte[] RemotePeerID { get; set; }            // Id of remote peer
        public DownloadContext Dc { get; set; }              // Torrent download context
        public byte[] RemotePieceBitfield { get; set; }     // Remote peer piece map
        public uint PacketLength { get; set; }              // Current packet length
        public PieceBuffer AssembledPiece { get; set; }     // Assembled pieces buffer
        public string Ip { get; set; }                      // Remote peer ip
        public Task AssemblerTask { get; set; }             // Peer piece assembly task
        public bool AmInterested { get; set; } = false;                  // == true then client interested in remote peer
        public bool AmChoked { get; set; } = true;                       // == true then client is choing remote peer.
        public ManualResetEvent PeerChoking { get; set; }                // == true (set) then remote peer is choking client (local host)
        public bool PeerInterested { get; set; } = false;                // == true then remote peer interested in client (local host)
        public CancellationTokenSource CancelTaskSource { get; set; }
        public ManualResetEvent WaitForPieceAssembly { get; set; }
        public ManualResetEvent BitfieldReceived { get; set; }

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
                        remotePeer.PacketLength = ((UInt32)remotePeer.ReadBuffer[0]) << 24;
                        remotePeer.PacketLength |= ((UInt32)remotePeer.ReadBuffer[1]) << 16;
                        remotePeer.PacketLength |= ((UInt32)remotePeer.ReadBuffer[2]) << 8;
                        remotePeer.PacketLength |= remotePeer.ReadBuffer[3];
                        remotePeer._lengthRead = true;
                        remotePeer._bytesRead = 0;
                        if (remotePeer.PacketLength > remotePeer.ReadBuffer.Length)
                        {
                            Log.Logger.Debug("Resizing readBuffer ...");
                            remotePeer.ReadBuffer = new byte[remotePeer.PacketLength];
                        }
                    }
                }
                else if (remotePeer._bytesRead == remotePeer.PacketLength)
                {
                    PWP.RemotePeerMessageProcess(remotePeer);
                    remotePeer._lengthRead = false;
                    remotePeer._bytesRead = 0;
                    remotePeer.PacketLength = Constants.SizeOfUInt32;
                }

                remotePeer._peerSocket.BeginReceive(remotePeer.ReadBuffer, (Int32)remotePeer._bytesRead,
                           (Int32)(remotePeer.PacketLength - remotePeer._bytesRead), 0, ReadPacketAsyncHandler, remotePeer);
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
        /// Initializes a new instance of Peer class.
        /// </summary>
        /// <param name="ip">Ip.</param>
        /// <param name="port">Port.</param>
        /// <param name="infoHash">Info hash.</param>
        /// <param name="dc">Download context.</param>
        public Peer(string ip, UInt32 port, byte[] infoHash, DownloadContext dc)
        {
            Ip = ip;
            _port = port;
            _infoHash = infoHash;
            Dc = dc;
            ReadBuffer = new byte[Constants.BlockSize + (2 * Constants.SizeOfUInt32) + 1]; // Maximum possible packet size
            AssembledPiece = new PieceBuffer(Dc.PieceLength);
            WaitForPieceAssembly = new ManualResetEvent(false);
            PeerChoking = new ManualResetEvent(false);
            BitfieldReceived = new ManualResetEvent(false);
            CancelTaskSource = new CancellationTokenSource();
        }


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
            return _peerSocket.Receive(buffer, length, SocketFlags.None);
        }

        /// <summary>
        /// Connect to a remote peer and perform initial Peer to Peer handshake.
        /// </summary>
        public void Connect()
        {
            try
            {
                IPAddress localPeerIP = Dns.GetHostEntry("localhost").AddressList[0];
                IPAddress remotePeerIP = System.Net.IPAddress.Parse(Ip);

                _peerSocket = new Socket(localPeerIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                _peerSocket.Connect(new IPEndPoint(remotePeerIP, (Int32)_port), new TimeSpan(0, 0, Constants.ReadSocketTimeout));

                ValueTuple<bool, byte[]> peerResponse = PWP.IntialHandshake(this, _infoHash);

                if (peerResponse.Item1)
                {
                    RemotePeerID = peerResponse.Item2;
                    Connected = true;
                    _peerSocket.BeginReceive(ReadBuffer, 0, Constants.SizeOfUInt32, 0, ReadPacketAsyncHandler, this);
                }
            }
            catch (Exception ex)
            {
                throw new Error("BitTorrent (Peer) Error: " + ex.Message);
            }
        }

        /// <summary>
        /// Release  any peer class resources.
        /// </summary>
        public void Close()
        {
            if (Connected)
            {
                Connected = false;
                CancelTaskSource.Cancel();
                _peerSocket.Close();
                Log.Logger.Info($"Closing down Peer {Encoding.ASCII.GetString(RemotePeerID)}.");
            }
        }

        /// <summary>
        /// Check downloaded bitfield to see if a piece is present on a remote peer.
        /// </summary>
        /// <returns><c>true</c>, if piece on remote peer was ised, <c>false</c> otherwise.</returns>
        /// <param name="pieceNumber">Piece number.</param>
        public bool IsPieceOnRemotePeer(UInt32 pieceNumber)
        {
            return (RemotePieceBitfield[pieceNumber >> 3] & 0x80 >> (Int32)(pieceNumber & 0x7)) != 0;
        }

        /// <summary>
        /// Sets piece bit in local bitfield to signify its presence.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        public void SetPieceOnRemotePeer(UInt32 pieceNumber)
        {
            RemotePieceBitfield[pieceNumber >> 3] |= (byte)(0x80 >> (Int32)(pieceNumber & 0x7));
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

                Log.Logger.Trace($"PlaceBlockIntoPiece({pieceNumber},{blockOffset},{PacketLength - 9})");

                AssembledPiece.AddBlockFromPacket(ReadBuffer, blockNumber);

                if (AssembledPiece.AllBlocksThere)
                {
                    Dc.MarkPieceLocal(pieceNumber, true);
                    Dc.MarkPieceRequested(pieceNumber, false);
                    AssembledPiece.Number = pieceNumber;
                    Dc.TotalBytesDownloaded += Dc.PieceMap[pieceNumber].pieceLength;
                    WaitForPieceAssembly.Set();
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (Peer) Error: " + ex.Message);
            }
        }
    }
}
