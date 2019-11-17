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
using System.Threading.Tasks;

namespace BitTorrent
{

    public class FileDownloader
    {

        private List<FileDetails> _filesToDownload;
        private DownloadContext _dc;
        private SHA1 _sha;
        private Task _pieceBufferWriterTask;

        public DownloadContext Dc { get => _dc; set => _dc = value; }

        private void CreateEmptyFilesOnDisk()
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

        private bool CheckPieceHash(UInt32 pieceNumber, byte[] pieceBuffer, UInt32 numberOfBytes)
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

        private void GeneratePieceMapFromBuffer(UInt32 pieceNumber, byte[] pieceBuffer, UInt32 numberOfBytes)
        {

            bool pieceThere = CheckPieceHash(pieceNumber, pieceBuffer, numberOfBytes);
            if (pieceThere)
            {
                _dc.totalBytesDownloaded += (UInt64)numberOfBytes;
            }
            UInt32 blockNumber = 0;
            for (; blockNumber < numberOfBytes / Constants.kBlockSize; blockNumber++)
            {
                _dc.BlockPieceLocal(pieceNumber, blockNumber, pieceThere);

            }
            if (numberOfBytes % Constants.kBlockSize != 0)
            {
                _dc.BlockPieceLocal(pieceNumber, (UInt32)numberOfBytes / Constants.kBlockSize, pieceThere);
                _dc.pieceMap[pieceNumber].lastBlockLength = (UInt32)numberOfBytes % Constants.kBlockSize;
                _dc.BlockPieceLast(pieceNumber, (numberOfBytes / Constants.kBlockSize), true);
            }
            else
            {
                _dc.pieceMap[pieceNumber].lastBlockLength = Constants.kBlockSize;
                _dc.BlockPieceLast(pieceNumber, blockNumber - 1, true);
            }
        }

        private void CreatePieceMap()
        {
            byte [] pieceBuffer = new byte[_dc.pieceLength];
            UInt32 pieceNumber = 0;
            UInt32 bytesInBuffer = 0;
            int bytesRead = 0;

            Program.Logger.Debug("Generate pieces downloaded map from local files ...");

            foreach (var file in _filesToDownload)
            {
                Program.Logger.Debug($"File: {file.name}");

                using (var inFileSteam = new FileStream(file.name, FileMode.Open))
                {

                    while ((bytesRead = inFileSteam.Read(pieceBuffer, (Int32)bytesInBuffer, pieceBuffer.Length - (Int32)bytesInBuffer)) > 0)
                    {
                        bytesInBuffer += (UInt32)bytesRead;

                        if (bytesInBuffer == _dc.pieceLength)
                        {
                            GeneratePieceMapFromBuffer(pieceNumber, pieceBuffer, (UInt32)bytesInBuffer);
                            bytesInBuffer = 0;
                            pieceNumber++;
                        }

                    }

                }

            }

            if (bytesInBuffer > 0)
            {
                GeneratePieceMapFromBuffer(pieceNumber, pieceBuffer, (UInt32)bytesInBuffer);
            }

            Program.Logger.Debug("Finished generating downloaded map.");

        }

        private void PieceBufferWriter()
        {
            while(!_dc.pieceBufferWriteQueue.IsCompleted)
            {
                PieceBuffer pieceBuffer = _dc.pieceBufferWriteQueue.Take();

                if (!CheckPieceHash(pieceBuffer.PieceNumber, pieceBuffer.Buffer, _dc.GetPieceLength(pieceBuffer.PieceNumber)))
                {
                    Program.Logger.Error($"Error: Hash for piece {pieceBuffer.PieceNumber} is invalid.");
                    return;
                }

                Program.Logger.Trace($"writePieceToFiles({pieceBuffer.PieceNumber})");

                UInt64 startOffset = (UInt64)(pieceBuffer.PieceNumber * _dc.pieceLength);
                UInt64 endOffset = startOffset + (UInt64)_dc.pieceLength;

                foreach (var file in _filesToDownload)
                {
                    if ((startOffset <= (file.offset + file.length)) && (file.offset <= endOffset))
                    {
                        UInt64 startWrite = Math.Max(startOffset, file.offset);
                        UInt64 endWrite = Math.Min(endOffset, file.offset + file.length);
                        WritePieceToFile(pieceBuffer.Buffer, file, startWrite, endWrite - startWrite);
                    }
                }
            }

        }

        public FileDownloader(List<FileDetails> filesToDownload, UInt32 pieceLength, byte[] pieces)
        {
            _filesToDownload = filesToDownload;
            _sha = new SHA1CryptoServiceProvider();
            _dc = new DownloadContext(filesToDownload, pieceLength, pieces);
            _pieceBufferWriterTask = new Task(PieceBufferWriter);
            _pieceBufferWriterTask.Start();
        }

        ~FileDownloader()
        {
            _dc.pieceBufferWriteQueue.CompleteAdding();
        }

        public void WritePieceToFile(byte[] pieceBuffer, FileDetails file, UInt64 startOffset, UInt64 length)
        {

            try
            {
                Program.Logger.Trace($"writePieceToFile({file.name},{startOffset},{length})");

                using (Stream stream = new FileStream(file.name, FileMode.OpenOrCreate))
                {
                    stream.Seek((Int64)(startOffset - file.offset), SeekOrigin.Begin);
                    stream.Write(pieceBuffer, (Int32) (startOffset % _dc.pieceLength), (Int32)length);
                    stream.Flush();
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

        public void BuildDownloadedPiecesMap()
        {

            try
            {
                CreateEmptyFilesOnDisk();
                CreatePieceMap();
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

        public bool HavePiece(UInt32 pieceNumber)
        {
            try
            {
                Program.Logger.Trace($"havePiece({pieceNumber})");

                for (UInt32 blockNumber = 0; blockNumber < _dc.blocksPerPiece; blockNumber++)
                {
                    if (!_dc.IsBlockPieceLocal(pieceNumber, blockNumber))
                    {
                        return (false);
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

        public bool SelectNextPiece(Peer remotePeer, ref UInt32 nextPiece)
        {
            try
            {
                // In onorder to stop same the piece requested with different peers a lock 
                // is required when trying to get the next unrequested non-local piece
                lock (this) 
                {
                    Program.Logger.Trace($"selectNextPiece()");

                    for (UInt32 pieceNumber = 0; pieceNumber < _dc.numberOfPieces; pieceNumber++)
                    {
                        if (remotePeer.IsPieceOnRemotePeer(pieceNumber))
                        {
                            UInt32 blockNumber = 0;
                            for (; !_dc.IsBlockPieceLast(pieceNumber, blockNumber); blockNumber++)
                            {
                                if (!_dc.IsBlockPieceRequested(pieceNumber, blockNumber) &&
                                    !_dc.IsBlockPieceLocal(pieceNumber, blockNumber))
                                {
                                    nextPiece = pieceNumber;
                                    _dc.MarkPieceRequested(pieceNumber);
                                    return (true);
                                }
                            }
                            if (!_dc.IsBlockPieceRequested(pieceNumber, blockNumber) &&
                                !_dc.IsBlockPieceLocal(pieceNumber, blockNumber))
                            {
                                nextPiece = pieceNumber;
                                _dc.MarkPieceRequested(pieceNumber);
                                return (true);
                            }
                        }
                    }
                }
;
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

    }
}
