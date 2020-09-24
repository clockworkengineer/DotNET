//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Buffer used to hold and assemble  pieces from
// blocks downloaded.
//
// Copyright 2019.
//

using System;

namespace BitTorrentLibrary
{
    /// <summary>
    /// Assembled piece buffer.
    /// </summary>
    public class PieceBuffer
    {
        public uint Length { get; set; }    // Piece Length
        public byte[] Buffer { get; set; }  // Piece Buffer
        public uint Number { get; set; }    // Piece Number

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
