using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace BitTorrent
{

    public class FileDownloader
    {

        private List<FileDetails> _filesToDownload;
        private DownloadContext _dc;

        public DownloadContext Dc { get => _dc; set => _dc = value; }

        private void createEmptyFilesOnDisk()
        {
            foreach (var file in _filesToDownload)
            {
                if (!System.IO.File.Exists(file.name))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file.name));
                    using (var fs = new FileStream(file.name, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                    {
                        fs.SetLength((Int64)file.length);
                    }

                }

            }

        }

        public bool checkPieceHash(byte[] hash, UInt32 pieceNumber)
        {
            UInt32 pieceOffset = pieceNumber * Constants.kHashLength;
            for (var byteNumber = 0; byteNumber < Constants.kHashLength; byteNumber++)
            {
                if (hash[byteNumber] != _dc.pieces[pieceOffset + byteNumber])
                {
                    return (false);
                }
            }
            return (true);

        }

        private void createDownloadedPieceMap()
        {
            SHA1 sha = new SHA1CryptoServiceProvider();
            List<byte> pieceBuffer = new List<byte>();
            UInt32 pieceNumber = 0;

            foreach (var file in _filesToDownload)
            {
                using (var inFileSteam = new FileStream(file.name, FileMode.Open))
                {
                    int bytesRead = inFileSteam.Read(_dc.pieceInProgress, 0,  _dc.pieceInProgress.Length - pieceBuffer.Count);

                    while (bytesRead > 0)
                    {
                        for (var byteNumber = 0; byteNumber < bytesRead; byteNumber++)
                        {
                            pieceBuffer.Add(_dc.pieceInProgress[byteNumber]);
                        }

                        if (pieceBuffer.Count == _dc.pieceLength)
                        {
                            byte[] hash = sha.ComputeHash(pieceBuffer.ToArray());
                            bool pieceThere = checkPieceHash(hash, pieceNumber);
                            if (pieceThere)
                            {
                                _dc.totalBytesDownloaded += (UInt64) _dc.pieceLength;
                            }
                            for (UInt32 blockNumber = 0; blockNumber < _dc.blocksPerPiece; blockNumber++)
                            {
                                _dc.blockPieceLocal(pieceNumber, blockNumber, pieceThere);
                                _dc.pieceMap[pieceNumber].blocks[blockNumber].size = Constants.kBlockSize;
                            }
                            pieceBuffer.Clear();
                            pieceNumber++;
                        }
                        bytesRead = inFileSteam.Read(_dc.pieceInProgress, 0, _dc.pieceInProgress.Length - pieceBuffer.Count);

                    }

                }

            }

            if (pieceBuffer.Count > 0)
            {
                byte[] hash = sha.ComputeHash(pieceBuffer.ToArray());
                bool pieceThere = checkPieceHash(hash, pieceNumber);
                if (pieceThere)
                {
                    _dc.totalBytesDownloaded += (UInt64)pieceBuffer.Count;
                }
                for (UInt32 blockNumber = 0; blockNumber < pieceBuffer.Count/Constants.kBlockSize; blockNumber++)
                {
                    _dc.blockPieceLocal(pieceNumber, blockNumber, pieceThere);
                    _dc.pieceMap[pieceNumber].blocks[blockNumber].size = Constants.kBlockSize;
    
                }
                if (pieceBuffer.Count % Constants.kBlockSize != 0)
                {
                    _dc.blockPieceLocal(pieceNumber, (UInt32) pieceBuffer.Count / Constants.kBlockSize, pieceThere);
                     _dc.pieceMap[pieceNumber].blocks[(pieceBuffer.Count / Constants.kBlockSize)].size = (UInt32) pieceBuffer.Count % Constants.kBlockSize;
                 }
            }
        }

        public void writePieceToFile(FileDetails file, UInt64 startOffset, UInt64 length)
        {

            using (Stream stream = new FileStream(file.name, FileMode.OpenOrCreate))
            {
                stream.Seek((int)(startOffset - file.offset), SeekOrigin.Begin);
                stream.Write(_dc.pieceInProgress, (int)(file.offset - ((file.offset / (UInt64)_dc.pieceLength) * (UInt64)_dc.pieceLength)), (int)length);;
            }

        }

        public FileDownloader(List<FileDetails> filesToDownload, UInt32 pieceLength, byte[] pieces)
        {

            _filesToDownload = filesToDownload;
           
            _dc = new DownloadContext(filesToDownload, pieceLength, pieces);

        }

        public void buildDownloadedPiecesMap()
        {

            createEmptyFilesOnDisk();
            createDownloadedPieceMap();

        }

        public bool havePiece(UInt32 pieceNumber)
        {
            for (UInt32 blockNumber=0; blockNumber < _dc.blocksPerPiece; blockNumber++)
            {
                if (!_dc.isBlockPieceLocal(pieceNumber, blockNumber))
                {
                    return (false);
                }
            }
            return (true);
        }

        public Int64 selectNextPiece()
        {

            for (UInt32 pieceNumber = 0; pieceNumber < _dc.numberOfPieces; pieceNumber++)
            {
                for (UInt32 blockNumber = 0; blockNumber < _dc.blocksPerPiece; blockNumber++)
                {
                    if (!_dc.isBlockPieceLocal(pieceNumber, blockNumber))
                    {
                        return (pieceNumber);
                    }
                }
            }
            return (-1 );

        }
     
        public void placeBlockIntoPiece (byte[] buffer, UInt32 offset, UInt32 blockOffset, UInt32 length)
        {
            Buffer.BlockCopy(buffer, 9, _dc.pieceInProgress, (Int32) blockOffset, (Int32)length);
        }

        public void writePieceToFiles(UInt32 pieceNumber)
        {

            UInt64 startOffset = (UInt64) (pieceNumber * _dc.pieceLength);
            UInt64 endOffset = startOffset+ (UInt64) _dc.pieceLength;

            foreach (var file in _filesToDownload)
            {
                if ((startOffset <= (file.offset + file.length)) && (file.offset <= endOffset))
                {
                    UInt64 startWrite = Math.Max(startOffset, file.offset);
                    UInt64 endWrite = Math.Min(endOffset, file.offset + file.length);
                    writePieceToFile(file, startWrite, endWrite - startWrite);
                }
            }
        }
    }
}
