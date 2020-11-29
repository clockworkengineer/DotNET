//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Torrent being downloaded/seeded context. This includes
// download progress, a pieces to download bitfield, functions for 
// accessing this map to change a pieces status and the buffer for the
// current piece being assembled.
//
// Copyright 2020.
//
using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading;
using System.Runtime.CompilerServices;
namespace BitTorrentLibrary
{
    public delegate void DownloadCompleteCallBack(Object callbackData);  // Download completed callback
    /// <summary>
    /// Piece being assembled related data
    /// </summary>
    internal struct AssemblerData
    {
        internal PieceBuffer pieceBuffer;                   // Assembled piece buffer
        internal Task task;                                 // Torrent piece assembly task
        internal ManualResetEvent pieceFinished;            // When event set then piece has been fully assembled
        internal ManualResetEvent blockRequestsDone;        // When event set then piece has been fully assembled
        internal CancellationTokenSource cancelTaskSource;  // Cancel assembler task source
        internal Average averageAssemblyTime;               // Average assembly time in milliseconds
        internal int totalTimeouts;                         // Timeouts while assembling pieces
        internal int currentBlockRequests;                  // Current outstanding block requests
        internal Mutex guardMutex;                          // Assembled piece guard mutex
    }
    /// <summary>
    /// Piece Information.
    /// </summary>
    internal struct PieceInfo
    {
        public int peerCount;        // Peers with the piece
        public UInt32 pieceLength;   // Piece length in bytes
    }
    /// <summary>
    /// Torrent context for a torrent file.
    /// </summary>
    public class TorrentContext
    {
        private readonly SHA1 _SHA1;                                 // Object to create SHA1 piece info hash
        private readonly byte[] _piecesMissing;                      // Missing piece bitfield
        private readonly PieceInfo[] _pieceData;                     // Piece information 
        internal Manager manager;                                    // Torrent context 
        internal ManualResetEvent paused;                            // == false (unset) pause downloading from peer
        internal BlockingCollection<PieceBuffer> pieceWriteQueue;    // Piece buffer disk write queue
        internal BlockingCollection<PieceRequest> pieceRequestQueue; // Piece request queue
        internal UInt32 pieceLength;                                 // Length of piece in bytese
        internal byte[] piecesInfoHash;                              // Pieces infohash from torrent file
        internal int numberOfPieces;                                // Number of pieces in torrent
        internal Selector selector;                                  // Piece selector
        internal ManualResetEvent downloadFinished;                  // == Set then download finished
        internal byte[] Bitfield;                                    // Bitfield for current torrent on disk
        internal List<FileDetails> filesToDownload;                  // Local files in torrent
        internal byte[] infoHash;                                    // Torrent info hash
        internal string trackerURL;                                  // Main Tracker URL
        internal int missingPiecesCount = 0;                         // Missing piece count
        internal int maximumSwarmSize = Constants.MaximumSwarmSize;  // Maximim swarm size
        internal ConcurrentDictionary<string, Peer> peerSwarm;       // Current peer swarm
        internal Tracker MainTracker;                                // Main tracker assigned to torrent
        internal AssemblerData assemblyData;                         // Torrent piece assemblage data
        public ProgessCallBack CallBack { get; set; }                // Download progress function
        public Object CallBackData { get; set; }                     // Download progress function data
        public TorrentStatus Status { get; set; }                    // Torrent status
        public string FileName { get; set; }                         // Torrent file name
        public UInt64 TotalBytesDownloaded { get; set; }             // Total bytes downloaded
        public UInt64 TotalBytesToDownload { get; set; }             // Total bytes in torrent
        public UInt64 TotalBytesUploaded { get; set; }               // Total bytes uploaded to all peers from torrent
        /// <summary>
        /// Setup data and resources needed by torrent context.
        /// </summary>
        /// <param name="torrentMetaInfo"></param>
        /// <param name="pieceSelector"></param>
        /// <param name="diskIO"></param>
        /// <param name="downloadPath"></param>
        /// <param name="seeding"></param>
        public TorrentContext(MetaInfoFile torrentMetaInfo, Selector pieceSelector, DiskIO diskIO, string downloadPath, bool seeding = false)
        {
            FileName = torrentMetaInfo.TorrentFileName;
            Status = TorrentStatus.Initialised;
            infoHash = torrentMetaInfo.GetInfoHash();
            trackerURL = torrentMetaInfo.GetTracker();
            (var totalDownloadLength, var allFilesToDownload) = torrentMetaInfo.LocalFilesToDownloadList(downloadPath);
            filesToDownload = allFilesToDownload;
            TotalBytesToDownload = totalDownloadLength;
            pieceLength = torrentMetaInfo.GetPieceLength();
            piecesInfoHash = torrentMetaInfo.GetPiecesInfoHash();
            numberOfPieces = ((piecesInfoHash.Length / Constants.HashLength));
            _pieceData = new PieceInfo[numberOfPieces];
            pieceWriteQueue = diskIO.pieceWriteQueue;
            pieceRequestQueue = diskIO.pieceRequestQueue;
            _SHA1 = new SHA1CryptoServiceProvider();
            downloadFinished = new ManualResetEvent(false);
            Bitfield = new byte[(int)Math.Ceiling((double)numberOfPieces / (double)8)];
            _piecesMissing = new byte[Bitfield.Length];
            selector = pieceSelector;
            peerSwarm = new ConcurrentDictionary<string, Peer>();
            assemblyData.pieceFinished = new ManualResetEvent(false);
            assemblyData.blockRequestsDone = new ManualResetEvent(false);
            assemblyData.cancelTaskSource = new CancellationTokenSource();
            assemblyData.guardMutex = new Mutex();
            paused = new ManualResetEvent(false);
            // In seeding mode mark eveything downloaded to save startup time
            diskIO.CreateLocalTorrentStructure(this);
            if (seeding)
            {
                diskIO.FullyDownloadedTorrentBitfield(this);
                TotalBytesDownloaded = TotalBytesToDownload = 0;
            }
            else
            {
                diskIO.CreateTorrentBitfield(this);
                TotalBytesToDownload -= TotalBytesDownloaded;
                TotalBytesDownloaded = 0;
            }
        }
        /// <summary>
        /// Sets a piece as downloaded.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="local">If set to <c>true</c> piece has been downloaded.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void MarkPieceLocal(UInt32 pieceNumber, bool local)
        {
            if (local)
            {
                Bitfield[pieceNumber >> 3] |= (byte)(0x80 >> (Int32)(pieceNumber & 0x7));
            }
            else
            {
                Bitfield[pieceNumber >> 3] &= (byte)~(0x80 >> (Int32)(pieceNumber & 0x7));
            }
        }
        /// <summary>
        /// Has a piece been fully downloaded.
        /// </summary>
        /// <returns><c>true</c>, if piece is local, <c>false</c> otherwise.</returns>
        /// <param name="pieceNumber">Piece number.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsPieceLocal(UInt32 pieceNumber)
        {
            return (Bitfield[pieceNumber >> 3] & 0x80 >> (Int32)(pieceNumber & 0x7)) != 0;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pieceNumber"></param>
        /// <param name="missing"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void MarkPieceMissing(UInt32 pieceNumber, bool missing)
        {
            if (missing)
            {
                if (!IsPieceMissing(pieceNumber))
                {
                    _piecesMissing[pieceNumber >> 3] |= (byte)(0x80 >> (Int32)(pieceNumber & 0x7));
                    missingPiecesCount++;
                }
            }
            else
            {
                if (IsPieceMissing(pieceNumber))
                {
                    _piecesMissing[pieceNumber >> 3] &= (byte)~(0x80 >> (Int32)(pieceNumber & 0x7));
                    missingPiecesCount--;
                }
            }
        }
        /// <summary>
        /// Is a piece missing from local peer.
        /// </summary>
        /// <param name="pieceNumber"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsPieceMissing(UInt32 pieceNumber)
        {
            return (_piecesMissing[pieceNumber >> 3] & 0x80 >> (Int32)(pieceNumber & 0x7)) != 0;
        }
        /// <summary>
        /// Merges the piece bitfield of a remote peer with the torrents local piece map data.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        internal void MergePieceBitfield(Peer remotePeer)
        {
            UInt32 pieceNumber = 0;
            for (int i = 0; i < remotePeer.RemotePieceBitfield.Length; i++)
            {
                for (byte bit = 0x80; bit != 0; bit >>= 1, pieceNumber++)
                {
                    if ((remotePeer.RemotePieceBitfield[i] & bit) != 0)
                    {
                        _pieceData[pieceNumber].peerCount++;
                        remotePeer.NumberOfMissingPieces--;
                    }
                }
            }
        }
        /// <summary>
        /// Checks the hash of a torrent piece on disc to see whether it has already been downloaded.
        /// </summary>
        /// <returns><c>true</c>, if piece hash agrees (it has been downloaded), <c>false</c> otherwise.</returns>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="pieceBuffer">Piece buffer.</param>
        /// <param name="numberOfBytes">Number of bytes.</param>
        internal bool CheckPieceHash(UInt32 pieceNumber, byte[] pieceBuffer, UInt32 numberOfBytes)
        {
            byte[] hash = _SHA1.ComputeHash(pieceBuffer, 0, (Int32)numberOfBytes);
            UInt32 pieceOffset = pieceNumber * Constants.HashLength;
            for (var byteNumber = 0; byteNumber < Constants.HashLength; byteNumber++)
            {
                if (hash[byteNumber] != piecesInfoHash[pieceOffset + byteNumber])
                {
                    return false;
                }
            }
            return true;
        }
        /// <summary>
        /// Calculate bytes left to download and report going negative.
        /// </summary>
        /// <returns>Bytes left in torrent to download</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal UInt64 BytesLeftToDownload()
        {
            if ((Int64)TotalBytesToDownload - (Int64)TotalBytesDownloaded < 0)
            {
                throw new BitTorrentException("Bytes left to download turned negative.");
            }
            return (UInt64)((Int64)TotalBytesToDownload - (Int64)TotalBytesDownloaded);
        }
        /// <summary>
        /// Un-merges disconnecting peer bitfield from piece information. At
        /// present this just involves decreasing its availability count.
        /// Note: Need to find where to put this call.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        internal void UnMergePieceBitfield(Peer remotePeer)
        {
            UInt32 pieceNumber = 0;
            for (int i = 0; i < remotePeer.RemotePieceBitfield.Length; i++)
            {
                for (byte bit = 0x80; bit != 0; bit >>= 1, pieceNumber++)
                {
                    if ((remotePeer.RemotePieceBitfield[i] & bit) != 0)
                    {
                        _pieceData[pieceNumber].peerCount--;
                    }
                }
            }
        }
        /// <summary>
        /// Get piece length in bytes
        /// </summary>
        /// <param name="peiceNumber"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal UInt32 PieceLength(UInt32 peiceNumber)
        {
            return _pieceData[peiceNumber].pieceLength;
        }
        /// <summary>
        /// Set piece length in bytes.
        /// </summary>
        /// <param name="pieceNumber"></param>
        /// <param name="pieceLength"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SetPieceLength(UInt32 pieceNumber, UInt32 pieceLength)
        {
            _pieceData[pieceNumber].pieceLength = pieceLength;
        }
        /// <summary>
        /// Check that ip not already in swarm and that maximum size hasnt been reached.
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsSpaceInSwarm(string ip)
        {
            return !peerSwarm.ContainsKey(ip) && (peerSwarm.Count < maximumSwarmSize);
        }
        /// <summary>
        /// Increment the peer count giving the number of peers with piece
        /// </summary>
        /// <param name="pieceNumber"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void IncrementPeerCount(UInt32 pieceNumber)
        {
            _pieceData[pieceNumber].peerCount++;
        }
        /// <summary>
        /// Find the next piece that the local torrent is missing. On finding a piece
        /// that is missing and is available on at least one peer returns a tuple<bool, UIInt32>
        /// that is true and has the piece number otherwise false and an unspecified value.
        /// </summary>
        /// <param name="startPiece"></param>
        /// <returns></returns>
        internal (bool, UInt32) FindNextMissingPiece(UInt32 startPiece)
        {
            startPiece %= (UInt32)numberOfPieces;
            UInt32 currentPiece = startPiece;
            do
            {
                if (IsPieceMissing(currentPiece) && (_pieceData[currentPiece].peerCount > 0))
                {
                    return (true, currentPiece);
                }
                currentPiece++;
                currentPiece %= (UInt32)numberOfPieces;
            } while (startPiece != currentPiece);
            return (false, currentPiece);
        }
        /// <summary>
        /// Calculate bytes per second of torrent download
        /// </summary>
        /// <returns></returns>
        internal Int64 BytesPerSecond() {
            double seconds = assemblyData.averageAssemblyTime.Get()/1000.0;
            if (seconds!=0) {
                return (Int64) (pieceLength/seconds);
            }else {
                return 0;
            }
        }
        /// <summary>
        /// Is peer in swarm. 
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        internal bool IsPeerInSwarm(string ip) {
            return peerSwarm.ContainsKey(ip);
        }
    }
}