//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Download context for a torrent file being downloaded. This includes
// download progress, a pieces download map and functions for accessing this map to
// change a pieces status.
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
    /// Download context for a torrent file.
    /// </summary>
    public class DownloadContext
    {
        private readonly SHA1 _SHA1;                          // Object to create SHA1 piece info hash
        private readonly Object _dcLock = new object();
        private readonly byte[] _piecesMissing;     // Missing piece bitfield
        public PieceInfo[] PieceData { get; set; }
        public BlockingCollection<PieceBuffer> PieceWriteQueue { get; }
        public BlockingCollection<PieceRequest> PieceRequestQueue { get; }
        public UInt64 TotalBytesDownloaded { get; set; }
        public UInt64 TotalBytesToDownload { get; set; }
        public UInt64 TotalBytesUploaded { get; set; }
        public uint PieceLength { get;  }
        public uint BlocksPerPiece { get;  }
        public byte[] PiecesInfoHash { get; }
        public uint NumberOfPieces { get; }
        public Selector PieceSelector { get; }
        public ManualResetEvent DownloadFinished { get; }
        public byte[] Bitfield { get; }
        public List<FileDetails> FilesToDownload { get; }
        public byte[] InfoHash { get; }                                         // Torrent info hash
        public string TrackerURL { get; }                                       // Main Tracker URL
        public uint MissingPiecesCount { get; set; } = 0;
        public DownloadCompleteCallBack DownloadCompleteCallBack { get; set; }
        public object DownloadCompleteCallBackData { get; set; }
        public int MaximumSwarmSize { get; } = Constants.MaximumSwarmSize;  // Maximim swarm size
        public ConcurrentDictionary<string, Peer> PeerSwarm { get; }
        public TorrentStatus Status { get; set; }                                // Torrent status
        public Tracker MainTracker { get; set; }


        /// <summary>
        /// Setup data and resources needed by download context.
        /// </summary>
        /// <param name="filesToDownload">Files to download.</param>
        /// <param name="pieceLength">Piece length.</param>
        /// <param name="pieces">Pieces.</param>
        public DownloadContext(MetaInfoFile torrentMetaInfo, Selector pieceSelector, Downloader downloader, string downloadPath)
        {
            Status = TorrentStatus.Stopped;
            InfoHash = torrentMetaInfo.MetaInfoDict["info hash"];
            TrackerURL = Encoding.ASCII.GetString(torrentMetaInfo.MetaInfoDict["announce"]);
            (var totalDownloadLength, var filesToDownload) = torrentMetaInfo.LocalFilesToDownloadList(downloadPath);
            FilesToDownload = filesToDownload;
            TotalBytesToDownload = totalDownloadLength;
            PieceLength = uint.Parse(Encoding.ASCII.GetString(torrentMetaInfo.MetaInfoDict["piece length"]));
            PiecesInfoHash = torrentMetaInfo.MetaInfoDict["pieces"];
            NumberOfPieces = ((UInt32)(PiecesInfoHash.Length / Constants.HashLength));
            BlocksPerPiece = PieceLength / Constants.BlockSize;
            PieceData = new PieceInfo[NumberOfPieces];
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
            return (_piecesMissing[pieceNumber >> 3] & 0x80 >> (Int32)(pieceNumber & 0x7)) != 0;
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
                            PieceData[pieceNumber].peerCount++;
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
        /// 
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="callBackData"></param>
        public void SetDownloadCompleteCallBack(DownloadCompleteCallBack callback, Object callBackData)
        {
            DownloadCompleteCallBack = callback;
            DownloadCompleteCallBackData = callBackData;
        }

    }
}