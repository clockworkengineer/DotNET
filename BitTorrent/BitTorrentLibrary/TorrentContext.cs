//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Torrent being downloaded/seeded context. This includes
// download progress, a pieces to download bitfiejd and functions for 
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
    public struct PieceInfo
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
        public AsyncQueue<PieceBuffer> PieceWriteQueue { get; }             // Piece buffer disk write queue
        public AsyncQueue<PieceRequest> PieceRequestQueue { get; }          // Piece request queue
        public UInt64 TotalBytesDownloaded { get; set; }                    // Total bytes downloaded
        public UInt64 TotalBytesToDownload { get; set; }                    // Total bytes in torrent
        public UInt64 TotalBytesUploaded { get; set; }                      // Total bytes uploaded to all peers from torrent
        public uint PieceLength { get; }                                    // Length of piece in bytese
        public byte[] PiecesInfoHash { get; }                               // Pieces infohash from torrent file
        public uint NumberOfPieces { get; }                                 // Number of pieces into torrent
        public Selector PieceSelector { get; }                              // Piece selector
        public ManualResetEvent DownloadFinished { get; }                   // == Set then download finished
        public byte[] Bitfield { get; }                                     // Bitfield for current torrent on disk
        public List<FileDetails> FilesToDownload { get; }                   // Local files in torrent
        public byte[] InfoHash { get; }                                     // Torrent info hash
        public string TrackerURL { get; }                                   // Main Tracker URL
        public uint MissingPiecesCount { get; set; } = 0;                   // Missing piece count
        public int MaximumSwarmSize { get; } = Constants.MaximumSwarmSize;  // Maximim swarm size
        public ConcurrentDictionary<string, Peer> PeerSwarm { get; }        // Current peer swarm
        public TorrentStatus Status { get; set; }                           // Torrent status
        public Tracker MainTracker { get; set; }                            // Main tracker assigned to torrent
        public string FileName { get; set; }                                // Torrent file name
        public HashSet<string> PeerFilter;                                  // Peers that are not interes 
        public DownloadCompleteCallBack CallBack { get; set; }
        public object CallBackData { get; set; }

        /// <summary>
        /// Setup data and resources needed by torrent context.
        /// </summary>
        /// <param name="filesToDownload">Files to download.</param>
        /// <param name="pieceLength">Piece length.</param>
        /// <param name="pieces">Pieces.</param>
        public TorrentContext(MetaInfoFile torrentMetaInfo, Selector pieceSelector, Downloader downloader, string downloadPath)
        {
            FileName = torrentMetaInfo.TorrentFileName;
            Status = TorrentStatus.Ended;
            InfoHash = torrentMetaInfo.MetaInfoDict["info hash"];
            TrackerURL = Encoding.ASCII.GetString(torrentMetaInfo.MetaInfoDict["announce"]);
            (var totalDownloadLength, var filesToDownload) = torrentMetaInfo.LocalFilesToDownloadList(downloadPath);
            FilesToDownload = filesToDownload;
            TotalBytesToDownload = totalDownloadLength;
            PieceLength = uint.Parse(Encoding.ASCII.GetString(torrentMetaInfo.MetaInfoDict["piece length"]));
            PiecesInfoHash = torrentMetaInfo.MetaInfoDict["pieces"];
            NumberOfPieces = ((UInt32)(PiecesInfoHash.Length / Constants.HashLength));
            _pieceData = new PieceInfo[NumberOfPieces];
            PieceWriteQueue = downloader.PieceWriteQueue;
            PieceRequestQueue = downloader.PieceRequestQueue;
            _SHA1 = new SHA1CryptoServiceProvider();
            DownloadFinished = new ManualResetEvent(false);
            Bitfield = new byte[(int)Math.Ceiling((double)NumberOfPieces / (double)8)];
            _piecesMissing = new byte[Bitfield.Length];
            PieceSelector = pieceSelector;
            PeerSwarm = new ConcurrentDictionary<string, Peer>();
            downloader.CreateLocalTorrentStructure(this);
            downloader.CreateTorrentBitfield(this);
            TotalBytesToDownload -= TotalBytesDownloaded;
            TotalBytesDownloaded = 0;
        }
        /// <summary>
        /// Checks the hash of a torrent piece on disc to see whether it has already been downloaded.
        /// </summary>
        /// <returns><c>true</c>, if piece hash agrees (it has been downloaded), <c>false</c> otherwise.</returns>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="pieceBuffer">Piece buffer.</param>
        /// <param name="numberOfBytes">Number of bytes.</param>
        public bool CheckPieceHash(UInt32 pieceNumber, byte[] pieceBuffer, UInt32 numberOfBytes)
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
        public UInt64 BytesLeftToDownload()
        {
            if ((Int64)TotalBytesToDownload - (Int64)TotalBytesDownloaded < 0)
            {
                throw new Error("BitTorrent (DownloadConext) Error: Bytes left to download turned negative.");
            }
            return (UInt64)((Int64)TotalBytesToDownload - (Int64)TotalBytesDownloaded);
        }
        /// <summary>
        /// Sets a piece as downloaded.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="local">If set to <c>true</c> piece has been downloaded.</param>
        public void MarkPieceLocal(UInt32 pieceNumber, bool local)
        {
            try
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
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BritTorent (DownloadConext) Error : " + ex.Message);
            }
        }
        /// <summary>
        /// Has a piece been fully downloaded.
        /// </summary>
        /// <returns><c>true</c>, if piece is local, <c>false</c> otherwise.</returns>
        /// <param name="pieceNumber">Piece number.</param>
        public bool IsPieceLocal(UInt32 pieceNumber)
        {
            try
            {
                lock (_dcLock)
                {
                    return (Bitfield[pieceNumber >> 3] & 0x80 >> (Int32)(pieceNumber & 0x7)) != 0;
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
            }

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pieceNumber"></param>
        /// <param name="missing"></param>
        public void MarkPieceMissing(UInt32 pieceNumber, bool missing)
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
        public bool IsPieceMissing(UInt32 pieceNumber)
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
        public void MergePieceBitfield(Peer remotePeer)
        {
            try
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
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
            }
        }
        /// <summary>
        /// Get piece length in bytes
        /// </summary>
        /// <param name="peiceNumber"></param>
        /// <returns></returns>
        public UInt32 GetPieceLength(UInt32 peiceNumber)
        {
            return _pieceData[peiceNumber].pieceLength;
        }
        /// <summary>
        /// Set piece length in bytes.
        /// </summary>
        /// <param name="pieceNumber"></param>
        /// <param name="pieceLength"></param>
        public void SetPieceLength(UInt32 pieceNumber, UInt32 pieceLength)
        {
            _pieceData[pieceNumber].pieceLength = pieceLength;
        }
        /// <summary>
        /// Get number of blocks in piece.
        /// </summary>
        /// <param name="pieceNumber"></param>
        /// <returns></returns>
        public UInt32 GetBlocksInPiece(UInt32 pieceNumber)
        {
            UInt32 blockCount = (_pieceData[pieceNumber].pieceLength / Constants.BlockSize);
            if (_pieceData[pieceNumber].pieceLength % Constants.BlockSize != 0)
            {
                blockCount++;
            }
            return blockCount;
        }

    }
}