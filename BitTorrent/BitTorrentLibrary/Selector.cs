//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Contains code and data ti implement a specific piece
// selection method for piece download. In this case create a list of all
// non-local pieces that are available and place them in a queue in a random
// order.
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
        private readonly BlockingCollection<UInt32> _suggestedPieces;   // Suggested piece quee (collection defaults to FIFO queue)
        private readonly DownloadContext _dc;                           // Download context for torrent

        /// <summary>
        /// Build queue of pieces in random order.
        /// </summary>
        private void BuildSuggestedPiecesQueue()
        {

            List<UInt32> pieces = new List<UInt32>();
            Random rnd = new Random();
            foreach (var pieceNumber in Enumerable.Range(0, (int)_dc.NumberOfPieces))
            {
                if (!_dc.IsPieceLocal((UInt32)pieceNumber))
                {
                    pieces.Add((UInt32)pieceNumber);
                }
            }
            foreach (var piece in pieces.OrderBy(x => rnd.Next()).ToArray())
            {
                _suggestedPieces.Add(piece);
            }
        }
        /// <summary>
        /// Setup data and resources needed by selector.
        /// </summary>
        /// <param name="dc"></param>
        public Selector(DownloadContext dc)
        {
            _dc = dc;
            _dc.PieceSelector = this;
            _suggestedPieces = new BlockingCollection<UInt32>();
            BuildSuggestedPiecesQueue();
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

                // Only two correct ways out of this loop.
                // 1) Found a piece on peer to download so return true.
                // 2) The queue has has AddComplete called on it and Try() fires InvalidOperationException.
                while (true)
                {

                    nextPiece = (UInt32)_suggestedPieces.Take(cancelTask);

                    if (remotePeer.IsPieceOnRemotePeer(nextPiece))
                    {
                        return true;
                    }
                    else
                    {
                        Log.Logger.Debug($"REQUEUING PIECE {nextPiece}");
                        _suggestedPieces.Add(nextPiece);
                    }
                }
            }

            catch (InvalidOperationException ex)
            {
                // Queue is empty and has had AddCompleted called for it (ie. download complete)
                Log.Logger.Debug("NextPiece close down." + ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                // Pass unknown exception up
                Log.Logger.Debug(ex);
                throw new Error("BitTorrent (Selector) Error: "+ex.Message);
            }

            return false;
        }
        /// <summary>
        /// Places piece at end of queue.
        /// </summary>
        /// <param name="pieceNumber"></param>
        public void PutPieceBack(UInt32 pieceNumber)
        {
            if (!_suggestedPieces.IsCompleted)
            {
                _suggestedPieces.Add(pieceNumber);
            }
        }
        /// <summary>
        /// Close down piece queue when downloaded last piece.
        /// </summary>
        public void DownloadComplete()
        {
            _suggestedPieces.CompleteAdding();
        }

        public int PieceQueueSize() {
            return(_suggestedPieces.Count);
        }
    }
}
