//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Provide functionality for downloading pieces of a torrent
// from a remote server using the piece selector algorithm passed to it. If
// the remote peer chokes while a piece is being processed then the the processing
// of the piece halts and it is requeued for download; except when the piece has
// sucessfully been assembled locally when the choke occurs then it is queued for
// writing to disk.
//
// Copyright 2020.
//

using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace BitTorrentLibrary
{
    public class Assembler
    {
        private readonly Selector _pieceSelector;                    // Piece to download selector
        private readonly ProgessCallBack _progressFunction = null;   // Download progress function
        private readonly Object _progressData = null;                // Download progress function data
        private readonly DownloadContext _dc;                        // Download context for torrent
        public ManualResetEvent Paused { get; set; }                 // == true (set) pause downloading from peer
        public int ActiveDownloaders { get; set; } = 0;              // Active Downloaders
        public int ActiveUploaders { get; set; } = 0;                // Active Uploaders

        /// <summary>
        /// Generate random piece number array for use in HAVE requests sent to remote peers.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="numberOfSuggestions"></param>
        /// <returns></returns>
        private UInt32[] PieceSuggestions(Peer remotePeer, UInt32 numberOfSuggestions)
        {
            List<UInt32> suggestions = new List<UInt32>();

            UInt32 startPiece = 0;
            UInt32 currentPiece = startPiece;

            do
            {
                if (!remotePeer.IsPieceOnRemotePeer(currentPiece))
                {
                    suggestions.Add(currentPiece);
                    numberOfSuggestions--;
                }
                currentPiece++;
                currentPiece %= remotePeer.Dc.NumberOfPieces;
            } while ((startPiece != currentPiece) && (numberOfSuggestions > 0));


            return (suggestions.ToArray());

        }
        /// <summary>
        /// Queue sucessfully assembled piece for writing to disk or requeue for download if not.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="pieceNumber"></param>
        /// <param name="pieceAssembled"></param>
        private void SavePieceToDisk(Peer remotePeer, UInt32 pieceNumber, bool pieceAssembled)
        {

            if (pieceAssembled)
            {
                bool pieceValid = _dc.CheckPieceHash(pieceNumber, remotePeer.AssembledPiece.Buffer, _dc.PieceMap[pieceNumber].pieceLength);
                if (pieceValid)
                {
                    if (!_dc.PieceBufferWriteQueue.IsCompleted)
                    {
                        Log.Logger.Debug($"All blocks for piece {pieceNumber} received");
                        _dc.PieceBufferWriteQueue.Add(new PieceBuffer(remotePeer.AssembledPiece));
                        _progressFunction?.Invoke(_progressData);
                        _dc.MarkPieceLocal(pieceNumber, true);
                    }
                }
                else
                {
                    Log.Logger.Debug("PIECE CONTAINED INVALID INFOHASH.");
                    Log.Logger.Debug($"REQUEUING PIECE {pieceNumber}");
                    _pieceSelector.PutPieceBack(pieceNumber);
                    _dc.MarkPieceLocal(pieceNumber, false);
                }
            }
            else
            {
                if (!_dc.IsPieceLocal(pieceNumber))
                {
                    Log.Logger.Debug($"REQUEUING PIECE {pieceNumber}");
                    _pieceSelector.PutPieceBack(pieceNumber);
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
            while (!evt.WaitOne(300))
            {
                cancelTask.ThrowIfCancellationRequested();
            }
        }
        /// <summary>
        /// Request piece from remote peer. If peer is choked or an cancel arises exit without completeing
        /// requests so that piece can be requeued for handling later.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="pieceNumber"></param>
        /// <param name="cancelTask"></param>
        /// <returns></returns>
        private bool GetPieceFromPeer(Peer remotePeer, uint pieceNumber, CancellationToken cancelTask)
        {
            WaitHandle[] waitHandles = new WaitHandle[] { remotePeer.WaitForPieceAssembly, cancelTask.WaitHandle };

            remotePeer.WaitForPieceAssembly.Reset();

            remotePeer.AssembledPiece.SetBlocksPresent(remotePeer.Dc.PieceMap[pieceNumber].pieceLength);

            UInt32 blockNumber = 0;
            for (; blockNumber < remotePeer.Dc.PieceMap[pieceNumber].pieceLength / Constants.BlockSize; blockNumber++)
            {
                if (!remotePeer.PeerChoking.WaitOne(0))
                {
                    return false;
                }
                cancelTask.ThrowIfCancellationRequested();
                PWP.Request(remotePeer, pieceNumber, blockNumber * Constants.BlockSize, Constants.BlockSize);
            }

            if (remotePeer.Dc.PieceMap[pieceNumber].pieceLength % Constants.BlockSize != 0)
            {
                if (!remotePeer.PeerChoking.WaitOne(0))
                {
                    return false;
                }
                cancelTask.ThrowIfCancellationRequested();
                PWP.Request(remotePeer, pieceNumber, blockNumber * Constants.BlockSize,
                             remotePeer.Dc.PieceMap[pieceNumber].pieceLength % Constants.BlockSize);
            }

            int index = WaitHandle.WaitAny(waitHandles);
            if (index == 0)
            {
                remotePeer.WaitForPieceAssembly.Reset();
            }
            else if (index == 1)
            {
                cancelTask.ThrowIfCancellationRequested();
            }
            return (remotePeer.AssembledPiece.AllBlocksThere);
        }
        /// <summary>
        /// Assembles the pieces of a torrent block by block. If a choke or cancel occurs when a piece is being handled the 
        /// piece is requeued for handling later by this or another task.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="cancelTask"></param>
        private void AssembleMissingPieces(Peer remotePeer, CancellationToken cancelTask)
        {
            UInt32 nextPiece = 0;
            ActiveDownloaders++;
            try
            {

                Log.Logger.Debug($"Running piece assembler for peer {Encoding.ASCII.GetString(remotePeer.RemotePeerID)}.");

                PWP.Bitfield(remotePeer, remotePeer.Dc.BuildPieceBitfield());
                
                PWP.Unchoke(remotePeer);

                PWP.Interested(remotePeer);

                WaitOnWithCancelation(remotePeer.PeerChoking, cancelTask);

                while (_pieceSelector.NextPiece(remotePeer, ref nextPiece, cancelTask))
                {
                    Log.Logger.Debug($"Assembling blocks for piece {nextPiece}.");
                    SavePieceToDisk(remotePeer, nextPiece, GetPieceFromPeer(remotePeer, nextPiece, cancelTask));
                    WaitOnWithCancelation(remotePeer.PeerChoking, cancelTask);
                    WaitOnWithCancelation(Paused, cancelTask);
                }

            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
                SavePieceToDisk(remotePeer, nextPiece, remotePeer.AssembledPiece.AllBlocksThere);
                ActiveDownloaders--;
                throw;
            }
            ActiveDownloaders--;
        }
        /// <summary>
        /// Loop dealing with piece requests until peer connection closed.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="cancelTask"></param>
        private void ProcessRemotePeerRequests(Peer remotePeer, CancellationToken cancelTask)
        {
            try
            {
                if (remotePeer.Connected)
                {
                    ActiveUploaders++;

                    PWP.Uninterested(remotePeer);

                    PWP.Unchoke(remotePeer);
                    while (true)
                    {
                        if (remotePeer.NumberOfMissingPieces > 0)
                        {
                            if (!remotePeer.PeerInterested)
                            {
                                foreach (var suggestion in PieceSuggestions(remotePeer, 10))
                                {
                                    PWP.Have(remotePeer, suggestion);
                                }

                            }
                        }
                        Thread.Sleep(100);
                        cancelTask.ThrowIfCancellationRequested();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
                ActiveUploaders--;
                throw;
            }
        }
        /// <summary>
        /// Setup data and resources needed by assembler.
        /// </summary>
        /// <param name="torrentDownloader"></param>
        /// <param name="progressFunction"></param>
        /// <param name="progressData"></param>
        public Assembler(Downloader torrentDownloader, Selector pieceSeclector, ProgessCallBack progressFunction = null, Object progressData = null)
        {
            _dc = torrentDownloader.Dc;
            _progressFunction = progressFunction;
            _progressData = progressData;
            _pieceSelector = pieceSeclector;
            Paused = new ManualResetEvent(false);

        }
        /// <summary>
        /// Task method to download any missing pieces of torrent and when that is done to simply
        /// loop processing remote peer commands until the connection is closed.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="_downloadFinished"></param>
        public void AssemblePieces(Peer remotePeer)
        {

            try
            {

                CancellationToken cancelTask = remotePeer.CancelTaskSource.Token;

                WaitOnWithCancelation(remotePeer.BitfieldReceived, cancelTask);

                if (_dc.BytesLeftToDownload() > 0)
                {
                    AssembleMissingPieces(remotePeer, cancelTask);
                }

                ProcessRemotePeerRequests(remotePeer, cancelTask);

            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
            }

            Log.Logger.Debug($"Exiting block assembler for peer {Encoding.ASCII.GetString(remotePeer.RemotePeerID)}.");

        }
    }
}
