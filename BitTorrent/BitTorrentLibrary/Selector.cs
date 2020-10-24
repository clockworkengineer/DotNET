//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Contains code and data to implement a specific piece
// selection method for piece download. In this case it just starts
// at beginning of the missing pieces bitfield and stops when it finds
// the first piece as flagged missing and returns its ordinal position within
// the bitfield.
//
// Copyright 2020.
//

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;

namespace BitTorrentLibrary
{
    public class Selector
    {
        private readonly DownloadContext _dc;       // Download context for torrent
        private readonly Object _nextPieceLock;     // Missing access lock

        /// <summary>
        /// Return next suggested piece to download.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="startPiece"></param>
        /// <returns></returns>
        private Int64 GetSuggestedPiece(Peer remotePeer, UInt32 startPiece = 0)
        {

            UInt32 currentPiece = startPiece;
            do
            {
                if (_dc.IsPieceMissing(currentPiece) && remotePeer.IsPieceOnRemotePeer(currentPiece))
                {
                    return currentPiece;
                }
                currentPiece++;
                currentPiece %= remotePeer.Dc.NumberOfPieces;
            } while (startPiece != currentPiece);

            return -1;

        }
        /// <summary>
        /// Setup data and resources needed by selector.
        /// </summary>
        /// <param name="dc"></param>
        public Selector(DownloadContext dc)
        {
            _dc = dc;
            _dc.PieceSelector = this;
            _nextPieceLock = new Object();
        }
        /// <summary>
        /// Selects the next piece to be downloaded.
        /// </summary>
        /// <returns><c>true</c>, if next piece was selected, <c>false</c> otherwise.</returns>
        /// <param name="remotePeer">Remote peer.</param>
        /// <param name="nextPiece">Next piece.</param>
        public bool NextPiece(Peer remotePeer, ref UInt32 nextPiece, CancellationToken cancelTask)
        {
            try
            {
                lock (_nextPieceLock)
                {
                    Int64 suggestedPiece = GetSuggestedPiece(remotePeer, 0);

                    if (suggestedPiece != -1)
                    {
                        nextPiece = (UInt32)suggestedPiece;
                        _dc.MarkPieceMissing(nextPiece, false);
                        return true;
                    }
                    return false;
                }

            }
            catch (Exception ex)
            {
                // Pass unknown exception up
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (Selector) Error: " + ex.Message);
            }

        }
        /// <summary>
        /// Set download finished flag.
        /// </summary>
        public void DownloadComplete()
        {
            _dc.DownloadFinished.Set();
        }
        /// <summary>
        /// Generate an array of pieces that are local but missing from the remote peer for input
        /// to Have packet requests sent to remote peer.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="numberOfSuggestions"></param>
        /// <returns></returns>
        public UInt32[] LocalPieceSuggestions(Peer remotePeer, UInt32 numberOfSuggestions, uint startPiece = 0)
        {
            List<UInt32> suggestions = new List<UInt32>();
            UInt32 currentPiece = startPiece;

            do
            {
                if (!remotePeer.IsPieceOnRemotePeer(currentPiece) && remotePeer.Dc.IsPieceLocal(currentPiece))
                {
                    suggestions.Add(currentPiece);
                    numberOfSuggestions--;
                }
                currentPiece++;
                currentPiece %= remotePeer.Dc.NumberOfPieces;
            } while ((startPiece != currentPiece) && (numberOfSuggestions > 0));

            return (suggestions.ToArray());

        }
    }

}
