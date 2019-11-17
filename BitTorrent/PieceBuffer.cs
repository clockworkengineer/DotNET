using System;
namespace BitTorrent
{
    public class PieceBuffer
    {
        private UInt32 _pieceNumber;
        private UInt32 _length;
        private byte[] _buffer;

        public uint Length { get => _length; set => _length = value; }
        public byte[] Buffer { get => _buffer; set => _buffer = value; }
        public uint PieceNumber { get => _pieceNumber; set => _pieceNumber = value; }

        public PieceBuffer(UInt32 pieceNumber, byte[] buffer)
        {
            PieceNumber = pieceNumber;
            Length = (UInt32) buffer.Length;
            Buffer = new byte[Length];
            buffer.CopyTo(Buffer,0);
        }
    }
}
