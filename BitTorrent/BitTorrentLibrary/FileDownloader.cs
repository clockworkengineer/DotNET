//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: The File Downloader class encapsulates all code and
// data relating to the readining/writing of the local torrent files
// to determine which pieces are missing and need downloading and
// written to the correct positions.
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

        private List<FileDetails> _filesToDownload; // Files in torrent to be dowloaded
        private DownloadContext _dc;                // Torrent download context
        private SHA1 _sha;                          // Object to create SHA1 piece info hash
        private Task _pieceBufferWriterTask;        // Task for piece buffer writer 

        public DownloadContext Dc { get => _dc; set => _dc = value; }

        /// <summary>
        /// Creates the empty files on diskas place holders of files to be downloaded.
        /// </summary>
        private void CreateEmptyFilesOnDisk()
        {
            Log.Logger.Debug("Creating empty files as placeholders for downloading ...");

            foreach (var file in _filesToDownload)
            {
                if (!System.IO.File.Exists(file.name))
                {
                    Log.Logger.Debug($"File: {file.name}");
                    Directory.CreateDirectory(Path.GetDirectoryName(file.name));
                    using (var fs = new FileStream(file.name, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                    {
                        fs.SetLength((Int64)file.length);
                    }

                }

            }

        }

        /// <summary>
        /// Checks the hash of a torrent piece on disc to see whether it has already been downloaded.
        /// </summary>
        /// <returns><c>true</c>, if piece hash agrees (it has been downloaded), <c>false</c> otherwise.</returns>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="pieceBuffer">Piece buffer.</param>
        /// <param name="numberOfBytes">Number of bytes.</param>
        private bool CheckPieceHash(UInt32 pieceNumber, byte[] pieceBuffer, UInt32 numberOfBytes)
        {
            byte[] hash = _sha.ComputeHash(pieceBuffer, 0, (Int32)numberOfBytes);
            UInt32 pieceOffset = pieceNumber * Constants.HashLength;
            for (var byteNumber = 0; byteNumber < Constants.HashLength; byteNumber++)
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
        /// <param name="numberOfBytes">Number of bytes in piece.</param>
        private void GeneratePieceMapFromBuffer(UInt32 pieceNumber, byte[] pieceBuffer, UInt32 numberOfBytes)
        {

            bool pieceThere = CheckPieceHash(pieceNumber, pieceBuffer, numberOfBytes);
            if (pieceThere)
            {
                _dc.totalBytesDownloaded += numberOfBytes;
            }
            UInt32 blockNumber = 0;
            for (; blockNumber < numberOfBytes / Constants.BlockSize; blockNumber++)
            {
                _dc.BlockPieceLocal(pieceNumber, blockNumber, pieceThere);

            }
            if (numberOfBytes % Constants.BlockSize != 0)
            {
                _dc.BlockPieceLocal(pieceNumber, numberOfBytes / Constants.BlockSize, pieceThere);
                _dc.pieceMap[pieceNumber].lastBlockLength = numberOfBytes % Constants.BlockSize;
                _dc.BlockPieceLast(pieceNumber, (numberOfBytes / Constants.BlockSize), true);
            }
            else
            {
                _dc.pieceMap[pieceNumber].lastBlockLength = Constants.BlockSize;
                _dc.BlockPieceLast(pieceNumber, blockNumber - 1, true);
            }
        }

        /// <summary>
        /// Creates the piece map from the current disc which details the state of the pieces 
        /// within a torrentdownload. This could be whether a piece  is present on a remote peer, 
        /// has been requested or has already been downloaded/
        /// </summary>
        private void CreatePieceMap()
        {
            byte [] pieceBuffer = new byte[_dc.pieceLength];
            UInt32 pieceNumber = 0;
            UInt32 bytesInBuffer = 0;
            int bytesRead = 0;

            Log.Logger.Debug("Generate pieces downloaded map from local files ...");

            foreach (var file in _filesToDownload)
            {
                Log.Logger.Debug($"File: {file.name}");

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

            Log.Logger.Debug("Finished generating downloaded map.");

        }

        /// <summary>
        /// Task to take a queued downloaed piece and write it away to the relevant file 
        /// sections to which it belongs.
        /// </summary>
        private void PieceBufferWriter()
        {
            while(!_dc.pieceBufferWriteQueue.IsCompleted)
            {
                PieceBuffer pieceBuffer = _dc.pieceBufferWriteQueue.Take();

                if (CheckPieceHash(pieceBuffer.Number, pieceBuffer.Buffer, _dc.GetPieceLength(pieceBuffer.Number)))
                {
                    Log.Logger.Trace($"writePieceToFiles({pieceBuffer.Number})");

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
                     Log.Logger.Error($"Error: Hash for piece {pieceBuffer.Number} is invalid.");
                }

              
            }

        }

        /// <summary>
        /// Initializes a new instance of the FileDownloader class.
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
        /// FileDownloader class is reclaimed by garbage collection.
        /// </summary>
        ~FileDownloader()
        {
            _dc.pieceBufferWriteQueue.CompleteAdding();
        }

        /// <summary>
        /// Build already downloaded pieces map.
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
                Log.Logger.Debug(ex);
            }

        }

        /// <summary>
        /// Determine whether a piece has already been downloaded.
        /// </summary>
        /// <returns><c>true</c>, if piece has been downloaded, <c>false</c> otherwise.</returns>
        /// <param name="pieceNumber">Piece number.</param>
        public bool HavePiece(UInt32 pieceNumber)
        {
            try
            {
                Log.Logger.Trace($"havePiece({pieceNumber})");

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
                Log.Logger.Debug(ex);
            }

            return (true);
        }

        /// <summary>
        /// Selects the next piece to be downloaded.
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
                    Log.Logger.Trace($"selectNextPiece()");

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
                Log.Logger.Debug(ex);
            }

            return (false);

        }

    }
}
