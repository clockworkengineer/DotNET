using System.Threading;
//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Buffer used to hold and assemble pieces from blocks 
// downloaded.
//
// Copyright 2020.
//

using System;
using System.Linq;

namespace BitTorrentLibrary
{

    /// <summary>
    /// Assembled piece buffer.
    /// </summary>
    internal class PieceBuffer
    {
        private readonly Mutex _bufferMutex;            // Piece buffer guard mutex
        private bool[] _blockPresent;                   // == true then block present
        private uint _blockCount;                       // Unfilled block spaces in buffer
        public TorrentContext Tc { get; }               // Torrent context
        public uint Length { get; }                     // Piece Length
        public byte[] Buffer { get; }                   // Piece Buffer
        public uint Number { get; set; }                // Piece Number
        public bool AllBlocksThere => _blockCount == 0; // == true All blocks have been downloaded

        /// <summary>
        /// Create an empty piece buffer.
        /// </summary>
        /// <param name="length">Length.</param>
        public PieceBuffer(TorrentContext tc, UInt32 length)
        {
            Tc = tc;
            Number = 0;
            Length = length;
            Buffer = new byte[Length];
            _blockCount = length / Constants.BlockSize;
            _blockPresent = new bool[_blockCount];
            _bufferMutex = new Mutex();

        }
        /// <summary>
        /// Create an empty piece buffer.
        /// </summary>
        /// <param name="length">Length.</param>
        public PieceBuffer(TorrentContext tc, UInt32 pieceNumber, UInt32 length) : this(tc, length)
        {
            Number = pieceNumber;
        }
        /// <summary>
        /// Create a piece buffer from a copy of another.
        /// </summary>
        /// <param name="pieceBuffer">Piece buffer.</param>
        public PieceBuffer(PieceBuffer pieceBuffer)
        {
            Tc = pieceBuffer.Tc;
            Number = pieceBuffer.Number;
            Length = pieceBuffer.Length;
            Buffer = new byte[Length];
            pieceBuffer.Buffer.CopyTo(Buffer, 0);
            _blockCount = pieceBuffer._blockCount;
            _blockPresent = new bool[pieceBuffer._blockPresent.Length];
            pieceBuffer._blockPresent.CopyTo(_blockPresent, 0);
            _bufferMutex = new Mutex();
        }
        /// <summary>
        /// Copy block from packet to piece buffer.
        /// </summary>
        /// <param name="packetBuffer"></param>
        /// <param name="blockNumber"></param>
        public void AddBlockFromPacket(byte[] packetBuffer, UInt32 blockNumber)
        {
            _bufferMutex.WaitOne();
            System.Buffer.BlockCopy(packetBuffer, 9, Buffer, (Int32)blockNumber * Constants.BlockSize, (Int32)packetBuffer.Length - 9);
            if (!_blockPresent[blockNumber])
            {
                _blockPresent[blockNumber] = true;
                _blockCount--;
            }
            _bufferMutex.ReleaseMutex();

        }
        /// <summary>
        /// Reset piece so can be refilled
        /// </summary>
        public void Reset()
        {
            _blockCount = (uint)_blockPresent.Length;
            _blockPresent = Enumerable.Repeat(false, (int)_blockCount).ToArray();
        }
        /// <summary>
        /// Set blocks to assemble count
        /// </summary>
        /// <param name="pieceLength"></param>
        public void SetBlocksPresent(UInt32 pieceLength)
        {
            _blockCount = (pieceLength / Constants.BlockSize);
            if (pieceLength % Constants.BlockSize != 0)
            {
                _blockCount++;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool[] BlocksPresent()
        {
            return _blockPresent;
        }

    }
}
