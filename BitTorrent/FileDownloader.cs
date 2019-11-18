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
    /// <summary>
    /// File downloader.
    /// </summary>
    public class FileDownloader
    {

        private List<FileDetails> _filesToDownload;
        private DownloadContext _dc;
        private SHA1 _sha;
        private Task _pieceBufferWriterTask;

        public DownloadContext Dc { get => _dc; set => _dc = value; }

        /// <summary>
        /// Creates the empty files on disk.
        /// </summary>
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

        /// <summary>
        /// Checks the piece hash.
        /// </summary>
        /// <returns><c>true</c>, if piece hash was checked, <c>false</c> otherwise.</returns>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="pieceBuffer">Piece buffer.</param>
        /// <param name="numberOfBytes">Number of bytes.</param>
        private bool CheckPieceHash(UInt32 pieceNumber, byte[] pieceBuffer, UInt32 numberOfBytes)
        {
            byte[] hash = _sha.ComputeHash(pieceBuffer, 0, (Int32)numberOfBytes);
            UInt32 pieceOffset = pieceNumber * Constants.kHashLength;
            for (var byteNumber = 0; byteNumber < Constants.kHashLength; byteNumber++)
            {
                if (hash[byteNumber] != _dc.piecesInfoHash[pieceOffset + byteNumber])
                {
                    return (false);
                }
            }
            return (true);

        }

        /// <summary>
        /// Generates the piece map from buffer.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="pieceBuffer">Piece buffer.</param>
        /// <param name="numberOfBytes">Number of bytes.</param>
        private void GeneratePieceMapFromBuffer(UInt32 pieceNumber, byte[] pieceBuffer, UInt32 numberOfBytes)
        {

            bool pieceThere = CheckPieceHash(pieceNumber, pieceBuffer, numberOfBytes);
            if (pieceThere)
            {
                _dc.totalBytesDownloaded += numberOfBytes;
            }
            UInt32 blockNumber = 0;
            for (; blockNumber < numberOfBytes / Constants.kBlockSize; blockNumber++)
            {
                _dc.BlockPieceLocal(pieceNumber, blockNumber, pieceThere);

            }
            if (numberOfBytes % Constants.kBlockSize != 0)
            {
                _dc.BlockPieceLocal(pieceNumber, numberOfBytes / Constants.kBlockSize, pieceThere);
                _dc.pieceMap[pieceNumber].lastBlockLength = numberOfBytes % Constants.kBlockSize;
                _dc.BlockPieceLast(pieceNumber, (numberOfBytes / Constants.kBlockSize), true);
            }
            else
            {
                _dc.pieceMap[pieceNumber].lastBlockLength = Constants.kBlockSize;
                _dc.BlockPieceLast(pieceNumber, blockNumber - 1, true);
            }
        }

        /// <summary>
        /// Creates the piece map.
        /// </summary>
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
                            GeneratePieceMapFromBuffer(pieceNumber, pieceBuffer, bytesInBuffer);
                            bytesInBuffer = 0;
                            pieceNumber++;
                        }

                    }

                }

            }

            if (bytesInBuffer > 0)
            {
                GeneratePieceMapFromBuffer(pieceNumber, pieceBuffer, bytesInBuffer);
            }

            Program.Logger.Debug("Finished generating downloaded map.");

        }

        /// <summary>
        /// Pieces the buffer writer.
        /// </summary>
        private void PieceBufferWriter()
        {
            while(!_dc.pieceBufferWriteQueue.IsCompleted)
            {
                PieceBuffer pieceBuffer = _dc.pieceBufferWriteQueue.Take();

                if (CheckPieceHash(pieceBuffer.Number, pieceBuffer.Buffer, _dc.GetPieceLength(pieceBuffer.Number)))
                {
                    Program.Logger.Trace($"writePieceToFiles({pieceBuffer.Number})");

                    UInt64 startOffset = pieceBuffer.Number * _dc.pieceLength;
                    UInt64 endOffset = startOffset + _dc.pieceLength;

                    foreach (var file in _filesToDownload)
                    {
                        if ((startOffset <= (file.offset + file.length)) && (file.offset <= endOffset))
                        {
                            UInt64 startWrite = Math.Max(startOffset, file.offset);
                            UInt64 endWrite = Math.Min(endOffset, file.offset + file.length);
                            using (Stream stream = new FileStream(file.name, FileMode.OpenOrCreate))
                            {
                                stream.Seek((Int64)(startWrite - file.offset), SeekOrigin.Begin);
                                stream.Write(pieceBuffer.Buffer, (Int32)(startWrite % _dc.pieceLength), (Int32)(endWrite - startWrite));
                                stream.Flush();
                            }
                        }
                    }
                }
                else
                {
                     Program.Logger.Error($"Error: Hash for piece {pieceBuffer.Number} is invalid.");
                }

              
            }

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:BitTorrent.FileDownloader"/> class.
        /// </summary>
        /// <param name="filesToDownload">Files to download.</param>
        /// <param name="pieceLength">Piece length.</param>
        /// <param name="pieces">Pieces.</param>
        public FileDownloader(List<FileDetails> filesToDownload, UInt32 pieceLength, byte[] pieces)
        {
            _filesToDownload = filesToDownload;
            _sha = new SHA1CryptoServiceProvider();
            _dc = new DownloadContext(filesToDownload, pieceLength, pieces);
            _pieceBufferWriterTask = new Task(PieceBufferWriter);
            _pieceBufferWriterTask.Start();
        }

        /// <summary>
        /// Releases unmanaged resources and performs other cleanup operations before the
        /// <see cref="T:BitTorrent.FileDownloader"/> is reclaimed by garbage collection.
        /// </summary>
        ~FileDownloader()
        {
            _dc.pieceBufferWriteQueue.CompleteAdding();
        }

        /// <summary>
        /// Builds the downloaded pieces map.
        /// </summary>
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

        /// <summary>
        /// Haves the piece.
        /// </summary>
        /// <returns><c>true</c>, if piece was had, <c>false</c> otherwise.</returns>
        /// <param name="pieceNumber">Piece number.</param>
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

        /// <summary>
        /// Selects the next piece.
        /// </summary>
        /// <returns><c>true</c>, if next piece was selected, <c>false</c> otherwise.</returns>
        /// <param name="remotePeer">Remote peer.</param>
        /// <param name="nextPiece">Next piece.</param>
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
