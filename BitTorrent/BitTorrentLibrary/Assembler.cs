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
    internal static class ManualResetEventExtensions
    {
        /// <summary>
        /// Wait for event to be set throwing a cancel exception if it is fired.
        /// </summary>
        /// <param name="evt"></param>
        /// <param name="cancel"></param>
        public static void WaitOne(this ManualResetEvent evt, CancellationToken cancel)
        {
            if (WaitHandle.WaitAny(new WaitHandle[] { evt, cancel.WaitHandle }) == 1)
            {
                cancel.ThrowIfCancellationRequested();
            }
        }
    }
    /// <summary>
    /// Piece Assembler
    /// </summary>
    public class Assembler
    {
        internal ManualResetEvent Paused { get; }      // == false (unset) pause downloading from peer

        /// <summary>
        /// Signal to all peers in swarm that we now have the piece local so
        /// that they can request it if they need.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="pieceNumber"></param>
        private void SignalHaveToSwarm(Peer remotePeer, UInt32 pieceNumber)
        {
            foreach (var peer in remotePeer.Tc.PeerSwarm.Values)
            {
                PWP.Have(peer, pieceNumber);
            }
        }
        /// <summary>
        /// Queue sucessfully assembled piece for writing to disk or requeue for download if not.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="pieceNumber"></param>
        /// <param name="pieceAssembled"></param>
        private void QeueAssembledPieceToDisk(Peer remotePeer, UInt32 pieceNumber, bool pieceAssembled)
        {

            if (pieceAssembled)
            {
                bool pieceValid = remotePeer.Tc.CheckPieceHash(pieceNumber, remotePeer.AssembledPiece.Buffer, remotePeer.Tc.GetPieceLength(pieceNumber));
                if (pieceValid)
                {
                    Log.Logger.Debug($"All blocks for piece {pieceNumber} received");
                    remotePeer.Tc.PieceWriteQueue.Enqueue(new PieceBuffer(remotePeer.AssembledPiece));
                    remotePeer.Tc.MarkPieceLocal(pieceNumber, true);
                    SignalHaveToSwarm(remotePeer, pieceNumber);
                }
            }

            if (!remotePeer.Tc.IsPieceLocal(pieceNumber))
            {
                Log.Logger.Debug($"REQUEUING PIECE {pieceNumber}");
                remotePeer.Tc.MarkPieceMissing(pieceNumber, true);
            }

            remotePeer.AssembledPiece.Reset();

        }
        /// <summary>
        /// Request piece from remote peer. If peer is choked or an cancel arises exit without completeing
        /// requests so that piece can be requeued for handling later.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="pieceNumber"></param>
        /// <param name="waitHandles"></param>
        /// <returns></returns>
        private bool GetPieceFromPeer(Peer remotePeer, uint pieceNumber, WaitHandle[] waitHandles)
        {

            remotePeer.WaitForPieceAssembly.Reset();

            remotePeer.AssembledPiece.SetBlocksPresent(remotePeer.Tc.GetPieceLength(pieceNumber));

            UInt32 bytesToRequest = remotePeer.Tc.GetPieceLength(pieceNumber);
            UInt32 byteOffset = 0;

            for (; bytesToRequest >= Constants.BlockSize; byteOffset += Constants.BlockSize, bytesToRequest -= Constants.BlockSize)
            {
                if (!remotePeer.PeerChoking.WaitOne(0)) return false;
                PWP.Request(remotePeer, pieceNumber, byteOffset, Constants.BlockSize);
            }
            if (bytesToRequest > 0)
            {
                if (!remotePeer.PeerChoking.WaitOne(0)) return false;
                PWP.Request(remotePeer, pieceNumber, byteOffset, bytesToRequest);
            }

            if (WaitHandle.WaitAny(waitHandles, 60000) != 0) return false;

            remotePeer.WaitForPieceAssembly.Reset();

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

                WaitHandle[] waitHandles = new WaitHandle[] { remotePeer.WaitForPieceAssembly, cancelTask.WaitHandle };

                PWP.Unchoke(remotePeer);
                PWP.Interested(remotePeer);
                remotePeer.PeerChoking.WaitOne(cancelTask);

                while (!remotePeer.Tc.DownloadFinished.WaitOne(0))
                {
                    while (remotePeer.Tc.PieceSelector.NextPiece(remotePeer, ref nextPiece, cancelTask))
                    {
                        Log.Logger.Debug($"Assembling blocks for piece {nextPiece}.");
                        QeueAssembledPieceToDisk(remotePeer, nextPiece, GetPieceFromPeer(remotePeer, nextPiece, waitHandles));
                        remotePeer.PeerChoking.WaitOne(cancelTask);
                        Paused.WaitOne(cancelTask);
                        cancelTask.ThrowIfCancellationRequested();
                    }
                }

            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
                if (!remotePeer.Tc.DownloadFinished.WaitOne(0))
                {
                    QeueAssembledPieceToDisk(remotePeer, nextPiece, remotePeer.AssembledPiece.AllBlocksThere);
                }
                throw;
            }

        }
        /// <summary>
        /// Wait dealing with peer requets until connection closed.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="cancelTask"></param>
        private void ProcessRemotePeerRequests(Peer remotePeer, CancellationToken cancelTask)
        {

            try
            {
                if (remotePeer.Connected)
                {
                    if (remotePeer.NumberOfMissingPieces != 0)
                    {
                        WaitHandle[] waitHandles = new WaitHandle[] { cancelTask.WaitHandle };
                        PWP.Uninterested(remotePeer);
                        PWP.Unchoke(remotePeer);
                        WaitHandle.WaitAll(waitHandles);
                    }
                    else
                    {
                        // SHOULD ADD TO DEAD PEERS LIST HERE TO (NEED TO MOVE IT TO TC)
                        Log.Logger.Info($"Remote Peer doesn't need pieces. Closing the connection.");
                        remotePeer.Close();
                    }
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
        public Assembler()
        {
            Paused = new ManualResetEvent(false);
        }
        /// <summary>
        /// Task method to download any missing pieces of torrent and when that is done to simply
        /// loop processing remote peer commands until the connection is closed.
        /// TODO:Look into making two parts of this Async and waitall.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="_downloadFinished"></param>
        internal void AssemblePieces(Peer remotePeer)
        {

            try
            {

                CancellationToken cancelTask = remotePeer.CancelTaskSource.Token;

                remotePeer.BitfieldReceived.WaitOne(cancelTask);

                foreach (var pieceNumber in remotePeer.Tc.PieceSelector.LocalPieceSuggestions(remotePeer, 10))
                {
                    PWP.Have(remotePeer, pieceNumber);
                }

                if (remotePeer.Tc.BytesLeftToDownload() > 0)
                {
                    AssembleMissingPieces(remotePeer, cancelTask);
                }

                ProcessRemotePeerRequests(remotePeer,cancelTask);

            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
            }

            remotePeer.Close();

            Log.Logger.Debug($"Exiting block assembler for peer {Encoding.ASCII.GetString(remotePeer.RemotePeerID)}.");

        }
    }
}
