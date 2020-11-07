//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Provide functionality for downloading pieces of a torrent
// from a remote server using the piece/peer selector algorithm passed to it. 
//
// Copyright 2020.
//

using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections.Generic;

namespace BitTorrentLibrary
{

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
        /// <param name="tc"></param>
        /// <param name="pieceNumber"></param>
        private void SignalHaveToSwarm(TorrentContext tc, UInt32 pieceNumber)
        {
            foreach (var remotePeer in tc.PeerSwarm.Values)
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
        /// Request a piece and wait for it to be assembled.
        /// </summary>
        /// <param name="tc"></param>
        /// <param name="pieceNumber"></param>
        /// <param name="waitHandles"></param>
        /// <returns></returns>
        private bool GetPieceFromPeers(TorrentContext tc, uint pieceNumber, WaitHandle[] waitHandles)
        {

            Peer[] remotePeers = tc.Selector.GetListOfPeers(tc, pieceNumber);

            if (remotePeers.Length == 0)
            {
                return false;
            }

            tc.AssembledPiece.Number = pieceNumber;
            tc.AssembledPiece.Reset();
            tc.AssembledPiece.SetBlocksPresent(tc.GetPieceLength(pieceNumber));
            tc.WaitForPieceAssembly.Reset();

            bool[] blockThere = tc.AssembledPiece.BlocksPresent();

            while (true)
            {
                UInt32 blockOffset = 0;
                UInt32 bytesToTransfer = tc.GetPieceLength(pieceNumber);
                UInt32 currentPeer = 0;

                for (var blockNumber = 0; blockNumber < tc.GetBlocksInPiece(pieceNumber); blockNumber++)
                {
                    if (!blockThere[blockNumber])
                    {
                        if (bytesToTransfer >= Constants.BlockSize)
                        {
                            Request(remotePeers[currentPeer], pieceNumber, blockOffset, Constants.BlockSize);
                        }
                        else
                        {
                            Request(remotePeers[currentPeer], pieceNumber, blockOffset, bytesToTransfer);
                        }

                    }
                    currentPeer++;
                    currentPeer %= (UInt32)remotePeers.Length;
                    blockOffset += Constants.BlockSize;
                    bytesToTransfer -= Constants.BlockSize;
                }

                switch (WaitHandle.WaitAny(waitHandles, 60000))
                {
                    case 0:
                        return tc.AssembledPiece.AllBlocksThere;
                    case 1:
                        return false;
                    case WaitHandle.WaitTimeout:
                        continue;
                }

            }

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tc"></param>
        /// <param name="cancelTask"></param>
        private void AssembleMissingPieces(TorrentContext tc, CancellationToken cancelTask)
        {
            UInt32 nextPiece = 0;

            WaitHandle[] waitHandles = new WaitHandle[] { tc.WaitForPieceAssembly, cancelTask.WaitHandle };

            while (!tc.DownloadFinished.WaitOne(0))
            {
                while (tc.Selector.NextPiece(tc, ref nextPiece, nextPiece, cancelTask))
                {
                    if (GetPieceFromPeers(tc, nextPiece, waitHandles))
                    {
                        bool pieceValid = tc.CheckPieceHash(nextPiece, tc.AssembledPiece.Buffer, tc.GetPieceLength(nextPiece));
                        if (pieceValid)
                        {
                            Log.Logger.Debug($"All blocks for piece {nextPiece} received");
                            tc.PieceWriteQueue.Enqueue(new PieceBuffer(tc.AssembledPiece));
                            tc.MarkPieceLocal(nextPiece, true);
                            SignalHaveToSwarm(tc, nextPiece);
                        }
                        else
                        {
                            Log.Logger.Debug($"InfoHash for piece {nextPiece} corrupt.");
                        }
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                    if (!tc.IsPieceLocal(nextPiece))
                    {
                        tc.MarkPieceMissing(nextPiece, true);
                    }
                    cancelTask.ThrowIfCancellationRequested();
                }

            }
        }
        /// <summary>
        // Wait and process remote peer requests until cancelleed.
        /// </summary>
        /// <param name="tc"></param>
        /// <param name="cancelTask"></param>
        private void ProcessRemotePeerRequests(TorrentContext tc, CancellationToken cancelTask)
        {

            WaitHandle[] waitHandles = new WaitHandle[] { cancelTask.WaitHandle };
            foreach (var remotePeer in tc.PeerSwarm.Values)
            {
                PWP.Uninterested(remotePeer);
                PWP.Unchoke(remotePeer);
            }
            WaitHandle.WaitAll(waitHandles);

        }
        /// <summary>
        /// Setup data and resources needed by assembler.
        /// </summary>
        public Assembler()
        {
            Paused = new ManualResetEvent(false);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tc"></param>
        /// <param name="cancelAssemblerTask"></param>
        internal void AssemblePieces(TorrentContext tc, CancellationToken cancelAssemblerTask)
        {

            Log.Logger.Debug($"Starting block assembler for InfoHash {Util.InfoHashToString(tc.InfoHash)}.");

            try
            {

                Paused.WaitOne();

                if (tc.BytesLeftToDownload() > 0)
                {
                    AssembleMissingPieces(tc, cancelAssemblerTask);
                }

                ProcessRemotePeerRequests(tc, cancelAssemblerTask);

            }
            catch (Exception ex)
            {
                Log.Logger.Error("BitTorrent (Assembler) Error: " + ex.Message);
            }

            Log.Logger.Debug($"Terminating block assembler for InfoHash {Util.InfoHashToString(tc.InfoHash)}.");

        }
    }
}
