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
using System.Collections.Concurrent;
namespace BitTorrentLibrary
{
    internal delegate void ProtocolHandler(Peer remotePeer);
    internal class Peer
    {
        private PeerNetwork _network;                                    // Network layer
        private readonly Mutex _closeGuardMutex;                         // Peer close guard
        internal readonly Stopwatch packetResponseTimer;                 // Packet reponse timer
        internal Average averagePacketResponse;                          // Average packet reponse time
        internal BlockingCollection<Peer> peerCloseQueue;                // Peer close queue
        internal ProtocolHandler protocolHandler;                        // Wire Procol handler for peer
        public bool Connected { get; set; }                              // == true connected to remote peer
        public byte[] RemotePeerID { get; set; }                         // Id of remote peer
        public TorrentContext Tc { get; set; }                           // Torrent torrent context
        public byte[] RemotePieceBitfield { get; set; }                  // Remote peer piece map
        public string Ip { get; }                                        // Remote peer ip
        public int Port { get; }                                        // peer Port
        public bool AmInterested { get; set; } = false;                  // == true then client interested in remote peer
        public bool AmChoking { get; set; } = true;                      // == true then client is choking remote peer.
        public ManualResetEvent PeerChoking { get; }                     // == true (set) then remote peer is choking client (local host)
        public bool PeerInterested { get; set; } = false;                // == true then remote peer interested in client (local host)
        public CancellationTokenSource CancelTaskSource { get; set; }    // Cancelation token source for cancel task request token
        public ManualResetEvent BitfieldReceived { get; }                // When event set then peer has recieved bitfield from remote peer
        public int NumberOfMissingPieces { get; set; }                   // Number of missing pieces from a remote peers torrent
        public byte[] ReadBuffer => _network?.ReadBuffer;                 // Network read buffer
        public int PacketLength => (int)_network?.PacketLength;          // Current read packet length
        /// <summary>
        /// Setup data and resources needed by peer.
        /// </summary>
        /// <param name="ip">Ip.</param>
        /// <param name="port">Port.</param>
        /// <param name="infoHash">Info hash.</param>
        /// <param name="tc">torrent context.</param>
        /// 
        public Peer(string ip, int port, TorrentContext tc, Socket socket)
        {
            Ip = ip;
            Port = port;
            _network = new PeerNetwork(socket);
            _closeGuardMutex = new Mutex();
            packetResponseTimer = new Stopwatch();
            PeerChoking = new ManualResetEvent(false);
            CancelTaskSource = new CancellationTokenSource();
            protocolHandler = PWP.MessageProcess;
            if (tc != null)
            {
                SetTorrentContext(tc);
            }
        }
        /// <summary>
        /// Set torrent context and dependant fields.
        /// </summary>
        /// <param name="tc"></param>
        public void SetTorrentContext(TorrentContext tc)
        {
            Tc = tc;
            NumberOfMissingPieces = (int)Tc.numberOfPieces;
            RemotePieceBitfield = new byte[tc.Bitfield.Length];
        }
        /// <summary>
        /// Send packet to remote peer.
        /// </summary>
        /// <param name="buffer">Buffer.</param>
        public void Write(byte[] buffer)
        {
            _network.Write(buffer);
        }
        /// <summary>
        /// Read packet from remote peer.
        /// </summary>
        /// <returns>The read.</returns>
        /// <param name="buffer">Buffer.</param>
        /// <param name="length">Length.</param>
        public int Read(byte[] buffer, int length)
        {
            return _network.Read(buffer, length);
        }
        /// <summary>
        ///  Perform intial handsake with remote peer.
        /// </summary>
        /// <param name="manager"></param>
        public void Handshake(Manager manager)
        {
            ValueTuple<bool, byte[]> peerResponse = PWP.Handshake(this, manager);
            if (peerResponse.Item1)
            {
                Connected = true;
                RemotePeerID = peerResponse.Item2;
                _network.StartReads(this);
                PWP.Bitfield(this, Tc.Bitfield);
            }
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
                    Log.Logger.Info($"(Peer) Closing down Peer {Encoding.ASCII.GetString(RemotePeerID)}...");
                    CancelTaskSource.Cancel();
                    Tc.UnMergePieceBitfield(this);
                    if (Tc.peerSwarm.ContainsKey(Ip))
                    {
                        if (Tc.peerSwarm.TryRemove(Ip, out Peer _))
                        {
                            Log.Logger.Info($"(Peer) Dead Peer {Ip} removed from swarm.");
                        }
                    }
                    Connected = false;
                    Log.Logger.Info($"(Peer) Closed down {Encoding.ASCII.GetString(RemotePeerID)}.");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
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
            return (RemotePieceBitfield[pieceNumber >> 3] & 0x80 >> (Int32)(pieceNumber & 0x7)) != 0;
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
        /// Place a block into the current piece being assembled.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="blockOffset">Block offset.</param>
        public void PlaceBlockIntoPiece(UInt32 pieceNumber, UInt32 blockOffset)
        {

            if (pieceNumber == Tc.assemblyData.pieceBuffer.Number)
            {
                Tc.assemblyData.guardMutex.WaitOne();
                Log.Logger.Trace($"(Peer) PlaceBlockIntoPiece({pieceNumber},{blockOffset},{_network.PacketLength - 9})");
                UInt32 blockNumber = blockOffset / Constants.BlockSize;
                if (!Tc.assemblyData.pieceBuffer.IsBlockPresent(blockNumber))
                {
                    Tc.assemblyData.currentBlockRequests--;
                }
                Tc.assemblyData.pieceBuffer.AddBlockFromPacket(_network.ReadBuffer, blockNumber);
                if (Tc.assemblyData.pieceBuffer.AllBlocksThere)
                {
                    Tc.assemblyData.pieceBuffer.Number = pieceNumber;
                    Tc.assemblyData.pieceFinished.Set();
                }
                if (Tc.assemblyData.currentBlockRequests == 0)
                {
                    Tc.assemblyData.blockRequestsDone.Set();
                }
                Tc.assemblyData.guardMutex.ReleaseMutex();
            }
            else
            {
                Log.Logger.Debug($"(Peer) PIECE {pieceNumber} DISCARDED.");
            }

        }
    }
}
