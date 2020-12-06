//
// Author: Rob Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Contains code and data to implement a specific piece and
// peer selection methods for piece download. For piece selection it just starts
// at a random piece and sequentially moves through the missing pieces bitfield and 
// stops when it finds the first piece as flagged missing and returns its ordinal 
// position within the bitfield. For peers selection it is just all those that 
// are currently active (not choked) and have the required piece.
//
// Copyright 2020.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
namespace BitTorrentLibrary
{
    public class Selector
    {
        private readonly Random _pieceRandmizer;
        /// <summary>
        /// Setup data and resources needed by selector.
        /// </summary>
        /// <param name="dc"></param>
        public Selector()
        {
            _pieceRandmizer = new Random();
        }
        /// <summary>
        /// Select the next piece to be downloaded. It does this by randomly generating a start
        /// piece and sequentially moving a long until ot finds a missing piece.
        /// </summary>
        /// <param name="tc"></param>
        /// <param name="nextPiece"></param>
        /// <param name="startPiece"></param>
        /// <param name="_"></param>
        /// <returns></returns>
        internal bool NextPiece(TorrentContext tc, ref UInt32 nextPiece)
        {
            if (tc is null)
            {
                throw new ArgumentNullException(nameof(tc));
            }

            UInt32 startPiece = (UInt32) _pieceRandmizer.Next(0, tc.numberOfPieces-1);
            (var pieceSuggested, var pieceNumber) = tc.FindNextMissingPiece(startPiece);
            nextPiece = pieceNumber;
            return pieceSuggested;
        }
        /// <summary>
        /// Generate an array of pieces that are local but missing from the remote peer for input
        /// to Have packet requests sent to remote peer. At present we only randomise the first 
        /// piece and the rest are chosen sequentially from that position (with wrap).
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="numberOfSuggestions"></param>
        /// <param name="startPiece"></param>
        /// <returns></returns>
        internal UInt32[] LocalPieceSuggestions(Peer remotePeer, int numberOfSuggestions)
        {
            HashSet<UInt32> suggestions = new HashSet<UInt32>();
            UInt32 startPiece = (UInt32)_pieceRandmizer.Next(0, remotePeer.Tc.numberOfPieces-1);
            numberOfSuggestions = Math.Min(remotePeer.NumberOfMissingPieces, numberOfSuggestions);
            if (numberOfSuggestions > 0)
            {
                UInt32 currentPiece = startPiece;
                do
                {
                    if (!remotePeer.IsPieceOnRemotePeer(currentPiece) && 
                         remotePeer.Tc.IsPieceLocal(currentPiece) && 
                         !suggestions.Contains(currentPiece))
                    {
                        suggestions.Add(currentPiece);
                    }
                    currentPiece++;
                    currentPiece %= (UInt32)remotePeer.Tc.numberOfPieces;
                } while ((startPiece != currentPiece)&&(suggestions.Count<numberOfSuggestions));
            }
            return (suggestions.ToArray());
        }
        /// <summary>
        /// Return list of peers connected that are not choked and have the piece. They
        /// are sorted in ascending order of average reponse time for a peer request packet
        /// reponse and limited by the value of maxPeers.
        /// </summary>
        /// <param name="tc"></param>
        /// <param name="pieceNumber"></param>
        /// <param name="maxPeers"></param>
        /// <returns></returns>
        internal Peer[] GetListOfPeers(TorrentContext tc, UInt32 pieceNumber, int maxPeers)
        {
            List<Peer> peers = new List<Peer>();
            foreach (var peer in tc.peerSwarm.Values)
            {
                if (peer.Connected &&
                    peer.PeerChoking.WaitOne(0) &&
                    peer.IsPieceOnRemotePeer(pieceNumber))
                {
                    peers.Add(peer);
                }
            }
            return peers.OrderBy(peer => peer.AveragePacketResponse.Get()).ToList().Take(maxPeers).ToArray();
        }
    }
}
