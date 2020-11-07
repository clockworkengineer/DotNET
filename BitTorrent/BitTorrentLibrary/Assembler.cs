//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Provide functionality for downloading pieces of a torrent
// from a remote server using the piece selector algorithm passed to it. 
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
        /// <param name="remotePeer"></param>
        /// <param name="pieceNumber"></param>
        private void SignalHaveToSwarm(TorrentContext tc, UInt32 pieceNumber)
        {
            foreach (var peer in tc.PeerSwarm.Values)
            {
                PWP.Have(peer, pieceNumber);
            }
        }
        /// <summary>
        /// 
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
        /// 
        /// </summary>
        /// <param name="tc"></param>
        /// <param name="pieceNumber"></param>
        /// <returns></returns>
        private Peer[] GetListOfOpenPeers(TorrentContext tc, UInt32 pieceNumber)
        {
            List<Peer> peers = new List<Peer>();
            foreach (var peer in tc.PeerSwarm.Values)
            {
                if (peer.Connected &&
                    peer.PeerChoking.WaitOne(0) &&
                    peer.IsPieceOnRemotePeer(pieceNumber))
                {
                    peers.Add(peer);
                }
            }
            return (peers.ToArray());
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tc"></param>
        /// <param name="pieceNumber"></param>
        /// <param name="waitHandles"></param>
        /// <returns></returns>
        private bool GetPieceFromPeers(TorrentContext tc, uint pieceNumber, WaitHandle[] waitHandles)
        {

            tc.AssembledPiece.Number = pieceNumber;
            tc.AssembledPiece.Reset();
            tc.WaitForPieceAssembly.Reset();
            tc.AssembledPiece.SetBlocksPresent(tc.GetPieceLength(pieceNumber));
            Peer[] peers = GetListOfOpenPeers(tc, pieceNumber);
            bool[] blockThere = tc.AssembledPiece.BlocksPresent();

            if (peers.Length == 0)
            {
                return false;
            }

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
                            Request(peers[currentPeer], pieceNumber, blockOffset, Constants.BlockSize);
                        }
                        else
                        {
                            Request(peers[currentPeer], pieceNumber, blockOffset, bytesToTransfer);
                        }

                    }
                    currentPeer++;
                    currentPeer %= (UInt32)peers.Length;
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
                while (tc.PieceSelector.NextPiece(tc, ref nextPiece, nextPiece, cancelTask))
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
        /// 
        /// </summary>
        /// <param name="tc"></param>
        /// <param name="cancelTask"></param>
        private void ProcessRemotePeerRequests(TorrentContext tc, CancellationToken cancelTask)
        {

            WaitHandle[] waitHandles = new WaitHandle[] { cancelTask.WaitHandle };
            foreach (var peer in tc.PeerSwarm.Values)
            {
                PWP.Uninterested(peer);
                PWP.Unchoke(peer);
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

            Log.Logger.Debug($"Entering block assembler for InfoHash {Util.InfoHashToString(tc.InfoHash)}.");

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

            Log.Logger.Debug($"Exiting block assembler for InfoHash {Util.InfoHashToString(tc.InfoHash)}.");

        }
    }
}
