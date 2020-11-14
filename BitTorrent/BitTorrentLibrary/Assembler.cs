//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Provide functionality for downloading pieces of a torrent
// from a remote server using the piece/peer selector algorithm passed to it.
// Each piece is selected and download requests are made for its individual
// blocks by a list of selected peers before the next piece is moved to.
//
// Copyright 2020.
//

using System;
using System.Diagnostics;
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
        private readonly int assemberTimeout;        // Assembly timeout in seconds

        /// <summary>
        /// Signal to all peers in swarm that we now have the piece local so
        /// that they can request it if they need.
        /// </summary>
        /// <param name="tc"></param>
        /// <param name="pieceNumber"></param>
        private void SignalHaveToSwarm(TorrentContext tc, UInt32 pieceNumber)
        {
            foreach (var remotePeer in tc.peerSwarm.Values)
            {
                PWP.Have(remotePeer, pieceNumber);
            }
        }
        /// <summary>
        /// Request block piece from a peer.
        /// </summary>
        /// <param name="peers"></param>
        /// <param name="pieceNumber"></param>
        /// <param name="blockOffset"></param>
        /// <param name="blockSize"></param>
        private void Request(Peer remotePeer, UInt32 pieceNumber, UInt32 blockOffset, UInt32 blockSize)
        {
            try
            {
                remotePeer.CancelTaskSource.Token.ThrowIfCancellationRequested();
                PWP.Request(remotePeer, pieceNumber, blockOffset, blockSize);
            }
            catch (Exception ex)
            {
                Log.Logger.Debug("BitTorrent (Assembler) Error:" + ex.Message);
            }
        }
        /// <summary>
        /// Request a piece block by block and wait for it to be assembled.
        /// </summary>
        /// <param name="tc"></param>
        /// <param name="pieceNumber"></param>
        /// <param name="waitHandles"></param>
        /// <returns></returns>
        private bool GetPieceFromPeers(TorrentContext tc, UInt32 pieceNumber, WaitHandle[] waitHandles)
        {

            Peer[] remotePeers = tc.selector.GetListOfPeers(tc, pieceNumber);

            if (remotePeers.Length == 0)
            {
                Log.Logger.Debug($"(Assembler) Zero peers to assemble piece {pieceNumber}.");
                return false;
            }

            var stopwatch = new Stopwatch();

            Log.Logger.Debug($"(Assembler) Piece {pieceNumber} being assembled by {remotePeers.Length} peers.");

            tc.assembledPiece.Number = pieceNumber;
            tc.assembledPiece.Reset();
            tc.assembledPiece.SetBlocksPresent(tc.GetPieceLength(pieceNumber));
            tc.waitForPieceAssembly.Reset();

            while (true)
            {
                UInt32 blockOffset = 0;
                UInt32 bytesToTransfer = tc.GetPieceLength(pieceNumber);
                UInt32 currentPeer = 0;

                stopwatch.Start();
                foreach (var blockThere in tc.assembledPiece.BlocksPresent())
                {
                    if (!blockThere)
                    {
                        Request(remotePeers[currentPeer], pieceNumber, blockOffset, Math.Min(Constants.BlockSize, bytesToTransfer));
                    }
                    currentPeer++;
                    currentPeer %= (UInt32)remotePeers.Length;
                    blockOffset += Constants.BlockSize;
                    bytesToTransfer -= Constants.BlockSize;
                }

                // Wait for piece to be assembled

                switch (WaitHandle.WaitAny(waitHandles, assemberTimeout * 1000))
                {
                    //  Something has been assembled
                    case 0:
                        stopwatch.Stop();
                        Log.Logger.Debug($"(Assembler) Time to assemble piece {pieceNumber} was {stopwatch.ElapsedMilliseconds} milliseconds");
                        return tc.assembledPiece.AllBlocksThere;
                    // Assembly has been cancelled by external source
                    case 1:
                        return false;
                    // Timeout so re-request blocks not returned
                    // Note: can result in blocks having to be discarded
                    case WaitHandle.WaitTimeout:
                        Log.Logger.Debug($"(Assembler) Timeout assembling piece {pieceNumber}.");
                        tc.assemblyTimeOuts++;
                        continue;
                }

            }

        }
        /// <summary>
        /// Loop for all pieces assembling them block by block until the download is
        /// complete or has been interrupted. If an an assembled piece is found to be
        /// corrupt it is discarded and requested again.
        /// </summary>
        /// <param name="tc"></param>
        /// <param name="cancelAssemblerTask"></param>
        private void AssembleMissingPieces(TorrentContext tc, CancellationToken cancelAssemblerTask)
        {
            UInt32 nextPiece = 0;

            WaitHandle[] waitHandles = new WaitHandle[] { tc.waitForPieceAssembly, cancelAssemblerTask.WaitHandle };

            while (!tc.downloadFinished.WaitOne(0))
            {
                while (tc.selector.NextPiece(tc, ref nextPiece, nextPiece, cancelAssemblerTask))
                {
                    if (GetPieceFromPeers(tc, nextPiece, waitHandles))
                    {
                        bool pieceValid = tc.CheckPieceHash(nextPiece, tc.assembledPiece.Buffer, tc.GetPieceLength(nextPiece));
                        if (pieceValid)
                        {
                            Log.Logger.Debug($"(Assembler) All blocks for piece {nextPiece} received");
                            tc.pieceWriteQueue.Enqueue(new PieceBuffer(tc.assembledPiece));
                            tc.MarkPieceLocal(nextPiece, true);
                            SignalHaveToSwarm(tc, nextPiece);
                        }
                        else
                        {
                            Log.Logger.Debug($"(Assembler) InfoHash for piece {nextPiece} corrupt.");
                        }
                    }
                    else
                    {   // if we reach here then no eligable peers in swarm so sleep a bit.
                        Thread.Sleep(100);
                    }
                    // Signal piece to be requested in unsucessful download
                    if (!tc.IsPieceLocal(nextPiece))
                    {
                        tc.MarkPieceMissing(nextPiece, true);
                    }
                    cancelAssemblerTask.ThrowIfCancellationRequested();
                    tc.paused.WaitOne(cancelAssemblerTask);
                }
            }
        }
        /// <summary>
        // Wait and process remote peer requests until cancelled.
        /// </summary>
        /// <param name="tc"></param>
        /// <param name="cancelTask"></param>
        private void ProcessRemotePeerRequests(TorrentContext tc, CancellationToken cancelAssemblerTask)
        {

            WaitHandle[] waitHandles = new WaitHandle[] { cancelAssemblerTask.WaitHandle };
            foreach (var remotePeer in tc.peerSwarm.Values)
            {
                PWP.Uninterested(remotePeer);
                PWP.Unchoke(remotePeer);
            }
            WaitHandle.WaitAll(waitHandles);

        }
        /// <summary>
        /// Setup data and resources needed by assembler.
        /// </summary>
        public Assembler(int assemblerTimeout = 60)
        {
            assemberTimeout = assemblerTimeout;
        }
        /// <summary>
        /// Piece assembler task. If/once downboad is complete then start seeding the torrent until
        /// a cancel request is sent. Note: For seeding we only send announce requests every 30 minutes
        /// to stop the local and remote peers from being swamped by alot of requests.
        /// </summary>
        /// <param name="tc"></param>
        /// <param name="cancelAssemblerTask"></param>
        internal void AssemblePieces(TorrentContext tc, CancellationToken cancelAssemblerTask)
        {

            Log.Logger.Debug($"(Assembler) Starting block assembler for InfoHash {Util.InfoHashToString(tc.infoHash)}.");
            try
            {

                tc.paused.WaitOne(cancelAssemblerTask);

                if (tc.MainTracker.Left != 0)
                {
                    Log.Logger.Info("Torrent downloading...");
                    tc.Status = TorrentStatus.Downloading;
                    AssembleMissingPieces(tc, cancelAssemblerTask);
                    tc.MainTracker.ChangeStatus(TrackerEvent.completed);
                    Log.Logger.Info("Whole Torrent finished downloading.");
                }

                Log.Logger.Info("Torrent seeding...");
                tc.Status = TorrentStatus.Seeding;
                tc.MainTracker.SetSeedingInterval(60000 * 30);
                // Make sure get at least one more annouce before long wait
                tc.MainTracker.ChangeStatus(TrackerEvent.None);
                ProcessRemotePeerRequests(tc, cancelAssemblerTask);

            }
            catch (Exception ex)
            {
                Log.Logger.Error("BitTorrent (Assembler) Error: " + ex.Message);
            }
            Log.Logger.Debug($"(Assembler) Terminating block assembler for InfoHash {Util.InfoHashToString(tc.infoHash)}.");

        }
    }
}
