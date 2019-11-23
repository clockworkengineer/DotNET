//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: 
//
// Copyright 2019.
//

using System;

namespace BitTorrent
{
    /// <summary>
    /// Assembled piece buffer.
    /// </summary>
    public class PieceBuffer
    {

        private UInt32 _number; // Piece number
        private UInt32 _length; // Piece length
        private byte[] _buffer; // buffer

        public uint Length { get => _length; set => _length = value; }
        public byte[] Buffer { get => _buffer; set => _buffer = value; }
        public uint Number { get => _number; set => _number = value; }

        /// <summary>
        /// Create an empty piece buffer.
        /// </summary>
        /// <param name="length">Length.</param>
        public PieceBuffer(UInt32 length)
        {
            Number = 0;
            Length = length;
            Buffer = new byte[Length];
        }

        /// <summary>
        /// Create a piece buffer from a copy of another.
        /// </summary>
        /// <param name="pieceBuffer">Piece buffer.</param>
        public PieceBuffer(PieceBuffer pieceBuffer)
        {
            Number = pieceBuffer.Number;
            Length = pieceBuffer.Length;
            Buffer = new byte[Length];
            pieceBuffer.Buffer.CopyTo(Buffer, 0);
        }

    }
}
