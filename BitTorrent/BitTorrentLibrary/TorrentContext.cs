//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Torrent being downloaded/seeded context. This includes
// download progress, a pieces to download bitfield and functions for 
// accessing this map to change a pieces status.
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

namespace BitTorrentLibrary
{
    public delegate void DownloadCompleteCallBack(Object callbackData);  // Download completed callback

    /// <summary>
    /// Piece assembly torrent context data
    /// </summary>
    internal struct AssemblerData
    {
        internal PieceBuffer pieceBuffer;                   // Assembled piece buffer
        internal Task task;                                 // Torrent piece assembly task
        internal ManualResetEvent waitForPieceAssembly;     // When event set then piece has been fully assembled
        internal CancellationTokenSource cancelTaskSource;  // Cancel assembler task source
        internal Average averageAssemblyTime;               // Average assembly time in milliseconds
        internal UInt64 totalTimeouts;                      // Timeouts while assembling pieces 
    }

    /// <summary>
    /// Piece Information.
    /// </summary>
    internal struct PieceInfo
    {
        public UInt16 peerCount;        // Peers with the piece
        public UInt32 pieceLength;      // Piece length in bytes
    }

    /// <summary>
    /// Torrent context for a torrent file.
    /// </summary>
    public class TorrentContext
    {
        private readonly SHA1 _SHA1;                                 // Object to create SHA1 piece info hash
        private readonly Object _dcLock = new object();              // Synchronization lock for torrent context
        private readonly byte[] _piecesMissing;                      // Missing piece bitfield
        private readonly PieceInfo[] _pieceData;                     // Piece information 
        internal Manager manager;                                    // Torrent context 
        internal ManualResetEvent paused;                            // == false (unset) pause downloading from peer
        internal AsyncQueue<PieceBuffer> pieceWriteQueue;            // Piece buffer disk write queue
        internal AsyncQueue<PieceRequest> pieceRequestQueue;         // Piece request queue
        internal uint pieceLength;                                   // Length of piece in bytese
        internal byte[] piecesInfoHash;                              // Pieces infohash from torrent file
        internal uint numberOfPieces;                                // Number of pieces into torrent
        internal Selector selector;                                  // Piece selector
        internal ManualResetEvent downloadFinished;                  // == Set then download finished
        internal byte[] Bitfield;                                    // Bitfield for current torrent on disk
        internal List<FileDetails> filesToDownload;                  // Local files in torrent
        internal byte[] infoHash;                                    // Torrent info hash
        internal string trackerURL;                                  // Main Tracker URL
        internal uint missingPiecesCount = 0;                        // Missing piece count
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
            infoHash = torrentMetaInfo.MetaInfoDict["info hash"];
            trackerURL = Encoding.ASCII.GetString(torrentMetaInfo.MetaInfoDict["announce"]);
            (var totalDownloadLength, var allFilesToDownload) = torrentMetaInfo.LocalFilesToDownloadList(downloadPath);
            filesToDownload = allFilesToDownload;
            TotalBytesToDownload = totalDownloadLength;
            pieceLength = uint.Parse(Encoding.ASCII.GetString(torrentMetaInfo.MetaInfoDict["piece length"]));
            piecesInfoHash = torrentMetaInfo.MetaInfoDict["pieces"];
            numberOfPieces = ((UInt32)(piecesInfoHash.Length / Constants.HashLength));
            _pieceData = new PieceInfo[numberOfPieces];
            pieceWriteQueue = diskIO.pieceWriteQueue;
            pieceRequestQueue = diskIO.pieceRequestQueue;
            _SHA1 = new SHA1CryptoServiceProvider();
            downloadFinished = new ManualResetEvent(false);
            Bitfield = new byte[(int)Math.Ceiling((double)numberOfPieces / (double)8)];
            _piecesMissing = new byte[Bitfield.Length];
            selector = pieceSelector;
            peerSwarm = new ConcurrentDictionary<string, Peer>();
            assemblyData.waitForPieceAssembly = new ManualResetEvent(false);
            assemblyData.pieceBuffer = new PieceBuffer(this, pieceLength);
            assemblyData.cancelTaskSource = new CancellationTokenSource();
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
        internal UInt64 BytesLeftToDownload()
        {
            if ((Int64)TotalBytesToDownload - (Int64)TotalBytesDownloaded < 0)
            {
                throw new Error("BitTorrent (TorrentContext) Error: Bytes left to download turned negative.");
            }
            return (UInt64)((Int64)TotalBytesToDownload - (Int64)TotalBytesDownloaded);
        }
        /// <summary>
        /// Sets a piece as downloaded.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="local">If set to <c>true</c> piece has been downloaded.</param>
        internal void MarkPieceLocal(UInt32 pieceNumber, bool local)
        {

            lock (_dcLock)
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

        }
        /// <summary>
        /// Has a piece been fully downloaded.
        /// </summary>
        /// <returns><c>true</c>, if piece is local, <c>false</c> otherwise.</returns>
        /// <param name="pieceNumber">Piece number.</param>
        internal bool IsPieceLocal(UInt32 pieceNumber)
        {
            lock (_dcLock)
            {
                return (Bitfield[pieceNumber >> 3] & 0x80 >> (Int32)(pieceNumber & 0x7)) != 0;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pieceNumber"></param>
        /// <param name="missing"></param>
        internal void MarkPieceMissing(UInt32 pieceNumber, bool missing)
        {

            lock (_dcLock)
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
        }

        /// <summary>
        /// Is a piece missing from local peer.
        /// </summary>
        /// <param name="pieceNumber"></param>
        /// <returns></returns>
        internal bool IsPieceMissing(UInt32 pieceNumber)
        {

            lock (_dcLock)
            {
                return (_piecesMissing[pieceNumber >> 3] & 0x80 >> (Int32)(pieceNumber & 0x7)) != 0;
            }
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
            remotePeer.BitfieldReceived.Set();
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
        internal UInt32 GetPieceLength(UInt32 peiceNumber)
        {
            return _pieceData[peiceNumber].pieceLength;
        }
        /// <summary>
        /// Set piece length in bytes.
        /// </summary>
        /// <param name="pieceNumber"></param>
        /// <param name="pieceLength"></param>
        internal void SetPieceLength(UInt32 pieceNumber, UInt32 pieceLength)
        {
            _pieceData[pieceNumber].pieceLength = pieceLength;
        }
        /// <summary>
        /// Get number of blocks in piece.
        /// </summary>
        /// <param name="pieceNumber"></param>
        /// <returns></returns>
        internal UInt32 GetBlocksInPiece(UInt32 pieceNumber)
        {
            UInt32 blockCount = (_pieceData[pieceNumber].pieceLength / Constants.BlockSize);
            if (_pieceData[pieceNumber].pieceLength % Constants.BlockSize != 0)
            {
                blockCount++;
            }
            return blockCount;
        }
        internal UInt32 PeersThatHavePiece(UInt32 pieceNumber)
        {
            return _pieceData[pieceNumber].peerCount;
        }
        /// <summary>
        /// Check that ip not already in swarm and that maximum size hasnt been reached.
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        internal bool IsSpaceInSwarm(string ip)
        {
            return !peerSwarm.ContainsKey(ip) && (peerSwarm.Count < maximumSwarmSize);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pieceNumber"></param>
        internal void IncrementPeerCount(UInt32 pieceNumber)
        {
            _pieceData[pieceNumber].peerCount++;
        }

    }
}