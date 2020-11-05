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
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading;

namespace BitTorrentLibrary
{
    public delegate void DownloadCompleteCallBack(Object callbackData);  // Download completed callback

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
        private readonly SHA1 _SHA1;                                        // Object to create SHA1 piece info hash
        private readonly Object _dcLock = new object();                     // Synchronization lock for torrent context
        private readonly byte[] _piecesMissing;                             // Missing piece bitfield
        private readonly PieceInfo[] _pieceData;                            // Piece information
        internal AsyncQueue<PieceBuffer> PieceWriteQueue { get; }             // Piece buffer disk write queue
        internal AsyncQueue<PieceRequest> PieceRequestQueue { get; }          // Piece request queue
        internal uint PieceLength { get; }                                    // Length of piece in bytese
        internal byte[] PiecesInfoHash { get; }                               // Pieces infohash from torrent file
        internal uint NumberOfPieces { get; }                                 // Number of pieces into torrent
        internal Selector PieceSelector { get; }                              // Piece selector
        internal ManualResetEvent DownloadFinished { get; }                   // == Set then download finished
        internal byte[] Bitfield { get; }                                     // Bitfield for current torrent on disk
        internal List<FileDetails> FilesToDownload { get; }                   // Local files in torrent
        internal byte[] InfoHash { get; }                                     // Torrent info hash
        internal string TrackerURL { get; }                                   // Main Tracker URL
        internal uint MissingPiecesCount { get; set; } = 0;                   // Missing piece count
        internal int MaximumSwarmSize { get; } = Constants.MaximumSwarmSize;  // Maximim swarm size
        internal ConcurrentDictionary<string, Peer> PeerSwarm { get; }        // Current peer swarm
        internal Tracker MainTracker { get; set; }                            // Main tracker assigned to torrent
        public TorrentStatus Status { get; set; }                             // Torrent status
        public string FileName { get; set; }                                  // Torrent file name
        public UInt64 TotalBytesDownloaded { get; set; }                      // Total bytes downloaded
        public UInt64 TotalBytesToDownload { get; set; }                      // Total bytes in torrent
        public UInt64 TotalBytesUploaded { get; set; }                        // Total bytes uploaded to all peers from torrent
        public DownloadCompleteCallBack CallBack { get; set; }                // Download complete callback
        public object CallBackData { get; set; }                              // Download complete callback data

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
            InfoHash = torrentMetaInfo.MetaInfoDict["info hash"];
            TrackerURL = Encoding.ASCII.GetString(torrentMetaInfo.MetaInfoDict["announce"]);
            (var totalDownloadLength, var filesToDownload) = torrentMetaInfo.LocalFilesToDownloadList(downloadPath);
            FilesToDownload = filesToDownload;
            TotalBytesToDownload = totalDownloadLength;
            PieceLength = uint.Parse(Encoding.ASCII.GetString(torrentMetaInfo.MetaInfoDict["piece length"]));
            PiecesInfoHash = torrentMetaInfo.MetaInfoDict["pieces"];
            NumberOfPieces = ((UInt32)(PiecesInfoHash.Length / Constants.HashLength));
            _pieceData = new PieceInfo[NumberOfPieces];
            PieceWriteQueue = diskIO.PieceWriteQueue;
            PieceRequestQueue = diskIO.PieceRequestQueue;
            _SHA1 = new SHA1CryptoServiceProvider();
            DownloadFinished = new ManualResetEvent(false);
            Bitfield = new byte[(int)Math.Ceiling((double)NumberOfPieces / (double)8)];
            _piecesMissing = new byte[Bitfield.Length];
            PieceSelector = pieceSelector;
            PeerSwarm = new ConcurrentDictionary<string, Peer>();
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
                if (hash[byteNumber] != PiecesInfoHash[pieceOffset + byteNumber])
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
                        MissingPiecesCount++;
                    }
                }
                else
                {
                    if (IsPieceMissing(pieceNumber))
                    {
                        _piecesMissing[pieceNumber >> 3] &= (byte)~(0x80 >> (Int32)(pieceNumber & 0x7));
                        MissingPiecesCount--;
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
        /// <summary>
        /// Check that ip not already in swarm and that maximum size hasnt been reached.
        /// </summary>
        /// <param name="ip"></param>
        /// <returns></returns>
        internal bool IsSpaceInSwarm(string ip)
        {
            return !PeerSwarm.ContainsKey(ip) && (PeerSwarm.Count < MaximumSwarmSize);
        }

    }
}