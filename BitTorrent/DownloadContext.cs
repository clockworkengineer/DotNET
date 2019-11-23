//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Download context for a torrent file being downloaded. This includes
// download progress and a local piece download map.
//
// Copyright 2019.
//

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace BitTorrent
{
    /// <summary>
    /// Possible piece mapping values.
    /// </summary>
    public static class Mapping
    {
        public const byte HaveLocal = 0x01; // Downloaded
        public const byte Requested = 0x2;  // Has been requested
        public const byte OnPeer = 0x04;    // Is present ona remote peer
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
        public PieceBlockMap[] pieceMap;                                // Piece map for current download
        public BlockingCollection<PieceBuffer> pieceBufferWriteQueue;   // Write buffer for pieces towrite to files
        public UInt64 totalBytesDownloaded;                             // Total downloaded bytes
        public UInt64 totalLength;                                      // Total length of torrent files
        public UInt32 pieceLength;                                      // Piece length in bytes
        public UInt32 blocksPerPiece;                                   // Blocks per piece
        public byte[] piecesInfoHash;                                   // Pieces info hash checksum
        public UInt32 numberOfPieces;                                   // Number of pieces to transfer

        /// <summary>
        /// Initializes a new instance of the DownloadContext class.
        /// </summary>
        /// <param name="filesToDownload">Files to download.</param>
        /// <param name="pieceLength">Piece length.</param>
        /// <param name="pieces">Pieces.</param>
        public DownloadContext(List<FileDetails> filesToDownload, UInt32 pieceLength, byte[] pieces)
        {
            try
            {
                foreach (var file in filesToDownload)
                {
                    totalLength += file.length;
                }

                this.pieceLength = pieceLength;
                piecesInfoHash = pieces;
                numberOfPieces = ((UInt32)(pieces.Length / Constants.kHashLength));
                blocksPerPiece = pieceLength / Constants.kBlockSize;

                pieceMap = new PieceBlockMap[numberOfPieces];
                for (var pieceNuber = 0; pieceNuber < numberOfPieces; pieceNuber++)
                {
                    pieceMap[pieceNuber].blocks = new byte[blocksPerPiece];
                }

                pieceBufferWriteQueue = new BlockingCollection<PieceBuffer>(10);

            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
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
                    pieceMap[pieceNumber].blocks[blockNumber] |= Mapping.HaveLocal;
                }
                else
                {
                    pieceMap[pieceNumber].blocks[blockNumber] &= (Mapping.HaveLocal ^ 0xff);
                }

            }
            catch (Error)
            {
                throw;
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
                    pieceMap[pieceNumber].blocks[blockNumber] |= Mapping.Requested;
                }
                else
                {
                    pieceMap[pieceNumber].blocks[blockNumber] &= (Mapping.Requested ^ 0xff);
                }

            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
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
                    pieceMap[pieceNumber].blocks[blockNumber] |= Mapping.OnPeer;
                }
                else
                {
                    pieceMap[pieceNumber].blocks[blockNumber] &= (Mapping.OnPeer ^ 0xff);
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }

        /// <summary>
        /// Sets a block as having been downloaed from a peer.
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
                    pieceMap[pieceNumber].blocks[blockNumber] |= Mapping.OnPeer;
                    pieceMap[pieceNumber].blocks[blockNumber] |= Mapping.HaveLocal;
                }
                else
                {
                    pieceMap[pieceNumber].blocks[blockNumber] &= (Mapping.HaveLocal ^ 0xff);
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }

        /// <summary>
        /// Sets a block as last withina piece.
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
                    pieceMap[pieceNumber].blocks[blockNumber] |= Mapping.LastBlock;
                }
                else
                {
                    pieceMap[pieceNumber].blocks[blockNumber] &= (Mapping.LastBlock ^ 0xff);
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
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
                return ((pieceMap[pieceNumber].blocks[blockNumber] & Mapping.OnPeer) == Mapping.OnPeer);
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
            return (false);
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
                return ((pieceMap[pieceNumber].blocks[blockNumber] & Mapping.HaveLocal) == Mapping.HaveLocal);
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
            return (false);
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
                return ((pieceMap[pieceNumber].blocks[blockNumber] & Mapping.Requested) == Mapping.Requested);
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
            return (false);
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
                return ((pieceMap[pieceNumber].blocks[blockNumber] & Mapping.LastBlock) == Mapping.LastBlock);
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
            return (false);
        }

        /// <summary>
        /// Gets the length of the piece in bytes.
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
                    length += Constants.kBlockSize;
                }
                length += pieceMap[pieceNumber].lastBlockLength;
            }

            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }

            return (length);

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
                        return (false);
                    }
                }
                if (!IsBlockPieceLocal(pieceNumber, blockNumber))
                {
                    return (false);
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
            return (true);
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
                            pieceMap[pieceNumber].peerCount++;
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
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
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
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
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
                }
                BlockPieceRequested(pieceNumber, blockNumber, false);
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }

        /// <summary>
        /// Checks to see if there are any missing blocks in the local piecemap and reports them.
        /// Used to check whether we have the complete file avaialable on peers todownload.
        /// </summary>
        public void CheckForMissingBlocksFromPeers()
        {
            try
            {
                for (UInt32 pieceNumber = 0; pieceNumber < numberOfPieces; pieceNumber++)
                {
                    UInt32 blockNumber = 0;
                    for (;  !IsBlockPieceLast(pieceNumber, blockNumber); blockNumber++)
                    {
                       if ((pieceMap[pieceNumber].blocks[blockNumber] & Mapping.OnPeer)==0)
                        {
                            Console.WriteLine($"{pieceNumber} { blockNumber}");
                        }
                    }
                    if ((pieceMap[pieceNumber].blocks[blockNumber] & Mapping.OnPeer) == 0)
                    {
                        Program.Logger.Info($"Piece {pieceNumber} Block {blockNumber} missing from all peers.");
                    }
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }
    }
}
