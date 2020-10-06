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
    }

    /// <summary>
    /// Piece block map.
    /// </summary>
    public struct PieceBlockMap
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
        public PieceBlockMap[] PieceMap { get; set; }
        public BlockingCollection<PieceBuffer> PieceBufferWriteQueue { get; set; }
        public UInt64 TotalBytesDownloaded { get; set; }
        public UInt64 TotalBytesToDownload { get; set; }
        public uint PieceLength { get; set; }
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
                PieceLength = pieceLength;
                PiecesInfoHash = pieces;
                NumberOfPieces = ((UInt32)(pieces.Length / Constants.HashLength));
                BlocksPerPiece = pieceLength / Constants.BlockSize;
                PieceMap = new PieceBlockMap[NumberOfPieces];
                PieceBufferWriteQueue = new BlockingCollection<PieceBuffer>();
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
            }
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
                if (local)
                {
                    PieceMap[pieceNumber].mapping |= Mapping.HaveLocal;
                }
                else
                {
                    PieceMap[pieceNumber].mapping &= (Mapping.HaveLocal ^ 0xff);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BritTorent (DownloadConext) Error : " + ex.Message);
            }
        }

        /// <summary>
        /// Sets a piece as requested.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="requested">If set to <c>true</c> piece has been requested.</param>
        public void MarkPieceRequested(UInt32 pieceNumber, bool requested)
        {
            try
            {
                if (requested)
                {
                    PieceMap[pieceNumber].mapping |= Mapping.Requested;
                }
                else
                {
                    PieceMap[pieceNumber].mapping &= (Mapping.Requested ^ 0xff);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
            }
        }

        /// <summary>
        /// Sets a piece as present on remote peer.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="noPeer">If set to <c>true</c> piece is present on remote peer.</param>
        public void MarkPieceOnPeer(UInt32 pieceNumber, bool noPeer)
        {
            try
            {
                if (noPeer)
                {
                    PieceMap[pieceNumber].mapping |= Mapping.OnPeer;
                }
                else
                {
                    PieceMap[pieceNumber].mapping &= (Mapping.OnPeer ^ 0xff);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
            }
        }

        /// <summary>
        /// Has a  piece been requested from a peer.
        /// </summary>
        /// <returns><c>true</c>, if piece has been requested, <c>false</c> otherwise.</returns>
        /// <param name="pieceNumber">Piece number.</param>
        public bool IsPieceRequested(UInt32 pieceNumber)
        {
            try
            {
                return (PieceMap[pieceNumber].mapping & Mapping.Requested) == Mapping.Requested;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (DownloadConext) Error : " + ex.Message);
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
                return (PieceMap[pieceNumber].mapping & Mapping.HaveLocal) == Mapping.HaveLocal;
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
                            remotePeer.Dc.MarkPieceOnPeer(pieceNumber, true);
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