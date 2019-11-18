using System;
namespace BitTorrent
{
    public class PieceBuffer
    {
        private UInt32 _number;
        private UInt32 _length;
        private byte[] _buffer;

        public uint Length { get => _length; set => _length = value; }
        public byte[] Buffer { get => _buffer; set => _buffer = value; }
        public uint Number { get => _number; set => _number = value; }

        public PieceBuffer(UInt32 length)
        {
            Number = 0;
            Length = length;
            Buffer = new byte[Length];
        }

        public PieceBuffer(PieceBuffer pieceBuffer)
        {
            Number = pieceBuffer.Number;
            Length = pieceBuffer.Length;
            Buffer = new byte[Length];
            pieceBuffer.Buffer.CopyTo(Buffer, 0);
        }

        public PieceBuffer(UInt32 pieceNumber, byte[] buffer)
        {
            Number = pieceNumber;
            Length = (UInt32) buffer.Length;
            Buffer = new byte[Length];
            buffer.CopyTo(Buffer,0);
        }
    }
}
