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

        public bool checkPieceHash(byte[] hash, int pieceNumber)
        {
            int pieceOffset = pieceNumber * Constants.kHashLength;
            for (var byteNumber = 0; byteNumber < Constants.kHashLength; byteNumber++)
            {
                if (hash[byteNumber] != _dc.pieces[pieceOffset + byteNumber])
                {
                    return (false);
                }
            }
            return (true);

        }

        private void createReceivedMap()
        {
            SHA1 sha = new SHA1CryptoServiceProvider();
            List<byte> pieceBuffer = new List<byte>();
            int pieceNumber = 0;

            foreach (var file in _filesToDownload)
            {
                using (var inFileSteam = new FileStream(file.name, FileMode.Open))
                {
                    int bytesRead = inFileSteam.Read(_dc.pieceInProgress, 0, _dc.pieceInProgress.Length - pieceBuffer.Count);

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
                            for (int block = 0; block < _dc.blocksPerPiece; block++)
                            {
                                _dc.pieceMap[pieceNumber].blocks[block].flags = (pieceThere) ? Mapping.Havelocal : Mapping.NoneLocal;
                                _dc.pieceMap[pieceNumber].blocks[block].size = Constants.kBlockSize;
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
                for (int block = 0; block < pieceBuffer.Count/Constants.kBlockSize; block++)
                {
                    _dc.pieceMap[pieceNumber].blocks[block].flags = (pieceThere) ? Mapping.Havelocal : Mapping.NoneLocal;
                    _dc.pieceMap[pieceNumber].blocks[block].size = Constants.kBlockSize;
    
                }
                if (pieceBuffer.Count % Constants.kBlockSize != 0)
                {
                    _dc.pieceMap[pieceNumber].blocks[(pieceBuffer.Count / Constants.kBlockSize)].flags = (pieceThere) ? Mapping.Havelocal : Mapping.NoneLocal;
                    _dc.pieceMap[pieceNumber].blocks[(pieceBuffer.Count / Constants.kBlockSize)].size = pieceBuffer.Count % Constants.kBlockSize;
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

        public FileDownloader(List<FileDetails> filesToDownload, int pieceLength, byte[] pieces)
        {

            _filesToDownload = filesToDownload;
           
            _dc = new DownloadContext(filesToDownload, pieceLength, pieces);

        }

        public void buildDownloadedPiecesMap()
        {

            createEmptyFilesOnDisk();
            createReceivedMap();

        }

        public bool havePiece(int pieceNumber)
        {
            for (int block=0; block < _dc.blocksPerPiece; block++)
            {
                if ((_dc.pieceMap[pieceNumber].blocks[block].flags & Mapping.NoneLocal)==Mapping.NoneLocal)
                {
                    return (false);
                }
            }
            return (true);
        }

        public int selectNextPiece()
        {

            for (var pieceNumber = 0; pieceNumber < _dc.numberOfPieces; pieceNumber++)
            {
                for (var blockNumber = 0; blockNumber < _dc.blocksPerPiece; blockNumber++)
                {
                    if (((_dc.pieceMap[pieceNumber].blocks[blockNumber].flags & Mapping.OnPeer) == Mapping.OnPeer) && 
                         ((_dc.pieceMap[pieceNumber].blocks[blockNumber].flags & Mapping.NoneLocal)==Mapping.NoneLocal))
                    {
                        return (pieceNumber);
                    }
                }
            }
            return (-1);

        }
     
        public void placeBlockIntoPiece (byte[] buffer, int offset, int blockOffset, int length)
        {
            Buffer.BlockCopy(buffer, 9, _dc.pieceInProgress, blockOffset, length);
        }

        public void writePieceToFiles(int pieceNumber)
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
