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
            Program.Logger.Debug("Creating empty files as placeholders for downloading ...");

            foreach (var file in _filesToDownload)
            {
                if (!System.IO.File.Exists(file.name))
                {
                    Program.Logger.Debug($"File: {file.name}");
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

        private void generatePieceMapFromBuffer(SHA1 sha, UInt32 pieceNumber, byte[] pieceBuffer, UInt32 numberOfBytes)
        {

            byte[] hash = sha.ComputeHash(pieceBuffer,0, (Int32) numberOfBytes);
            bool pieceThere = checkPieceHash(hash, pieceNumber);
            if (pieceThere)
            {
                _dc.totalBytesDownloaded += (UInt64)numberOfBytes;
            }
            for (UInt32 blockNumber = 0; blockNumber < numberOfBytes / Constants.kBlockSize; blockNumber++)
            {
                _dc.blockPieceLocal(pieceNumber, blockNumber, pieceThere);
                _dc.pieceMap[pieceNumber].blocks[blockNumber].size = Constants.kBlockSize;

            }
            if (numberOfBytes % Constants.kBlockSize != 0)
            {
                _dc.blockPieceLocal(pieceNumber, (UInt32)numberOfBytes / Constants.kBlockSize, pieceThere);
                _dc.pieceMap[pieceNumber].blocks[(numberOfBytes / Constants.kBlockSize)].size = (UInt32)numberOfBytes % Constants.kBlockSize;
                _dc.pieceMap[pieceNumber].lastBlockLength = (UInt32)numberOfBytes % Constants.kBlockSize;
            }
        }

        private void createPieceMap()
        {
            SHA1 sha = new SHA1CryptoServiceProvider();
            UInt32 pieceNumber = 0;
            UInt32 bytesInBuffer = 0;

            Program.Logger.Debug("Generate pieces downloaded map from local files ...");

            foreach (var file in _filesToDownload)
            {
                Program.Logger.Debug($"File: {file.name}");

                using (var inFileSteam = new FileStream(file.name, FileMode.Open))
                {
                    int bytesRead = inFileSteam.Read(_dc.pieceInProgress, (Int32) bytesInBuffer,  _dc.pieceInProgress.Length - (Int32) bytesInBuffer);

                    while (bytesRead > 0)
                    {
                        bytesInBuffer += (UInt32)bytesRead;

                        if (bytesInBuffer == _dc.pieceLength)
                        {
                            generatePieceMapFromBuffer(sha, pieceNumber, _dc.pieceInProgress, (UInt32)bytesInBuffer);
                            bytesInBuffer = 0;
                            pieceNumber++;
                        }

                        bytesRead = inFileSteam.Read(_dc.pieceInProgress, (Int32)bytesInBuffer, _dc.pieceInProgress.Length - (Int32)bytesInBuffer);

                    }

                }

            }

            if (bytesInBuffer > 0)
            {
                generatePieceMapFromBuffer(sha, pieceNumber, _dc.pieceInProgress, (UInt32)bytesInBuffer);
            }
        }

        public void writePieceToFile(FileDetails file, UInt64 startOffset, UInt64 length)
        {

            Program.Logger.Trace($"writePieceToFile({file.name},{startOffset},{length})");

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
            createPieceMap();

        }

        public bool havePiece(UInt32 pieceNumber)
        {
            Program.Logger.Trace($"havePiece({pieceNumber})");

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
            Program.Logger.Trace($"selectNextPiece()");

            for (UInt32 pieceNumber = 0; pieceNumber < _dc.numberOfPieces; pieceNumber++)
            {
                for (UInt32 blockNumber = 0; blockNumber < _dc.blocksPerPiece; blockNumber++)
                {
                    if ((_dc.pieceMap[pieceNumber].blocks[blockNumber].size!=0) &&
                        !_dc.isBlockPieceLocal(pieceNumber, blockNumber))
                    {
                        return (pieceNumber);
                    }
                }
            }
            return (-1 );

        }
     
        public void placeBlockIntoPiece (byte[] buffer, UInt32 pieceNumber, UInt32 blockOffset, UInt32 length)
        {
            Program.Logger.Trace($"placeBlockIntoPiece({pieceNumber},{blockOffset},{length})");

            Buffer.BlockCopy(buffer, 9, _dc.pieceInProgress, (Int32) blockOffset, (Int32)length);

            _dc.blockPieceDownloaded(pieceNumber, blockOffset / Constants.kBlockSize, true);
;
            _dc.totalBytesDownloaded += (UInt64)_dc.pieceMap[pieceNumber].blocks[blockOffset / Constants.kBlockSize].size;

        }

        public void writePieceToFiles(UInt32 pieceNumber)
        {
            Program.Logger.Trace($"writePieceToFiles({pieceNumber})");

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
