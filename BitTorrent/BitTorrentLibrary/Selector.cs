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
using System.Collections.Generic;
using System.Threading;

namespace BitTorrentLibrary
{
    public class Selector
    {

        /// <summary>
        /// Return next suggested piece to download.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="startPiece"></param>
        /// <returns></returns>
        private Int64 GetSuggestedPiece(TorrentContext tc, UInt32 startPiece)
        {

            UInt32 currentPiece = startPiece;
            do
            {
                if (tc.IsPieceMissing(currentPiece) && (tc.PeersThatHavePiece(currentPiece) > 0))
                {
                    return currentPiece;
                }
                currentPiece++;
                currentPiece %= tc.NumberOfPieces;
            } while (startPiece != currentPiece);

            return -1;

        }
        /// <summary>
        /// Setup data and resources needed by selector.
        /// </summary>
        /// <param name="dc"></param>
        public Selector()
        {
        }
        internal bool NextPiece(TorrentContext tc, ref UInt32 nextPiece, UInt32 startPiece, CancellationToken _)
        {
            bool pieceSuggested = false;

            try
            {

                Int64 suggestedPiece = GetSuggestedPiece(tc, startPiece);

                if (suggestedPiece != -1)
                {
                    nextPiece = (UInt32)suggestedPiece;
                    tc.MarkPieceMissing(nextPiece, false);
                    pieceSuggested = true;
                }

            }
            catch (Exception ex)
            {
                // Pass unknown exception up
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (Selector) Error: " + ex.Message);
            }

            return pieceSuggested;

        }
        /// <summary>
        /// Generate an array of pieces that are local but missing from the remote peer for input
        /// to Have packet requests sent to remote peer.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="numberOfSuggestions"></param>
        /// <param name="startPiece"></param>
        /// <returns></returns>
        internal UInt32[] LocalPieceSuggestions(Peer remotePeer, UInt32 numberOfSuggestions, uint startPiece = 0)
        {
            List<UInt32> suggestions = new List<UInt32>();
            UInt32 currentPiece = startPiece;

            do
            {
                if (!remotePeer.IsPieceOnRemotePeer(currentPiece) && remotePeer.Tc.IsPieceLocal(currentPiece))
                {
                    suggestions.Add(currentPiece);
                    numberOfSuggestions--;
                }
                currentPiece++;
                currentPiece %= remotePeer.Tc.NumberOfPieces;
            } while ((startPiece != currentPiece) && (numberOfSuggestions > 0));

            return (suggestions.ToArray());

        }
    }

}
