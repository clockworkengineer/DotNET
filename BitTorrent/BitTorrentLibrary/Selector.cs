//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Contains code and data to implement a specific piece and
// peer selection methods for piece download. For piece selection it just starts
// at beginning of the missing pieces bitfield and stops when it finds
// the first piece as flagged missing and returns its ordinal position within
// the bitfield. For peers its just al those that are currently active and have
// the required piece.
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
        /// <summary>
        /// Return next suggested piece to download.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="startPiece"></param>
        /// <returns></returns>
        private Int64 GetSuggestedPiece(TorrentContext tc, UInt32 startPiece)
        {
            UInt32 currentPiece = startPiece;
            do
            {
                if (tc.IsPieceMissing(currentPiece) && (tc.PeersThatHavePiece(currentPiece) > 0))
                {
                    return currentPiece;
                }
                currentPiece++;
                currentPiece %= (UInt32) tc.numberOfPieces;
            } while (startPiece != currentPiece);
            return -1;
        }
        /// <summary>
        /// Setup data and resources needed by selector.
        /// </summary>
        /// <param name="dc"></param>
        public Selector()
        {
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tc"></param>
        /// <param name="nextPiece"></param>
        /// <param name="startPiece"></param>
        /// <param name="_"></param>
        /// <returns></returns>
        internal bool NextPiece(TorrentContext tc, ref UInt32 nextPiece, UInt32 startPiece, CancellationToken _)
        {
            bool pieceSuggested = false;
            Int64 suggestedPiece = GetSuggestedPiece(tc, startPiece);
            if (suggestedPiece != -1)
            {
                nextPiece = (UInt32)suggestedPiece;
                tc.MarkPieceMissing(nextPiece, false);
                pieceSuggested = true;
            }
            return pieceSuggested;
        }
        /// <summary>
        /// Generate an array of pieces that are local but missing from the remote peer for input
        /// to Have packet requests sent to remote peer.
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="numberOfSuggestions"></param>
        /// <param name="startPiece"></param>
        /// <returns></returns>
        internal UInt32[] LocalPieceSuggestions(Peer remotePeer, UInt32 numberOfSuggestions, UInt32 startPiece = 0)
        {
            List<UInt32> suggestions = new List<UInt32>();
            UInt32 currentPiece = startPiece;
            do
            {
                if (!remotePeer.IsPieceOnRemotePeer(currentPiece) && remotePeer.Tc.IsPieceLocal(currentPiece))
                {
                    suggestions.Add(currentPiece);
                    numberOfSuggestions--;
                }
                currentPiece++;
                currentPiece %= (UInt32) remotePeer.Tc.numberOfPieces;
            } while ((startPiece != currentPiece) && (numberOfSuggestions > 0));
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
            return peers.OrderBy(peer => peer.averagePacketResponse.Get()).ToList().Take(maxPeers).ToArray();
        }
    }
}
