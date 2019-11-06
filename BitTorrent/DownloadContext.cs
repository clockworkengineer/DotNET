using System;
using System.Collections.Generic;
namespace BitTorrent
{
    public static class Mapping
    {
         public const byte HaveLocal = 0x01;
         public const byte OnPeer = 0x02;
    }

    public struct BlockData
    {
        public byte flags;
        public UInt32 size;
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
        public UInt32 pieceLength = 0;
        public UInt32 blocksPerPiece = 0;
        public byte[] pieces;
        public UInt32 numberOfPieces = 0;

        public DownloadContext(List<FileDetails> filesToDownload, UInt32 pieceLength, byte[] pieces)
        {
            foreach (var file in filesToDownload)
            {
                totalLength += file.length;
            }

            this.pieceLength = pieceLength;
            this.pieces = pieces;
            numberOfPieces = ((UInt32)(pieces.Length / Constants.kHashLength));
            blocksPerPiece = pieceLength / Constants.kBlockSize;
            pieceInProgress = new byte[pieceLength];
            pieceMap = new PieceBlockMap[numberOfPieces];

            for (var pieceNuber = 0; pieceNuber < numberOfPieces; pieceNuber++)
            {
                pieceMap[pieceNuber].blocks = new BlockData[blocksPerPiece];
            }
        }

        public void blockPieceLocal(UInt32 pieceNumber, UInt32 blockNumber, bool local)
        {
            if (local)
            {
                pieceMap[pieceNumber].blocks[blockNumber].flags |= Mapping.HaveLocal;
            }
            else
            {
                pieceMap[pieceNumber].blocks[blockNumber].flags &= (Mapping.HaveLocal ^ 0xff);
            }
        }

        public void blockPieceOnPeer(UInt32 pieceNumber, UInt32 blockNumber, bool noPeer)
        {
            if (noPeer)
            {
                pieceMap[pieceNumber].blocks[blockNumber].flags |= Mapping.OnPeer;
            }
            else
            {
                pieceMap[pieceNumber].blocks[blockNumber].flags &= (Mapping.OnPeer ^ 0xff);
            }
        }

        public void blockPieceDownloaded(UInt32 pieceNumber, UInt32 blockNumber, bool downloaded)
        {
            if (downloaded)
            {
                pieceMap[pieceNumber].blocks[blockNumber].flags |= Mapping.OnPeer;
                pieceMap[pieceNumber].blocks[blockNumber].flags |= Mapping.HaveLocal;
            }
            else
            {
                pieceMap[pieceNumber].blocks[blockNumber].flags &= (Mapping.HaveLocal ^ 0xff);
            }
        }

        public bool isBlockPieceOnPeer(UInt32 pieceNumber, UInt32 blockNumber)
        {
            return ((pieceMap[pieceNumber].blocks[blockNumber].flags & Mapping.OnPeer)==Mapping.OnPeer);
        }

        public bool isBlockPieceLocal(UInt32 pieceNumber, UInt32 blockNumber)
        {
            return ((pieceMap[pieceNumber].blocks[blockNumber].flags & Mapping.HaveLocal) == Mapping.HaveLocal);
        }


    }
}
