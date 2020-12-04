using System.Threading;
//
// Author: Rob Tizzard
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
    internal class PieceBuffer
    {
        private readonly bool[] _blockPresent;          // == true then block present
        private int _blockCount;                        // Unfilled block spaces in buffer
        public TorrentContext Tc { get; }               // Torrent context
        public UInt32 Length { get; }                   // Piece Length
        public byte[] Buffer { get; }                   // Piece Buffer
        public UInt32 Number { get; set; }              // Piece Number
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
            _blockCount = (int)Length / Constants.BlockSize;
            if (Length % Constants.BlockSize != 0) _blockCount++;
            _blockPresent = new bool[_blockCount];
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
        /// Copy block from packet to piece buffer.
        /// </summary>
        /// <param name="packetBuffer"></param>
        /// <param name="blockNumber"></param>
        public void AddBlockFromPacket(byte[] packetBuffer, UInt32 blockNumber)
        {
            int blockLength = (int)Math.Min(Length - blockNumber * Constants.BlockSize, Constants.BlockSize);
            System.Buffer.BlockCopy(packetBuffer, 9, Buffer, (Int32)blockNumber * Constants.BlockSize, blockLength);
            if (!_blockPresent[blockNumber])
            {
                _blockPresent[blockNumber] = true;
                Interlocked.Decrement(ref _blockCount);
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
        /// <summary>
        /// 
        /// </summary>
        /// <param name="blockNumber"></param>
        /// <returns></returns>
        public bool IsBlockPresent(UInt32 blockNumber)
        {
            return _blockPresent[blockNumber];
        }
    }
}
