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
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading;

namespace BitTorrentLibrary
{
    /// <summary>
    /// Possible piece mapping values.
    /// </summary>
    public static class Mapping
    {
        public const byte HaveLocal = 0x01; // Downloaded
    }

    /// <summary>
    /// Piece block map.
    /// </summary>
    public struct PieceData
    {
        public byte mapping;            // Piece mapping
        public UInt16 peerCount;        // Peers with the piece
        public UInt32 pieceLength;      // Piece length in bytes
    }

    /// <summary>
    /// Download context for a torrent file.
    /// </summary>
    public class DownloadContext
    {
        private byte[] bitfield;                              // Local piece bitfield
        private readonly SHA1 _SHA1;                          // Object to create SHA1 piece info hash
        private readonly Object _PieceMapLock = new object();
        public PieceData[] PieceMap { get; set; }
        public BlockingCollection<PieceBuffer> PieceBufferWriteQueue { get; set; }
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

        /// <summary>
        ///Setup data and resources needed by download context.
        /// </summary>
        /// <param name="filesToDownload">Files to download.</param>
        /// <param name="pieceLength">Piece length.</param>
        /// <param name="pieces">Pieces.</param>
        public DownloadContext(UInt64 totalDownloadLength, UInt32 pieceLength, byte[] pieces)
        {
            TotalBytesToDownload = totalDownloadLength;
            PieceLength = pieceLength;
            PiecesInfoHash = pieces;
            NumberOfPieces = ((UInt32)(pieces.Length / Constants.HashLength));
            BlocksPerPiece = pieceLength / Constants.BlockSize;
            PieceMap = new PieceData[NumberOfPieces];
            PieceBufferWriteQueue = new BlockingCollection<PieceBuffer>(10);
            PieceRequestQueue = new BlockingCollection<PieceRequest>();
            _SHA1 = new SHA1CryptoServiceProvider();
            DownloadFinished = new ManualResetEvent(false);
            bitfield = new byte[(int)Math.Ceiling((double)NumberOfPieces / (double)8)];
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
                lock (_PieceMapLock)
                {
                    if (local)
                    {
                        bitfield[pieceNumber >> 3] |= (byte)(0x80 >> (Int32)(pieceNumber & 0x7));
                    }
                    else
                    {
                        bitfield[pieceNumber >> 3] &= (byte) ~(0x80 >> (Int32)(pieceNumber & 0x7));
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
                lock (_PieceMapLock)
                {
                    return (bitfield[pieceNumber >> 3] & 0x80 >> (Int32)(pieceNumber & 0x7)) != 0;
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
            }

        }

        /// <summary>
        /// Merges the piece bitfield of a remote peer with the torrents piece map.
        /// </summary>
        /// <param name="remotePeer">Remote peer.</param>
        public void MergePieceBitfield(Peer remotePeer)
        {
            try
            {
                UInt32 pieceNumber = 0;
                for (int i = 0; i < remotePeer.RemotePieceBitfield.Length; i++)
                {
                    byte map = remotePeer.RemotePieceBitfield[i];
                    for (byte bit = 0x80; bit != 0; bit >>= 1, pieceNumber++)
                    {
                        if ((map & bit) != 0)
                        {
                            PieceMap[pieceNumber].peerCount++;
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
        /// Build local piece bitfield to send to remote peer.
        /// </summary>
        public byte[] BuildLocalPieceBitfield()
        {
            try
            {

                byte[] bitfield = new byte[(int)Math.Ceiling((double)NumberOfPieces / (double)8)];

                for (UInt32 pieceNumber = 0; pieceNumber < NumberOfPieces; pieceNumber++)
                {
                    for (byte bit = 0x80; bit != 0; bit >>= 1)
                    {
                        if (IsPieceLocal(pieceNumber))
                        {
                            bitfield[pieceNumber >> 3] |= bit;
                        }
                    }

                }
                return (bitfield);

            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
            }
        }

    }
}