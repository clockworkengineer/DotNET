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
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;

namespace BitTorrentLibrary
{
    public class Assembler
    {
        private ConcurrentQueue<UInt32> _suggestedPieces;
        private readonly Object _pieceLock = new object();           // Piece Lock     
        private readonly ProgessCallBack _progressFunction = null;   // Download progress function
        private readonly Object _progressData = null;                // Download progress function data
        private readonly DownloadContext _dc;                        // Download context for torrent
        public ManualResetEvent Paused { get; set; }                 // == true (set) pause downloading from peer
        public int ActiveAssemblerTasks { get; set; } = 0;           // Active Assembler tasks

        /// <summary>
        /// 
        /// </summary>
        private void BuildSuggestedPiecesQueue()
        {
            _suggestedPieces = new ConcurrentQueue<uint>();
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
        /// Selects the next piece to be downloaded.
        /// </summary>
        /// <returns><c>true</c>, if next piece was selected, <c>false</c> otherwise.</returns>
        /// <param name="remotePeer">Remote peer.</param>
        /// <param name="nextPiece">Next piece.</param>
        private bool SelectNextPiece(Peer remotePeer, ref UInt32 nextPiece)
        {
            try
            {
                // Inorder to stop same the piece requested with different peers a lock 
                // is required when trying to get the next unrequested non-local pi

                while (!_suggestedPieces.IsEmpty)
                {
                    if (_suggestedPieces.TryDequeue(out nextPiece))
                    {
                        {
                            if (remotePeer.Dc.IsPieceLocal(nextPiece))
                            {
                                continue;
                            }
                            if (remotePeer.IsPieceOnRemotePeer(nextPiece))
                            {
                                //                              _dc.MarkPieceRequested(nextPiece, true);
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
        /// <summary>
        /// Queue sucessfully assembled piece or flag for redownload.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="pieceNumber"></param>
        /// <param name="pieceAssembled"></param>
        public void SavePieceToDisk(Peer remotePeer, UInt32 pieceNumber, bool pieceAssembled)
        {

            if (pieceAssembled)
            {
                bool pieceValid = _dc.CheckPieceHash(pieceNumber, remotePeer.AssembledPiece.Buffer, _dc.PieceMap[pieceNumber].pieceLength);
                if (pieceValid)
                {
                    Log.Logger.Debug($"All blocks for piece {pieceNumber} received");
                    _dc.PieceBufferWriteQueue.Add(new PieceBuffer(remotePeer.AssembledPiece));
                    _progressFunction?.Invoke(_progressData);
                }
                else
                {
                    Log.Logger.Debug("PIECE CONTAINED INVALID INFOHASH.");
                    Log.Logger.Debug($"REQUEUING PIECE {pieceNumber}");
                    //                   _dc.MarkPieceRequested((UInt32)pieceNumber, false);
                    //                   _dc.MarkPieceLocal((UInt32)pieceNumber, false);
                    _suggestedPieces.Enqueue(pieceNumber);
                    // remotePeer.Close();
                }
            }
            else
            {
                if (!_dc.IsPieceLocal(pieceNumber))
                {
                    Log.Logger.Debug($"REQUEUING PIECE {pieceNumber}");
                    //                    _dc.MarkPieceRequested((UInt32)pieceNumber, false);
                    //                    _dc.MarkPieceLocal((UInt32)pieceNumber, false);
                    _suggestedPieces.Enqueue(pieceNumber);
                }
            }

            remotePeer.AssembledPiece.Reset();

        }
        /// <summary>
        /// Wait for event to be set throwing a cancel exception if it is fired.
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="cancelTask"></param>
        private void WaitOnWithCancelation(ManualResetEvent evt, CancellationToken cancelTask)
        {
            while (!evt.WaitOne(100))
            {
                cancelTask.ThrowIfCancellationRequested();
            }
        }
        /// <summary>
        /// Request piece number from remote peer.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="pieceNumber"></param>
        private bool GetPieceFromPeer(Peer remotePeer, uint pieceNumber, CancellationToken cancelTask)
        {
            remotePeer.AssembledPiece.SetBlocksPresent(remotePeer.Dc.PieceMap[pieceNumber].pieceLength);

            UInt32 blockNumber = 0;
            for (; blockNumber < remotePeer.Dc.PieceMap[pieceNumber].pieceLength / Constants.BlockSize; blockNumber++)
            {
                if (!remotePeer.PeerChoking.WaitOne(0))
                {
                    remotePeer.WaitForPieceAssembly.Reset();
                    return false;
                }
                PWP.Request(remotePeer, pieceNumber, blockNumber * Constants.BlockSize, Constants.BlockSize);
            }

            if (remotePeer.Dc.PieceMap[pieceNumber].pieceLength % Constants.BlockSize != 0)
            {
                if (!remotePeer.PeerChoking.WaitOne(0))
                {
                    remotePeer.WaitForPieceAssembly.Reset();
                    return false;
                }
                PWP.Request(remotePeer, pieceNumber, blockNumber * Constants.BlockSize,
                             remotePeer.Dc.PieceMap[pieceNumber].pieceLength % Constants.BlockSize);
            }
            while (!remotePeer.WaitForPieceAssembly.WaitOne(100))
            {
                cancelTask.ThrowIfCancellationRequested();
                if (!remotePeer.PeerChoking.WaitOne(0))
                {
                    remotePeer.WaitForPieceAssembly.Reset();
                    return false;
                }
            }
            remotePeer.WaitForPieceAssembly.Reset();
            return (remotePeer.AssembledPiece.AllBlocksThere);
        }

        /// <summary>
        /// Setup data and resources needed by an assembler.
        /// </summary>
        /// <param name="torrentDownloader"></param>
        /// <param name="progressFunction"></param>
        /// <param name="progressData"></param>
        public Assembler(Downloader torrentDownloader, ProgessCallBack progressFunction = null, Object progressData = null)
        {
            _dc = torrentDownloader.Dc;
            _progressFunction = progressFunction;
            _progressData = progressData;
            Paused = new ManualResetEvent(false);
            BuildSuggestedPiecesQueue();
        }

        /// <summary>
        /// Assembles the pieces of a torrent block by block.A task is created using this method for each connected peer.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="_downloadFinished"></param>
        public void AssemblePieces(Peer remotePeer, ManualResetEvent _downloadFinished)
        {
            UInt32 nextPiece = 0;
            CancellationToken cancelTask = remotePeer.CancelTaskSource.Token;

            try
            {
                ActiveAssemblerTasks++;

                Log.Logger.Debug($"Running block assembler for peer {Encoding.ASCII.GetString(remotePeer.RemotePeerID)}.");

                WaitOnWithCancelation(remotePeer.BitfieldReceived, cancelTask);

                PWP.Unchoke(remotePeer);

                PWP.Interested(remotePeer);

                WaitOnWithCancelation(remotePeer.PeerChoking, cancelTask);

                while (_dc.BytesLeftToDownload() != 0)
                {

                    while (SelectNextPiece(remotePeer, ref nextPiece))
                    {

                        Log.Logger.Debug($"Assembling blocks for piece {nextPiece}.");

                        SavePieceToDisk(remotePeer, nextPiece, GetPieceFromPeer(remotePeer, nextPiece, cancelTask));

                        WaitOnWithCancelation(remotePeer.PeerChoking, cancelTask);
                        WaitOnWithCancelation(Paused, cancelTask);

                    }

                }

                _downloadFinished.Set();

            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
                SavePieceToDisk(remotePeer, nextPiece, remotePeer.AssembledPiece.AllBlocksThere);
            }

            Log.Logger.Debug($"Exiting block assembler for peer {Encoding.ASCII.GetString(remotePeer.RemotePeerID)}.");

            ActiveAssemblerTasks--;

        }
    }
}
