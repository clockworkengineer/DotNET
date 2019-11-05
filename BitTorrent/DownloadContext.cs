using System;
using System.Collections.Generic;
namespace BitTorrent
{
    public static class Mapping
    {
         public const byte none = 0x01;
         public const byte requested = 0x02;
         public const byte have = 0x4;
    }

    public struct BlockData
    {
        public bool mapped;
        public byte flags;
        public int size;
    }

    public struct FileRecievedMap
    {
        public BlockData[] blocks;
    }

    public class DownloadContext
    {
        public FileRecievedMap[] receivedMap;
        public FileRecievedMap[] remotePeerMap;
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
            remotePeerMap = new FileRecievedMap[numberOfPieces];
            receivedMap = new FileRecievedMap[numberOfPieces];

            for (var pieceNuber = 0; pieceNuber < numberOfPieces; pieceNuber++)
            {
                receivedMap[pieceNuber].blocks = new BlockData[blocksPerPiece];
                remotePeerMap[pieceNuber].blocks = new BlockData[blocksPerPiece];
            }
        }
    }
}
