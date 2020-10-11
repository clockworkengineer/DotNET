//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: 
//
// Copyright 2020.
//

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace BitTorrentLibrary
{
    public class Selector
    {
        private readonly ConcurrentQueue<UInt32> _suggestedPieces;
        private readonly DownloadContext _dc;

        /// <summary>
        /// 
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
                _suggestedPieces.Enqueue(piece);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dc"></param>
        public Selector(DownloadContext dc)
        {
            _dc = dc;
            _suggestedPieces = new ConcurrentQueue<uint>();
            BuildSuggestedPiecesQueue();
        }
        /// <summary>
        /// Selects the next piece to be downloaded.
        /// </summary>
        /// <returns><c>true</c>, if next piece was selected, <c>false</c> otherwise.</returns>
        /// <param name="remotePeer">Remote peer.</param>
        /// <param name="nextPiece">Next piece.</param>
        public bool NextPiece(Peer remotePeer, ref UInt32 nextPiece)
        {
            try
            {
                // Inorder to stop same the piece requested with different peers a lock 
                // is required when trying to get the next unrequested non-local pi

                while (!_suggestedPieces.IsEmpty)
                {
                    if (_suggestedPieces.TryDequeue(out nextPiece))
                    {

                        if (remotePeer.IsPieceOnRemotePeer(nextPiece))
                        {
                            return (true);
                        }
                        else
                        {
                            Log.Logger.Debug($"REQUEUING PIECE {nextPiece}");
                            _suggestedPieces.Enqueue(nextPiece);
                        }

                    }

                }
            }

            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
            }

            return false;
        }
        public void PutPieceBack(UInt32 pieceNumber)
        {
            _suggestedPieces.Enqueue(pieceNumber);
        }
    }
}
