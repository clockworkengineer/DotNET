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
using System.Linq;
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
        private readonly int _assemberTimeout;        // Assembly timeout in seconds
        private readonly int _maximumBlockRequests;   // Maximum requests at a time
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
                try
                {
                    PWP.Have(remotePeer, pieceNumber);
                }
                catch (Exception ex)
                {
                    Log.Logger.Debug(ex);
                    remotePeer.Close();
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tc"></param>
        /// <param name="pieceNumber"></param>
        /// <param name="remotePeers"></param>
        /// <param name="stopwatch"></param>
        private bool GetMoreBlocks(TorrentContext tc, UInt32 pieceNumber, Peer[] remotePeers)
        {
            tc.assemblyData.guardMutex.WaitOne();
            try
            {
                int currentBlockRequests = 0;
                UInt32 blockOffset = 0;
                UInt32 bytesToTransfer = tc.GetPieceLength(pieceNumber);
                int currentPeer = 0;
                foreach (var blockThere in tc.assemblyData.pieceBuffer.BlocksPresent())
                {
                    if (!blockThere)
                    {
                        remotePeers[currentPeer].CancelTaskSource.Token.ThrowIfCancellationRequested();
                        try
                        {
                            PWP.Request(remotePeers[currentPeer], pieceNumber, blockOffset, Math.Min(Constants.BlockSize, bytesToTransfer));
                        }
                        catch (Exception ex)
                        {
                            // Peer has an error so remove it
                            Log.Logger.Debug(ex);
                            Log.Logger.Info("(Assembler) Peer exception so removing it.");
                            remotePeers[currentPeer].Close();
                            remotePeers = remotePeers.Except(new Peer[] { remotePeers[currentPeer] }).ToArray();
                            if (remotePeers.Length == 0)
                            {
                                tc.assemblyData.guardMutex.ReleaseMutex();
                                return false;
                            }
                            currentPeer %= (int)remotePeers.Length;
                            continue;
                        }
                        if (++currentBlockRequests == _maximumBlockRequests) break;
                        currentPeer++;
                        currentPeer %= (int)remotePeers.Length;
                    }
                    blockOffset += Constants.BlockSize;
                    bytesToTransfer -= Constants.BlockSize;
                }
                tc.assemblyData.currentBlockRequests = currentBlockRequests;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug("BitTorrent (Assembler) Error:" + ex.Message);
            }
            tc.assemblyData.guardMutex.ReleaseMutex();
            return true;
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
            Peer[] remotePeers = tc.selector.GetListOfPeers(tc, pieceNumber, _maximumBlockRequests);
            if (remotePeers.Length == 0)
            {
                Log.Logger.Debug($"(Assembler) Zero peers to assemble piece {pieceNumber}.");
                return false;
            }
            var stopwatch = new Stopwatch();
            Log.Logger.Debug($"(Assembler) Piece {pieceNumber} being assembled by {remotePeers.Length} peers.");
            tc.assemblyData.pieceBuffer.Number = pieceNumber;
            tc.assemblyData.pieceBuffer.Reset();
            tc.assemblyData.pieceBuffer.SetBlocksPresent(tc.GetPieceLength(pieceNumber));
            tc.assemblyData.pieceFinished.Reset();
            tc.assemblyData.blockRequestsDone.Reset();
             tc.MarkPieceMissing(pieceNumber, false);
            while (GetMoreBlocks(tc, pieceNumber, remotePeers))
            {
                //
                // Wait for blocks to arrive
                //
                stopwatch.Start();
                switch (WaitHandle.WaitAny(waitHandles, _assemberTimeout * 1000))
                {
                    // Any outstanding requests have been completed
                    case 0:
                        tc.assemblyData.blockRequestsDone.Reset();
                        continue;
                    //  Piece has been fully assembled
                    case 1:
                        stopwatch.Stop();
                        tc.assemblyData.averageAssemblyTime.Add(stopwatch.ElapsedMilliseconds);
                        Log.Logger.Debug($"(Assembler) Average time {tc.assemblyData.averageAssemblyTime.Get()} ms.");
                        return tc.assemblyData.pieceBuffer.AllBlocksThere;
                    // Assembly has been cancelled by external source
                    case 2:
                        return false;
                    // Timeout so re-request blocks not returned
                    // Note: can result in blocks having to be discarded
                    case WaitHandle.WaitTimeout:
                        Log.Logger.Debug($"(Assembler) Timeout assembling piece {pieceNumber}.");
                        tc.assemblyData.totalTimeouts++;
                        Log.Logger.Debug($"(Assembler) Reconfiguring peer list because of timeout.");
                        remotePeers = tc.selector.GetListOfPeers(tc, pieceNumber, _maximumBlockRequests);
                        if (remotePeers.Length == 0)
                        {
                            Log.Logger.Debug($"(Assembler) Zero peers to assemble piece {pieceNumber}.");
                            return false;
                        }
                        continue;
                }
            }
            return false;
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
            WaitHandle[] waitHandles = new WaitHandle[]
            {
                tc.assemblyData.blockRequestsDone,
                tc.assemblyData.pieceFinished,
                cancelAssemblerTask.WaitHandle
            };
            while (!tc.downloadFinished.WaitOne(0))
            {
                while (tc.selector.NextPiece(tc, ref nextPiece, nextPiece, cancelAssemblerTask))
                {
                    if (GetPieceFromPeers(tc, nextPiece, waitHandles))
                    {
                        bool pieceValid = tc.CheckPieceHash(nextPiece, tc.assemblyData.pieceBuffer.Buffer, tc.GetPieceLength(nextPiece));
                        if (pieceValid)
                        {
                            Log.Logger.Debug($"(Assembler) All blocks for piece {nextPiece} received");
                            tc.pieceWriteQueue.Add(new PieceBuffer(tc.assemblyData.pieceBuffer));
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
                        Thread.Sleep(1000);
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

                try
                {
                    PWP.Uninterested(remotePeer);
                    PWP.Unchoke(remotePeer);
                }
                catch (Exception ex)
                {
                    Log.Logger.Debug(ex);
                    remotePeer.Close();
                }

            }
            WaitHandle.WaitAll(waitHandles);
        }
        /// <summary>
        /// Setup data and resources needed by assembler.
        /// </summary>
        public Assembler(int assemblerTimeout = 10, int maximumBlockRequests = 10)
        {
            _assemberTimeout = assemblerTimeout;
            _maximumBlockRequests = maximumBlockRequests;
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
                Log.Logger.Debug(ex);
                Log.Logger.Error("BitTorrent (Assembler) Error: " + ex.Message);
            }
            Log.Logger.Debug($"(Assembler) Terminating block assembler for InfoHash {Util.InfoHashToString(tc.infoHash)}.");
        }
    }
}
