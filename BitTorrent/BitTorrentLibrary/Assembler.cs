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
    public class Assembler
    {

        private readonly Selector _pieceSelector;                    // Piece to download selector
        private readonly ProgessCallBack _progressFunction = null;   // Download progress function
        private readonly Object _progressData = null;                // Download progress function data
        private readonly DownloadContext _dc;                        // Download context for torrent
        public ManualResetEvent Paused { get; set; }                 // == true (set) pause downloading from peer
        public int ActiveAssemblerTasks { get; set; } = 0;           // Active Assembler tasks

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
                    Log.Logger.Debug($"All blocks for piece {pieceNumber} received");
                    _dc.PieceBufferWriteQueue.Add(new PieceBuffer(remotePeer.AssembledPiece));
                    _progressFunction?.Invoke(_progressData);
                }
                else
                {
                    Log.Logger.Debug("PIECE CONTAINED INVALID INFOHASH.");
                    Log.Logger.Debug($"REQUEUING PIECE {pieceNumber}");
                    _pieceSelector.PutPieceBack(pieceNumber);
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
        /// Assembles the pieces of a torrent block by block.A task is created using this method for each connected peer.
        /// If a choke or cancel occurs when a piece is being handled the piece is requeued for handling later by another
        /// task or same thread. Handling this at the piece level and not block simplifies the code significantly for not
        /// much added disadvantage.
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

                Log.Logger.Debug($"Running piece assembler for peer {Encoding.ASCII.GetString(remotePeer.RemotePeerID)}.");

                WaitOnWithCancelation(remotePeer.BitfieldReceived, cancelTask);

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
            }

            if (_dc.BytesLeftToDownload()==0){
                remotePeer.CancelTaskSource.Cancel();
                _downloadFinished.Set();
            }

            Log.Logger.Debug($"Exiting block assembler for peer {Encoding.ASCII.GetString(remotePeer.RemotePeerID)}.");

            ActiveAssemblerTasks--;

        }
    }
}
