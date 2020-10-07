//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: 
//
// Copyright 2019.
//
using System;
using System.Text;
using System.Threading;

namespace BitTorrentLibrary
{
    public class Assembler
    {
        private readonly Object _pieceLock = new object();           // Piece Lock     
        private readonly ProgessCallBack _progressFunction = null;   // Download progress function
        private readonly Object _progressData = null;                // Download progress function data
        private readonly DownloadContext _dc;                        // Download context for torrent
        public int ActiveAssemblerTasks { get; set; } = 0;           // Active Assembler tasks

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
                // is required when trying to get the next unrequested non-local piece
                lock (_pieceLock)
                {

                    for (UInt32 pieceNumber = 0; pieceNumber < _dc.NumberOfPieces; pieceNumber++)
                    {
                        if (remotePeer.IsPieceOnRemotePeer(pieceNumber))
                        {
                            if (!_dc.IsPieceRequested(pieceNumber) && !_dc.IsPieceLocal(pieceNumber))
                            {
                                nextPiece = pieceNumber;
                                _dc.MarkPieceRequested(pieceNumber, true);
                                return true;
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
                    return false;
                }
                PWP.Request(remotePeer, pieceNumber, blockNumber * Constants.BlockSize, Constants.BlockSize);
            }

            if (remotePeer.Dc.PieceMap[pieceNumber].pieceLength % Constants.BlockSize != 0)
            {
                if (!remotePeer.PeerChoking.WaitOne(0))
                {
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


        public Assembler(Downloader torrentDownloader, ProgessCallBack progressFunction = null, Object progressData = null)
        {
            _dc = torrentDownloader.Dc;
            _progressFunction = progressFunction;
            _progressData = progressData;
        }

        /// <summary>
        /// Assembles the pieces of a torrent block by block.A task is created using this method for each connected peer.
        /// </summary>
        /// <returns>Task reference on completion.</returns>
        /// <param name="remotePeer">Remote peer.</param>
        /// <param name="progressFunction">Progress function.</param>
        /// <param name="progressData">Progress data.</param>
        public void AssemblePieces(Peer remotePeer, ManualResetEvent _downloadFinished)
        {
            Int64 currentPiece = -1;

            try
            {
                ActiveAssemblerTasks++;

                Log.Logger.Debug($"Running block assembler for peer {Encoding.ASCII.GetString(remotePeer.RemotePeerID)}.");

                CancellationToken cancelTask = remotePeer.CancelTaskSource.Token;

                PWP.Unchoke(remotePeer);

                WaitOnWithCancelation(remotePeer.Paused, cancelTask);
                WaitOnWithCancelation(remotePeer.BitfieldReceived, cancelTask);
                WaitOnWithCancelation(remotePeer.PeerChoking, cancelTask);

                while (_dc.TotalBytesToDownload - _dc.TotalBytesDownloaded != 0)
                {
                    UInt32 nextPiece = 0;
                    while (SelectNextPiece(remotePeer, ref nextPiece))
                    {
                        Log.Logger.Debug($"Assembling blocks for piece {nextPiece}.");

                        currentPiece = (Int32)nextPiece;

                        if (GetPieceFromPeer(remotePeer, nextPiece, cancelTask))
                        {

                            Log.Logger.Debug($"All blocks for piece {nextPiece} received");

                            _dc.PieceBufferWriteQueue.Add(new PieceBuffer(remotePeer.AssembledPiece), cancelTask);

                            remotePeer.AssembledPiece.Reset();

                            _progressFunction?.Invoke(_progressData);

                            Log.Logger.Info((_dc.TotalBytesDownloaded / (double)_dc.TotalBytesToDownload).ToString("0.00%"));
                            currentPiece = -1;

                        }
                        else
                        {
                            Log.Logger.Info($"REMARK FOR DOWNLOAD PIECE {currentPiece}.");
                            _dc.MarkPieceRequested((UInt32)currentPiece, false);
                            _dc.MarkPieceLocal((UInt32)currentPiece, false);
                            remotePeer.AssembledPiece.Reset();
                        }

                        WaitOnWithCancelation(remotePeer.PeerChoking, cancelTask);
                        WaitOnWithCancelation(remotePeer.Paused, cancelTask);

                    }

                }

                _downloadFinished.Set();

            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
                if (currentPiece != -1)
                {
                    Log.Logger.Info($"REMARK FOR DOWNLOAD PIECE {currentPiece}.");
                    _dc.MarkPieceRequested((UInt32)currentPiece, false);
                    _dc.MarkPieceLocal((UInt32)currentPiece, false);
                    remotePeer.AssembledPiece.Reset();
                }
            }

            Log.Logger.Debug($"Exiting block assembler for peer {Encoding.ASCII.GetString(remotePeer.RemotePeerID)}.");

            ActiveAssemblerTasks--;

        }
    }
}
