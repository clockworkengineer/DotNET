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
using System.Collections.Generic;

namespace BitTorrent
{
    public static class Mapping
    {
        public const byte HaveLocal = 0x01;
        public const byte OnPeer = 0x02;
        public const byte LastBlock = 0x04;
    }

    public struct PieceBlockMap
    {
        public byte[] blocks;
        public UInt16 peerCount;
        public UInt32 lastBlockLength;
    }

    public class DownloadContext
    {
        public PieceBlockMap[] pieceMap;
        public UInt64 totalBytesDownloaded = 0;
        public byte[] pieceBuffer;
        public UInt64 totalLength = 0;
        public UInt32 pieceLength = 0;
        public UInt32 blocksPerPiece = 0;
        public byte[] pieces;
        public UInt32 numberOfPieces = 0;

        public DownloadContext(List<FileDetails> filesToDownload, UInt32 pieceLength, byte[] pieces)
        {
            try
            {
                foreach (var file in filesToDownload)
                {
                    totalLength += file.length;
                }

                this.pieceLength = pieceLength;
                this.pieces = pieces;
                numberOfPieces = ((UInt32)(pieces.Length / Constants.kHashLength));
                blocksPerPiece = pieceLength / Constants.kBlockSize;
                pieceBuffer = new byte[pieceLength];

                pieceMap = new PieceBlockMap[numberOfPieces];
                for (var pieceNuber = 0; pieceNuber < numberOfPieces; pieceNuber++)
                {
                    pieceMap[pieceNuber].blocks = new byte[blocksPerPiece];
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }

        public void blockPieceLocal(UInt32 pieceNumber, UInt32 blockNumber, bool local)
        {
            try
            {
                if (local)
                {
                    pieceMap[pieceNumber].blocks[blockNumber] |= Mapping.HaveLocal;
                }
                else
                {
                    pieceMap[pieceNumber].blocks[blockNumber] &= (Mapping.HaveLocal ^ 0xff);
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }

        public void blockPieceOnPeer(UInt32 pieceNumber, UInt32 blockNumber, bool noPeer)
        {
            try
            {
                if (noPeer)
                {
                    pieceMap[pieceNumber].blocks[blockNumber] |= Mapping.OnPeer;
                }
                else
                {
                    pieceMap[pieceNumber].blocks[blockNumber] &= (Mapping.OnPeer ^ 0xff);
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }

        public void blockPieceDownloaded(UInt32 pieceNumber, UInt32 blockNumber, bool downloaded)
        {
            try
            {
                if (downloaded)
                {
                    pieceMap[pieceNumber].blocks[blockNumber] |= Mapping.OnPeer;
                    pieceMap[pieceNumber].blocks[blockNumber] |= Mapping.HaveLocal;
                }
                else
                {
                    pieceMap[pieceNumber].blocks[blockNumber] &= (Mapping.HaveLocal ^ 0xff);
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }

        public void blockPieceLast(UInt32 pieceNumber, UInt32 blockNumber, bool last)
        {
            try
            {
                if (last)
                {
                    pieceMap[pieceNumber].blocks[blockNumber] |= Mapping.LastBlock;
                }
                else
                {
                    pieceMap[pieceNumber].blocks[blockNumber] &= (Mapping.LastBlock ^ 0xff);
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }

        public bool isBlockPieceOnPeer(UInt32 pieceNumber, UInt32 blockNumber)
        {
            try
            {
                return ((pieceMap[pieceNumber].blocks[blockNumber] & Mapping.OnPeer) == Mapping.OnPeer);
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
            return (false);
        }

        public bool isBlockPieceLocal(UInt32 pieceNumber, UInt32 blockNumber)
        {
            try
            {
                return ((pieceMap[pieceNumber].blocks[blockNumber] & Mapping.HaveLocal) == Mapping.HaveLocal);
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
            return (false);
        }

        public bool isBlockPieceLast(UInt32 pieceNumber, UInt32 blockNumber)
        {
            try
            {
                return ((pieceMap[pieceNumber].blocks[blockNumber] & Mapping.LastBlock) == Mapping.LastBlock);
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
            return (false);
        }

        public UInt32 getPieceLength(UInt32 pieceNumber)
        {

            UInt32 length = 0;

            try
            {
  
                for (UInt32 blockNumber = 0; !isBlockPieceLast(pieceNumber, blockNumber); blockNumber++)
                {
                    length += Constants.kBlockSize;
                }
                length += pieceMap[pieceNumber].lastBlockLength;
            }

            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }

            return (length);

        }

        public bool hasPieceBeenAssembled(UInt32 pieceNumber)
        {

            try
            {
                for (UInt32 blockNumber = 0; blockNumber < pieceMap[pieceNumber].blocks.Length; blockNumber++)
                {
                    if (!isBlockPieceLocal(pieceNumber, blockNumber))
                    {
                        return (false);
                    }
                    if (isBlockPieceLast(pieceNumber, blockNumber))
                    {
                        break;
                    }

                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
            return (true);
        }

        public void mergePieceBitfield(Peer remotePeer)
        {

            try
            {
                UInt32 pieceNumber = 0;
                for (int i = 0; i < remotePeer.RemotePieceBitfield.Length; i++)
                {
                    byte map = remotePeer.RemotePieceBitfield[i];
                    for (byte bit = 0x80; bit != 0; bit >>= 1, pieceNumber++)
                    {
                        if ((map & bit) != 0)
                        {
                            pieceMap[pieceNumber].peerCount++;
                            for (UInt32 blockNumber = 0; blockNumber < remotePeer.TorrentDownloader.Dc.blocksPerPiece; blockNumber++)
                            {
                                remotePeer.TorrentDownloader.Dc.blockPieceOnPeer(pieceNumber, blockNumber, true);
                            }

                        }

                    }
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }
    }
}
