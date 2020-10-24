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
        public PieceInfo[] PieceData { get; set; }
        public BlockingCollection<PieceBuffer> PieceWriteQueue { get; set; }
        public BlockingCollection<PieceRequest> PieceRequestQueue { get; set; }
        public UInt64 TotalBytesDownloaded { get; set; }
        public UInt64 TotalBytesToDownload { get; set; }
        public UInt64 TotalBytesUploaded { get; set; }
        public uint PieceLength { get; set; }
        public uint BlocksPerPiece { get; set; }
        public byte[] PiecesInfoHash { get; set; }
        public uint NumberOfPieces { get; set; }
        public Selector PieceSelector { get; set; }
        public ManualResetEvent DownloadFinished { get; }
        public byte[] Bitfield { get; }
        public List<FileDetails> FilesToDownload { get; }
        public byte[] InfoHash { get; }                                    // Torrent info hash
        public string TrackerURL { get; }                                  // Main Tracker URL

        /// <summary>
        ///Setup data and resources needed by download context.
        /// </summary>
        /// <param name="filesToDownload">Files to download.</param>
        /// <param name="pieceLength">Piece length.</param>
        /// <param name="pieces">Pieces.</param>
        public DownloadContext(MetaInfoFile torrentMetaInfo, string downloadPath)
        {
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
            PieceWriteQueue = new BlockingCollection<PieceBuffer>();
            PieceRequestQueue = new BlockingCollection<PieceRequest>();
            _SHA1 = new SHA1CryptoServiceProvider();
            DownloadFinished = new ManualResetEvent(false);
            Bitfield = new byte[(int)Math.Ceiling((double)NumberOfPieces / (double)8)];
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

    }
}