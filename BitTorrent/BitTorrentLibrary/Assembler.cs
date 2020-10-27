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
// TODO: Needs better use of waits and positoning of choke checks.
//
// Copyright 2020.
//

using System;
using System.Text;
using System.Threading;

namespace BitTorrentLibrary
{
    public delegate void ProgessCallBack(Object callBackData); // Download progress callback

    public interface IAssembler
    {
        ManualResetEvent Paused { get; }
        void AssemblePieces(Peer remotePeer);
        void SetDownloadProgressCallBack(ProgessCallBack callBack, object callBackData);
    }

    /// <summary>
    /// Piece Assembler
    /// </summary>
    public class Assembler : IAssembler
    {
        private ProgessCallBack _progressCallBack;   // Download progress function
        private Object _progressCallBackData;        // Download progress function data
        public ManualResetEvent Paused { get; }      // == true (set) pause downloading from peer

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
                    bool pieceValid = remotePeer.Dc.CheckPieceHash(pieceNumber, remotePeer.AssembledPiece.Buffer, remotePeer.Dc.GetPieceLength(pieceNumber));
                    if (pieceValid)
                    {
                        Log.Logger.Debug($"All blocks for piece {pieceNumber} received");
                        remotePeer.Dc.PieceWriteQueue.Add(new PieceBuffer(remotePeer.AssembledPiece));
                        _progressCallBack?.Invoke(_progressCallBackData);
                        remotePeer.Dc.MarkPieceLocal(pieceNumber, true);
                    }
                    else
                    {
                        Log.Logger.Debug("PIECE CONTAINED INVALID INFOHASH.");
                        Log.Logger.Debug($"REQUEUING PIECE {pieceNumber}");
                        remotePeer.Dc.MarkPieceMissing(pieceNumber, true);
                        remotePeer.Dc.MarkPieceLocal(pieceNumber, false);
                    }
                }
                else
                {
                    if (!remotePeer.Dc.IsPieceLocal(pieceNumber))
                    {
                        Log.Logger.Debug($"REQUEUING PIECE {pieceNumber}");
                        remotePeer.Dc.MarkPieceMissing(pieceNumber, true);
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

            remotePeer.AssembledPiece.SetBlocksPresent(remotePeer.Dc.GetPieceLength(pieceNumber));

            UInt32 blockNumber = 0;
            for (; blockNumber < remotePeer.Dc.GetPieceLength(pieceNumber)/Constants.BlockSize; blockNumber++)
            {
                if (!remotePeer.PeerChoking.WaitOne(0))
                {
                    return false;
                }
                PWP.Request(remotePeer, pieceNumber, blockNumber * Constants.BlockSize, Constants.BlockSize);
            }

            if (remotePeer.Dc.GetPieceLength(pieceNumber) % Constants.BlockSize != 0)
            {
                if (!remotePeer.PeerChoking.WaitOne(0))
                {
                    return false;
                }
                PWP.Request(remotePeer, pieceNumber, blockNumber * Constants.BlockSize,
                             remotePeer.Dc.GetPieceLength(pieceNumber) % Constants.BlockSize);
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

            try
            {

                Log.Logger.Debug($"Running piece assembler for peer {Encoding.ASCII.GetString(remotePeer.RemotePeerID)}.");
                PWP.Unchoke(remotePeer);
                PWP.Interested(remotePeer);
                WaitOnWithCancelation(remotePeer.PeerChoking, cancelTask);

                while (!remotePeer.Dc.DownloadFinished.WaitOne(0))
                {
                    while (remotePeer.Dc.PieceSelector.NextPiece(remotePeer, ref nextPiece, cancelTask))
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
                throw;
            }

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
                    WaitHandle[] waitHandles = new WaitHandle[] { cancelTask.WaitHandle };
                    PWP.Uninterested(remotePeer);
                    PWP.Unchoke(remotePeer);
                    WaitHandle.WaitAll(waitHandles);

                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
                throw;
            }

        }
        /// <summary>
        /// Setup data and resources needed by assembler.
        /// </summary>
        /// <param name="torrentDownloader"></param>
        /// <param name="progressFunction"></param>
        /// <param name="progressData"></param>
        public Assembler()
        {
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

                if (remotePeer.Dc.BytesLeftToDownload() > 0)
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
        public void SetDownloadProgressCallBack(ProgessCallBack callBack, Object callBackData)
        {
            _progressCallBack = callBack;
            _progressCallBackData = callBackData;
        }
    }
}
