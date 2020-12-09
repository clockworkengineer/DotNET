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
using System.Threading;
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
        public bool[] BlocksPresent => _blockPresent;   // == true then block is present
        /// <summary>
        /// Create an empty piece buffer.
        /// </summary>
        /// <param name="length">Length.</param>
        public PieceBuffer(TorrentContext tc, UInt32 length)
        {
            Tc = tc ?? throw new ArgumentNullException(nameof(tc));
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
            if (packetBuffer is null)
            {
                throw new ArgumentNullException(nameof(packetBuffer));
            }
            int blockLength = (int)Math.Min(Length - blockNumber * Constants.BlockSize, Constants.BlockSize);
            System.Buffer.BlockCopy(packetBuffer, 9, Buffer, (Int32)blockNumber * Constants.BlockSize, blockLength);
            if (!_blockPresent[blockNumber])
            {
                _blockPresent[blockNumber] = true;
                Interlocked.Decrement(ref _blockCount);
            }
        }
    }
}
