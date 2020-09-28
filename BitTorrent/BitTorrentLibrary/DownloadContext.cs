//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Download context for a torrent file being downloaded. This includes
// download progress, a pieces download map and functions for accessing this map to
// change a pieces status.
//
// Copyright 2019.
//

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace BitTorrentLibrary
{
    /// <summary>
    /// Possible piece mapping values.
    /// </summary>
    public static class Mapping
    {
        public const byte HaveLocal = 0x01; // Downloaded
        public const byte Requested = 0x2;  // Has been requested
        public const byte OnPeer = 0x04;    // Is present on a remote peer
        public const byte LastBlock = 0x08; // Last block in a piece
    }

    /// <summary>
    /// Piece block map.
    /// </summary>
    public struct PieceBlockMap
    {
        public byte[] blocks;           // Block mappings for piece
        public UInt16 peerCount;        // Peers with the piece
        public UInt32 lastBlockLength;  // Length of last block in piece
    }

    /// <summary>
    /// Download context for a torrent file.
    /// </summary>
    public class DownloadContext
    {

        public PieceBlockMap[] PieceMap { get; set; }
        public BlockingCollection<PieceBuffer> PieceBufferWriteQueue { get; set; }
        public ulong TotalBytesDownloaded { get; set; }
        public ulong TotalBytesToDownload { get; set; }
        public uint PieceLengthInBytes { get; set; }
        public uint BlocksPerPiece { get; set; }
        public byte[] PiecesInfoHash { get; set; }
        public uint NumberOfPieces { get; set; }

        /// <summary>
        /// Initializes a new instance of the DownloadContext class.
        /// </summary>
        /// <param name="filesToDownload">Files to download.</param>
        /// <param name="pieceLength">Piece length.</param>
        /// <param name="pieces">Pieces.</param>
        public DownloadContext(UInt64 totalDownloadLength, UInt32 pieceLength, byte[] pieces)
        {
            try
            {
                TotalBytesToDownload = totalDownloadLength;
                PieceLengthInBytes = pieceLength;
                PiecesInfoHash = pieces;
                NumberOfPieces = ((UInt32)(pieces.Length / Constants.HashLength));
                BlocksPerPiece = pieceLength / Constants.BlockSize;

                PieceMap = new PieceBlockMap[NumberOfPieces];
                for (var pieceNumber = 0; pieceNumber < NumberOfPieces; pieceNumber++)
                {
                    PieceMap[pieceNumber].blocks = new byte[BlocksPerPiece];
                }

                PieceBufferWriteQueue = new BlockingCollection<PieceBuffer>(10);
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
            }
        }

        /// <summary>
        /// Sets a block piece as downloaded.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="blockNumber">Block number.</param>
        /// <param name="local">If set to <c>true</c> block has been downloaded.</param>
        public void BlockPieceLocal(UInt32 pieceNumber, UInt32 blockNumber, bool local)
        {
            try
            {
                if (local)
                {
                    PieceMap[pieceNumber].blocks[blockNumber] |= Mapping.HaveLocal;
                }
                else
                {
                    PieceMap[pieceNumber].blocks[blockNumber] &= (Mapping.HaveLocal ^ 0xff);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BritTorent (DownloadConext) Error : " + ex.Message);
            }
        }

        /// <summary>
        /// Sets a block piece as requested.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="blockNumber">Block number.</param>
        /// <param name="requested">If set to <c>true</c> block has been requested.</param>
        public void BlockPieceRequested(UInt32 pieceNumber, UInt32 blockNumber, bool requested)
        {
            try
            {
                if (requested)
                {
                    PieceMap[pieceNumber].blocks[blockNumber] |= Mapping.Requested;
                }
                else
                {
                    PieceMap[pieceNumber].blocks[blockNumber] &= (Mapping.Requested ^ 0xff);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
            }
        }

        /// <summary>
        /// Sets a block piece as present on remote peer.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="blockNumber">Block number.</param>
        /// <param name="noPeer">If set to <c>true</c> block is present on remote peer.</param>
        public void BlockPieceOnPeer(UInt32 pieceNumber, UInt32 blockNumber, bool noPeer)
        {
            try
            {
                if (noPeer)
                {
                    PieceMap[pieceNumber].blocks[blockNumber] |= Mapping.OnPeer;
                }
                else
                {
                    PieceMap[pieceNumber].blocks[blockNumber] &= (Mapping.OnPeer ^ 0xff);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
            }
        }

        /// <summary>
        /// Sets a block as having been downloaded from a peer.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="blockNumber">Block number.</param>
        /// <param name="downloaded">If set to <c>true</c> black has been downloaded.</param>
        public void BlockPieceDownloaded(UInt32 pieceNumber, UInt32 blockNumber, bool downloaded)
        {
            try
            {
                if (downloaded)
                {
                    PieceMap[pieceNumber].blocks[blockNumber] |= Mapping.OnPeer;
                    PieceMap[pieceNumber].blocks[blockNumber] |= Mapping.HaveLocal;
                }
                else
                {
                    PieceMap[pieceNumber].blocks[blockNumber] &= (Mapping.HaveLocal ^ 0xff);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
            }
        }

        /// <summary>
        /// Sets a block as last within a piece.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="blockNumber">Block number.</param>
        /// <param name="last">If set to <c>true</c> block is the last in a piece.</param>
        public void BlockPieceLast(UInt32 pieceNumber, UInt32 blockNumber, bool last)
        {
            try
            {
                if (last)
                {
                    PieceMap[pieceNumber].blocks[blockNumber] |= Mapping.LastBlock;
                }
                else
                {
                    PieceMap[pieceNumber].blocks[blockNumber] &= (Mapping.LastBlock ^ 0xff);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
            }
        }

        /// <summary>
        /// Is a block of  piece present on a remote peer.
        /// </summary>
        /// <returns><c>true</c>, if block piece on peer. <c>false</c> otherwise.</returns>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="blockNumber">Block number.</param>
        public bool IsBlockPieceOnPeer(UInt32 pieceNumber, UInt32 blockNumber)
        {
            try
            {
                return (PieceMap[pieceNumber].blocks[blockNumber] & Mapping.OnPeer) == Mapping.OnPeer;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
            }
        }

        /// <summary>
        /// Is a block piece local (ie has been downloaded).
        /// </summary>
        /// <returns><c>true</c>, if block piece is local <c>false</c> otherwise.</returns>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="blockNumber">Block number.</param>
        public bool IsBlockPieceLocal(UInt32 pieceNumber, UInt32 blockNumber)
        {
            try
            {
                return (PieceMap[pieceNumber].blocks[blockNumber] & Mapping.HaveLocal) == Mapping.HaveLocal;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
            }
        }

        /// <summary>
        /// Has a block piece been requested from a peer.
        /// </summary>
        /// <returns><c>true</c>, if block piece has been requested, <c>false</c> otherwise.</returns>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="blockNumber">Block number.</param>
        public bool IsBlockPieceRequested(UInt32 pieceNumber, UInt32 blockNumber)
        {
            try
            {
                return (PieceMap[pieceNumber].blocks[blockNumber] & Mapping.Requested) == Mapping.Requested;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
            }
        }

        /// <summary>
        /// Is a block the last within piece.
        /// </summary>
        /// <returns><c>true</c>, if block piece last within a piece, <c>false</c> otherwise.</returns>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="blockNumber">Block number.</param>
        public bool IsBlockPieceLast(UInt32 pieceNumber, UInt32 blockNumber)
        {
            try
            {
                return (PieceMap[pieceNumber].blocks[blockNumber] & Mapping.LastBlock) == Mapping.LastBlock;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
            }
        }

        /// <summary>
        /// Get the length of a piece in bytes.
        /// </summary>
        /// <returns>The piece length.</returns>
        /// <param name="pieceNumber">Piece number.</param>
        public UInt32 GetPieceLength(UInt32 pieceNumber)
        {
            UInt32 length = 0;

            try
            {
                for (UInt32 blockNumber = 0; !IsBlockPieceLast(pieceNumber, blockNumber); blockNumber++)
                {
                    length += Constants.BlockSize;
                }
                length += PieceMap[pieceNumber].lastBlockLength;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
            }
            return length;
        }

        /// <summary>
        /// Has a piece been assembled.
        /// </summary>
        /// <returns><c>true</c>, if piece been assembled, <c>false</c> otherwise.</returns>
        /// <param name="pieceNumber">Piece number.</param>
        public bool HasPieceBeenAssembled(UInt32 pieceNumber)
        {
            try
            {
                UInt32 blockNumber = 0;
                for (; !IsBlockPieceLast(pieceNumber, blockNumber); blockNumber++)
                {
                    if (!IsBlockPieceLocal(pieceNumber, blockNumber))
                    {
                        return false;
                    }
                }
                if (!IsBlockPieceLocal(pieceNumber, blockNumber))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
            }
            return true;
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
                            UInt32 blockNumber = 0;
                            for (; !IsBlockPieceLast(pieceNumber, blockNumber); blockNumber++)
                            {
                                remotePeer.TorrentDownloader.Dc.BlockPieceOnPeer(pieceNumber, blockNumber, true);
                            }
                            remotePeer.TorrentDownloader.Dc.BlockPieceOnPeer(pieceNumber, blockNumber, true);
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
        /// Mark all blocks of a piece as requested.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        public void MarkPieceRequested(UInt32 pieceNumber)
        {
            try
            {
                UInt32 blockNumber = 0;
                for (; !IsBlockPieceLast(pieceNumber, blockNumber); blockNumber++)
                {
                    BlockPieceRequested(pieceNumber, blockNumber, true);
                }
                BlockPieceRequested(pieceNumber, blockNumber, true);
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
            }
        }

        /// <summary>
        /// Mark all blocks of a piece as unrequested.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        public void MarkPieceNotRequested(UInt32 pieceNumber)
        {
            try
            {
                UInt32 blockNumber = 0;
                for (; !IsBlockPieceLast(pieceNumber, blockNumber); blockNumber++)
                {
                    BlockPieceRequested(pieceNumber, blockNumber, false);
                    BlockPieceLocal(pieceNumber, blockNumber, false);
                }
                BlockPieceRequested(pieceNumber, blockNumber, false);
                BlockPieceLocal(pieceNumber, blockNumber, false);
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
            }
        }
    }
}