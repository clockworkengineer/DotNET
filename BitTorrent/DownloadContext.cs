using System;
using System.Collections.Generic;
namespace BitTorrent
{
    public static class Mapping
    {
         public const byte NoneLocal = 0x01;
         public const byte Requested = 0x02;
         public const byte Havelocal = 0x4;
         public const byte OnPeer = 0x8;
    }

    public struct BlockData
    {
        public byte flags;
        public int size;
    }

    public struct PieceBlockMap
    {
        public BlockData[] blocks;
    }

    public class DownloadContext
    {
        public PieceBlockMap[] pieceMap;
        public UInt64 totalBytesDownloaded;
        public byte[] pieceInProgress;
        public UInt64 totalLength = 0;
        public int pieceLength = 0;
        public int blocksPerPiece = 0;
        public byte[] pieces;
        public int numberOfPieces = 0;

        public DownloadContext(List<FileDetails> filesToDownload, int pieceLength, byte[] pieces)
        {
            foreach (var file in filesToDownload)
            {
                totalLength += file.length;
            }

            this.pieceLength = pieceLength;
            this.pieces = pieces;
            numberOfPieces = pieces.Length / Constants.kHashLength;
            blocksPerPiece = pieceLength / Constants.kBlockSize;
            pieceInProgress = new byte[pieceLength];
            pieceMap = new PieceBlockMap[numberOfPieces];

            for (var pieceNuber = 0; pieceNuber < numberOfPieces; pieceNuber++)
            {
                pieceMap[pieceNuber].blocks = new BlockData[blocksPerPiece];
            }
        }
    }
}
