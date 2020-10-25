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
using System.Threading;

namespace BitTorrentLibrary
{
    public delegate void ProgessCallBack(Object callBackData);            // Download progress callback
    public class Assembler
    {
        private readonly ProgessCallBack _progressCallBack;          // Download progress function
        private readonly Object _progressCallBackDta;                // Download progress function data
        private readonly DownloadContext _dc;                        // Download context for torrent
        public ManualResetEvent Paused { get; set; }                 // == true (set) pause downloading from peer
        public int ActiveDownloaders { get; set; } = 0;              // Active Downloaders
        public int ActiveUploaders { get; set; } = 0;                // Active Uploaders

        /// <summary>
        /// Queue sucessfully assembled piece for writing to disk or requeue for download if not.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="pieceNumber"></param>
        /// <param name="pieceAssembled"></param>
        private void SavePieceToDisk(Peer remotePeer, UInt32 pieceNumber, bool pieceAssembled)
        {

            if (!remotePeer.Dc.DownloadFinished.WaitOne(0))
            {
                if (pieceAssembled)
                {
                    bool pieceValid = _dc.CheckPieceHash(pieceNumber, remotePeer.AssembledPiece.Buffer, _dc.PieceData[pieceNumber].pieceLength);
                    if (pieceValid)
                    {
                        Log.Logger.Debug($"All blocks for piece {pieceNumber} received");
                        _dc.PieceWriteQueue.Add(new PieceBuffer(remotePeer.AssembledPiece));
                        _progressCallBack?.Invoke(_progressCallBackDta);
                        _dc.MarkPieceLocal(pieceNumber, true);
                    }
                    else
                    {
                        Log.Logger.Debug("PIECE CONTAINED INVALID INFOHASH.");
                        Log.Logger.Debug($"REQUEUING PIECE {pieceNumber}");
                        _dc.MarkPieceMissing(pieceNumber, true);
                        _dc.MarkPieceLocal(pieceNumber, false);
                    }
                }
                else
                {
                    if (!_dc.IsPieceLocal(pieceNumber))
                    {
                        Log.Logger.Debug($"REQUEUING PIECE {pieceNumber}");
                        _dc.MarkPieceMissing(pieceNumber, true);
                    }
                }

                remotePeer.AssembledPiece.Reset();
            }

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

            remotePeer.AssembledPiece.SetBlocksPresent(remotePeer.Dc.PieceData[pieceNumber].pieceLength);

            UInt32 blockNumber = 0;
            for (; blockNumber < remotePeer.Dc.PieceData[pieceNumber].pieceLength / Constants.BlockSize; blockNumber++)
            {
                if (!remotePeer.PeerChoking.WaitOne(0))
                {
                    return false;
                }
                cancelTask.ThrowIfCancellationRequested();
                PWP.Request(remotePeer, pieceNumber, blockNumber * Constants.BlockSize, Constants.BlockSize);
            }

            if (remotePeer.Dc.PieceData[pieceNumber].pieceLength % Constants.BlockSize != 0)
            {
                if (!remotePeer.PeerChoking.WaitOne(0))
                {
                    return false;
                }
                cancelTask.ThrowIfCancellationRequested();
                PWP.Request(remotePeer, pieceNumber, blockNumber * Constants.BlockSize,
                             remotePeer.Dc.PieceData[pieceNumber].pieceLength % Constants.BlockSize);
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

                PWP.Unchoke(remotePeer);

                PWP.Interested(remotePeer);

                WaitOnWithCancelation(remotePeer.PeerChoking, cancelTask);

                while (!remotePeer.Dc.DownloadFinished.WaitOne(0))
                {
                    while (_dc.PieceSelector.NextPiece(remotePeer, ref nextPiece, cancelTask))
                    {
                        Log.Logger.Debug($"Assembling blocks for piece {nextPiece}.");
                        SavePieceToDisk(remotePeer, nextPiece, GetPieceFromPeer(remotePeer, nextPiece, cancelTask));
                        WaitOnWithCancelation(remotePeer.PeerChoking, cancelTask);
                        WaitOnWithCancelation(Paused, cancelTask);
                    }
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
            ActiveUploaders++;

            try
            {
                if (remotePeer.Connected)
                {
                    WaitHandle[] waitHandles = new WaitHandle[] { cancelTask.WaitHandle };
                    
                
                    PWP.Uninterested(remotePeer);

                    PWP.Unchoke(remotePeer);

                    WaitHandle.WaitAll(waitHandles);

                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
                ActiveUploaders--;
                throw;
            }

            ActiveUploaders--;

        }
        /// <summary>
        /// Setup data and resources needed by assembler.
        /// </summary>
        /// <param name="torrentDownloader"></param>
        /// <param name="progressFunction"></param>
        /// <param name="progressData"></param>
        public Assembler(DownloadContext dc, ProgessCallBack progressFunction = null, Object progressData = null)
        {   
            _dc = dc;
            _progressCallBack = progressFunction;
            _progressCallBackDta = progressData;
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

                foreach (var pieceNumber in remotePeer.Dc.PieceSelector.LocalPieceSuggestions(remotePeer, 10))
                {
                    PWP.Have(remotePeer, pieceNumber);
                }

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
