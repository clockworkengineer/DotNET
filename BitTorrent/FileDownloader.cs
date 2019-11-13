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
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace BitTorrent
{

    public class FileDownloader
    {

        private List<FileDetails> _filesToDownload;
        private DownloadContext _dc;
        private SHA1 _sha;

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

        private bool checkPieceHash(UInt32 pieceNumber, byte[] pieceBuffer, UInt32 numberOfBytes)
        {
            byte[] hash = _sha.ComputeHash(pieceBuffer, 0, (Int32)numberOfBytes);
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

        private void generatePieceMapFromBuffer(UInt32 pieceNumber, byte[] pieceBuffer, UInt32 numberOfBytes)
        {

            bool pieceThere = checkPieceHash(pieceNumber, pieceBuffer, numberOfBytes);
            if (pieceThere)
            {
                _dc.totalBytesDownloaded += (UInt64)numberOfBytes;
            }
            UInt32 blockNumber = 0;
            for (; blockNumber < numberOfBytes / Constants.kBlockSize; blockNumber++)
            {
                _dc.blockPieceLocal(pieceNumber, blockNumber, pieceThere);

            }
            if (numberOfBytes % Constants.kBlockSize != 0)
            {
                _dc.blockPieceLocal(pieceNumber, (UInt32)numberOfBytes / Constants.kBlockSize, pieceThere);
                _dc.pieceMap[pieceNumber].lastBlockLength = (UInt32)numberOfBytes % Constants.kBlockSize;
                _dc.blockPieceLast(pieceNumber, (numberOfBytes / Constants.kBlockSize), true);
            }
            else
            {
                _dc.pieceMap[pieceNumber].lastBlockLength = Constants.kBlockSize;
                _dc.blockPieceLast(pieceNumber, blockNumber - 1, true);
            }
        }

        private void createPieceMap()
        {

            UInt32 pieceNumber = 0;
            UInt32 bytesInBuffer = 0;
            int bytesRead = 0;

            Program.Logger.Debug("Generate pieces downloaded map from local files ...");

            foreach (var file in _filesToDownload)
            {
                Program.Logger.Debug($"File: {file.name}");

                using (var inFileSteam = new FileStream(file.name, FileMode.Open))
                {

                    while ((bytesRead = inFileSteam.Read(_dc.pieceBuffer, (Int32)bytesInBuffer, _dc.pieceBuffer.Length - (Int32)bytesInBuffer)) > 0)
                    {
                        bytesInBuffer += (UInt32)bytesRead;

                        if (bytesInBuffer == _dc.pieceLength)
                        {
                            generatePieceMapFromBuffer(pieceNumber, _dc.pieceBuffer, (UInt32)bytesInBuffer);
                            bytesInBuffer = 0;
                            pieceNumber++;
                        }

                    }

                }

            }

            if (bytesInBuffer > 0)
            {
                generatePieceMapFromBuffer(pieceNumber, _dc.pieceBuffer, (UInt32)bytesInBuffer);
            }

        }

        public void writePieceToFile(FileDetails file, UInt64 startOffset, UInt64 length)
        {

            try
            {
                Program.Logger.Trace($"writePieceToFile({file.name},{startOffset},{length})");

                using (Stream stream = new FileStream(file.name, FileMode.OpenOrCreate))
                {
                    stream.Seek((Int64)(startOffset - file.offset), SeekOrigin.Begin);
                    stream.Write(_dc.pieceBuffer, (int)(file.offset - ((file.offset / (UInt64)_dc.pieceLength) * (UInt64)_dc.pieceLength)), (int)length); ;
                }
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }

        }

        public FileDownloader(List<FileDetails> filesToDownload, UInt32 pieceLength, byte[] pieces)
        {

            _filesToDownload = filesToDownload;
            _sha = new SHA1CryptoServiceProvider();
            _dc = new DownloadContext(filesToDownload, pieceLength, pieces);

        }

        public void buildDownloadedPiecesMap()
        {

            try
            {
                createEmptyFilesOnDisk();
                createPieceMap();
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }

        }

        public bool havePiece(UInt32 pieceNumber)
        {
            try
            {
                Program.Logger.Trace($"havePiece({pieceNumber})");

                for (UInt32 blockNumber = 0; blockNumber < _dc.blocksPerPiece; blockNumber++)
                {
                    if (!_dc.isBlockPieceLocal(pieceNumber, blockNumber))
                    {
                        return (false);
                    }
                }

            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }

            return (true);
        }

        public bool selectNextPiece(ref UInt32 nextPiece)
        {
            try
            {
                Program.Logger.Trace($"selectNextPiece()");

                for (UInt32 pieceNumber = 0; pieceNumber < _dc.numberOfPieces; pieceNumber++)
                {
                    UInt32 blockNumber = 0;
                    for (; !_dc.isBlockPieceLast(pieceNumber, blockNumber); blockNumber++)
                    {
                        if (!_dc.isBlockPieceLocal(pieceNumber, blockNumber))
                        {
                            nextPiece = pieceNumber;
                            return (true);
                        }
                    }
                    if (!_dc.isBlockPieceLocal(pieceNumber, blockNumber))
                    {
                        nextPiece = pieceNumber;
                        return (true);
                    }
                }
;
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }

            return (false);

        }

        public void placeBlockIntoPiece(byte[] buffer, UInt32 pieceNumber, UInt32 blockOffset, UInt32 length)
        {
            try
            {
                Program.Logger.Trace($"placeBlockIntoPiece({pieceNumber},{blockOffset},{length})");

                Buffer.BlockCopy(buffer, 9, _dc.pieceBuffer, (Int32)blockOffset, (Int32)length);

                _dc.blockPieceDownloaded(pieceNumber, blockOffset / Constants.kBlockSize, true);

                if (!_dc.isBlockPieceLast(pieceNumber, blockOffset / Constants.kBlockSize))
                {
                    _dc.totalBytesDownloaded += (UInt64)Constants.kBlockSize;
                }
                else
                {
                    _dc.totalBytesDownloaded += (UInt64)_dc.pieceMap[pieceNumber].lastBlockLength;
                }
            }
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }

        public void writePieceToFiles(UInt32 pieceNumber)
        {
            try
            {

                if (!checkPieceHash(pieceNumber, _dc.pieceBuffer, _dc.pieceLength)){
                    throw new Error($"Error: Hash for piece {pieceNumber} is invalid.");
                }

                Program.Logger.Trace($"writePieceToFiles({pieceNumber})");

                UInt64 startOffset = (UInt64)(pieceNumber * _dc.pieceLength);
                UInt64 endOffset = startOffset + (UInt64)_dc.pieceLength;

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
            catch (Exception ex)
            {
                Program.Logger.Debug(ex);
            }
        }
    }
}
