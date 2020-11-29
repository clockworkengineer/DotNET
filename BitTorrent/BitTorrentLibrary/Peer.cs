//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Class encaspulating peer data resources and functionlaity.
//
// Copyright 2020.
//
using System;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
namespace BitTorrentLibrary
{
    internal delegate void ProtocolHandler(Peer remotePeer);
    internal class Peer
    {
        private IPeerNetwork _network;                                   // Network layer
        private readonly Mutex _closeGuardMutex;                         // Peer close guard
        public Stopwatch PacketResponseTimer { get; }                    // Packet reponse timer
        public Average AveragePacketResponse { get; }                    // Average packet reponse time
        public ProtocolHandler ProtocolHandler { get; }                  // Wire Procol handler for peer
        public bool Connected { get; set; }                              // == true connected to remote peer
        public byte[] RemotePeerID { get; set; }                         // Id of remote peer
        public TorrentContext Tc { get; set; }                           // Torrent torrent context
        public byte[] RemotePieceBitfield { get; set; }                  // Remote peer piece map
        public string Ip { get; }                                        // Remote peer ip
        public int Port { get; }                                         // peer Port
        public bool AmInterested { get; set; } = false;                  // == true then client interested in remote peer
        public bool AmChoking { get; set; } = true;                      // == true then client is choking remote peer.
        public ManualResetEvent PeerChoking { get; }                     // == true (set) then remote peer is choking client (local host)
        public bool PeerInterested { get; set; } = false;                // == true then remote peer interested in client (local host)
        public CancellationTokenSource CancelTaskSource { get; set; }    // Cancelation token source for cancel task request token
        public int NumberOfMissingPieces { get; set; }                   // Number of missing pieces from a remote peers torrent
        public int OutstandingRequestsCount { get; set; }                // Current number of outstanding reqests
        public byte[] ReadBuffer => _network?.ReadBuffer;                // Network read buffer
        /// <summary>
        /// Internal constructor Setup data and resources needed by peer for unit tests.
        /// </summary>
        /// <param name="ip">Ip.</param>
        /// <param name="port">Port.</param>
        /// <param name="infoHash">Info hash.</param>
        /// <param name="tc">torrent context.</param>
        /// <param name="network">peer network layer.</param>
        /// 
        internal Peer(string ip, int port, TorrentContext tc, Socket socket, IPeerNetwork network) : this(ip, port, tc, socket)
        {
            _network = network;
        }
        /// <summary>
        /// Setup data and resources needed by peer. No torrent context
        /// passed as it is hooked up when the remote connecting peer has
        /// done a successful intial handshake.
        /// </summary>
        /// <param name="ip">Ip.</param>
        /// <param name="port">Port.</param>
        /// <param name="infoHash">Info hash.</param>
        /// 
        public Peer(string ip, int port, Socket socket)
        {
            if (string.IsNullOrEmpty(ip))
            {
                throw new ArgumentException($"'{nameof(ip)}' cannot be null or empty", nameof(ip));
            }
            if (socket is null)
            {
                throw new ArgumentNullException(nameof(socket));
            }
            Ip = ip;
            Port = port;
            _network = new PeerNetwork(socket);
            _closeGuardMutex = new Mutex();
            PacketResponseTimer = new Stopwatch();
            PeerChoking = new ManualResetEvent(false);
            CancelTaskSource = new CancellationTokenSource();
            ProtocolHandler = PWP.MessageProcess;
        }
        /// <summary>
        /// Setup data and resources needed by peer.
        /// </summary>
        /// <param name="ip">Ip.</param>
        /// <param name="port">Port.</param>
        /// <param name="infoHash">Info hash.</param>
        /// <param name="tc">torrent context.</param>
        /// 
        public Peer(string ip, int port, TorrentContext tc, Socket socket) : this(ip, port, socket)
        {
            if (tc is null)
            {
                throw new ArgumentNullException(nameof(tc));
            }
            SetTorrentContext(tc);
        }
        /// <summary>
        /// Set torrent context and dependant fields.
        /// </summary>
        /// <param name="tc"></param>
        public void SetTorrentContext(TorrentContext tc)
        {
            Tc = tc ?? throw new ArgumentNullException(nameof(tc));
            NumberOfMissingPieces = (int)Tc.numberOfPieces;
            RemotePieceBitfield = new byte[tc.Bitfield.Length];
        }
        /// <summary>
        /// Send packet to remote peer.
        /// </summary>
        /// <param name="buffer">Buffer.</param>
        public void Write(byte[] buffer)
        {
            if (buffer is null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            _network.Write(buffer);
        }
        /// <summary>
        /// Read packet from remote peer.
        /// </summary>
        /// <returns>The read.</returns>
        /// <param name="buffer">Buffer.</param>
        public int Read(byte[] buffer)
        {
            if (buffer is null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            return _network.Read(buffer, buffer.Length);
        }
        /// <summary>
        ///  Perform intial handsake with remote peer.
        /// </summary>
        /// <param name="manager"></param>
        public void Handshake(Manager manager)
        {
            if (manager is null)
            {
                throw new ArgumentNullException(nameof(manager));
            }
            RemotePeerID = PWP.Handshake(this, manager);
            Connected = true;
            _network.StartReads(this);
            PWP.Bitfield(this, Tc.Bitfield);
        }
        /// <summary>
        /// Release  any peer class resources.
        /// </summary>
        public void Close()
        {
            try
            {
                _closeGuardMutex.WaitOne();
                if (Connected)
                {
                    Log.Logger.Info($"Closing down Peer {Encoding.ASCII.GetString(RemotePeerID)}...");
                    CancelTaskSource.Cancel();
                    Tc.UnMergePieceBitfield(this);
                    if (Tc.peerSwarm.ContainsKey(Ip))
                    {
                        if (Tc.peerSwarm.TryRemove(Ip, out Peer _))
                        {
                            Log.Logger.Info($"Dead Peer {Ip} removed from swarm.");
                        }
                    }
                    Connected = false;
                    Log.Logger.Info($"Closed down {Encoding.ASCII.GetString(RemotePeerID)}.");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex);
            }
            _network?.Close();
            _network = null;
            _closeGuardMutex.ReleaseMutex();
        }
        /// <summary>
        /// Check downloaded bitfield to see if a piece is present on a remote peer.
        /// </summary>
        /// <returns><c>true</c>, if piece on remote peer was ised, <c>false</c> otherwise.</returns>
        /// <param name="pieceNumber">Piece number.</param>
        public bool IsPieceOnRemotePeer(UInt32 pieceNumber)
        {
            if (RemotePieceBitfield != null)
            {
                return (RemotePieceBitfield[pieceNumber >> 3] & 0x80 >> (Int32)(pieceNumber & 0x7)) != 0;
            }
            else
            {
                throw new Exception("Torrent context needs to be set for peer.");
            }
        }
        /// <summary>
        /// Sets piece bit in local bitfield to signify its presence.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        public void SetPieceOnRemotePeer(UInt32 pieceNumber)
        {
            if (!IsPieceOnRemotePeer(pieceNumber))
            {
                RemotePieceBitfield[pieceNumber >> 3] |= (byte)(0x80 >> (Int32)(pieceNumber & 0x7));
                Tc.IncrementPeerCount(pieceNumber);
                NumberOfMissingPieces--;
            }
        }
        /// <summary>
        /// Place a block into the current piece being assembled. Any request
        /// that comes down the wire not for the current piece being assembled
        /// is discarded.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="blockOffset">Block offset.</param>
        public void PlaceBlockIntoPiece(UInt32 pieceNumber, UInt32 blockOffset)
        {
            if (pieceNumber == Tc.assemblyData.pieceBuffer.Number)
            {
                Tc.assemblyData.guardMutex.WaitOne();
                try
                {
                    Log.Logger.Trace($"PlaceBlockIntoPiece({pieceNumber},{blockOffset},{_network.PacketLength - 9})");
                    UInt32 blockNumber = blockOffset / Constants.BlockSize;
                    if (!Tc.assemblyData.pieceBuffer.IsBlockPresent(blockNumber))
                    {
                        Tc.assemblyData.currentBlockRequests--;
                    }
                    Tc.assemblyData.pieceBuffer.AddBlockFromPacket(_network.ReadBuffer, blockNumber);
                    if (Tc.assemblyData.currentBlockRequests == 0)
                    {
                        Tc.assemblyData.blockRequestsDone.Set();
                    }
                }
                catch (Exception ex)
                {
                    Log.Logger.Error(ex);
                }
                OutstandingRequestsCount--;
                Tc.assemblyData.guardMutex.ReleaseMutex();
            }
        }
        /// <summary>
        /// Get length of command packet read.
        /// </summary>
        /// <returns></returns>
        public int GetPacketLength()
        {
            return _network != null ? (int)_network.PacketLength : 0; // Packet Length
        }
    }
}
