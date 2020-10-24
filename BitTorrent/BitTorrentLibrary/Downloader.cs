//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: The Downloader class encapsulates all code and
// data relating to the readining/writing of the local torrent files
// to determine which pieces are missing and need downloading and
// written to the correct positions.
//
// Copyright 2020.
//

using System;
using System.IO;
using System.Threading.Tasks;

namespace BitTorrentLibrary
{
    /// <summary>
    /// File downloader.
    /// </summary>
    public class Downloader
    {
        private readonly DownloadContext _dc;                 // Torrent download context
        private readonly Task _pieceBufferWriterTask;        // Task for piece buffer writer 
        private readonly Task _pieceRequestProcessingTask;   // Task for processing piece requests from remote peersxt

        /// <summary>
        /// Read/Write piece buffers to/from torrent on disk.
        /// </summary>
        /// <param name="transferBuffer"></param>
        /// <param name="read"></param>
        private void TransferPiece(ref PieceBuffer transferBuffer, bool read)
        {
            int bytesTransferred = 0;
            UInt64 startOffset = transferBuffer.Number * _dc.PieceLength;
            UInt64 endOffset = startOffset + _dc.PieceLength;

            foreach (var file in _dc.FilesToDownload)
            {
                if ((startOffset <= (file.offset + file.length)) && (file.offset <= endOffset))
                {
                    UInt64 startTransfer = Math.Max(startOffset, file.offset);
                    UInt64 endTransfer = Math.Min(endOffset, file.offset + file.length);
                    using (Stream stream = new FileStream(file.name, FileMode.Open))
                    {
                        stream.Seek((Int64)(startTransfer - file.offset), SeekOrigin.Begin);
                        if (read)
                        {
                            stream.Read(transferBuffer.Buffer, (Int32)(startTransfer % _dc.PieceLength), (Int32)(endTransfer - startTransfer));
                        }
                        else
                        {
                            stream.Write(transferBuffer.Buffer, (Int32)(startTransfer % _dc.PieceLength), (Int32)(endTransfer - startTransfer));
                        }
                        bytesTransferred += (Int32)(endTransfer - startTransfer);
                        if (bytesTransferred == _dc.PieceData[transferBuffer.Number].pieceLength)
                        {
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates the empty files on disk as place holders of files to be downloaded.
        /// </summary>
        private void CreateLocalTorrentStructure()
        {
            Log.Logger.Debug("Creating empty files as placeholders for downloading ...");

            foreach (var file in _dc.FilesToDownload)
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
        /// Update torrent piece information and bitfield from buffer.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="pieceBuffer">Piece buffer.</param>
        /// <param name="numberOfBytes">Number of bytes in piece.</param>
        private void UpdateBitfieldFromBuffer(UInt32 pieceNumber, byte[] pieceBuffer, UInt32 numberOfBytes)
        {
            bool pieceThere = _dc.CheckPieceHash(pieceNumber, pieceBuffer, numberOfBytes);
            if (pieceThere)
            {
                _dc.TotalBytesDownloaded += numberOfBytes;
            }
            _dc.PieceData[pieceNumber].pieceLength = numberOfBytes;
            _dc.MarkPieceLocal(pieceNumber, pieceThere);
            if (!pieceThere)
            {
                        _dc.MarkPieceMissing(pieceNumber, true);
            }
        }

        /// <summary>
        /// Creates the torrent bitfield and piece information structures from the current disc 
        /// which details the state of the piece.
        /// </summary>
        private void CreateTorrentBitfield()
        {
            byte[] pieceBuffer = new byte[_dc.PieceLength];
            UInt32 pieceNumber = 0;
            UInt32 bytesInBuffer = 0;
            int bytesRead = 0;

            Log.Logger.Debug("Generate pieces downloaded map from local files ...");

            foreach (var file in _dc.FilesToDownload)
            {
                Log.Logger.Debug($"File: {file.name}");

                using (var inFileSteam = new FileStream(file.name, FileMode.Open))
                {
                    while ((bytesRead = inFileSteam.Read(pieceBuffer, (Int32)bytesInBuffer, pieceBuffer.Length - (Int32)bytesInBuffer)) > 0)
                    {
                        bytesInBuffer += (UInt32)bytesRead;

                        if (bytesInBuffer == _dc.PieceLength)
                        {
                            UpdateBitfieldFromBuffer(pieceNumber, pieceBuffer, bytesInBuffer);
                            bytesInBuffer = 0;
                            pieceNumber++;
                        }
                    }
                }
            }

            if (bytesInBuffer > 0)
            {
                UpdateBitfieldFromBuffer(pieceNumber, pieceBuffer, bytesInBuffer);
            }

            Log.Logger.Debug("Finished generating downloaded map.");
        }
        /// <summary>
        /// Read piece from torrent
        /// </summary>
        private PieceBuffer GetPieceFromTorrent(Peer remotePeer, UInt32 pieceNumber)
        {

            PieceBuffer pieceBuffer = new PieceBuffer(remotePeer, pieceNumber, _dc.PieceData[pieceNumber].pieceLength);

            Log.Logger.Debug($"Read piece ({pieceBuffer.Number}) from file.");

            TransferPiece(ref pieceBuffer, true);

            Log.Logger.Debug($"Piece ({pieceBuffer.Number}) read from file.");

            return pieceBuffer;
        }


        /// <summary>
        /// Task to take a queued download piece and write it away to the relevant file
        /// sections to which it belongs within a torrent.
        /// </summary>
        private void PieceBufferDiskWriter()
        {
            while (!_dc.PieceWriteQueue.IsCompleted)
            {
                PieceBuffer pieceBuffer = _dc.PieceWriteQueue.Take();

                Log.Logger.Debug($"Write piece ({pieceBuffer.Number}) to file.");

                TransferPiece(ref pieceBuffer, false);

                _dc.TotalBytesDownloaded += _dc.PieceData[pieceBuffer.Number].pieceLength;
                Log.Logger.Info((_dc.TotalBytesDownloaded / (double)_dc.TotalBytesToDownload).ToString("0.00%"));
                Log.Logger.Debug($"Piece ({pieceBuffer.Number}) written to file.");

                if (_dc.BytesLeftToDownload() == 0)
                {
                    _dc.DownloadFinished.Set();
                }

            }
        }
        /// <summary>
        /// Process any piece requests in buffer and send to remote peer.
        /// </summary>
        private void PieceRequestProcessingTask()
        {
            while (!_dc.PieceRequestQueue.IsCompleted)
            {
                PieceRequest request = _dc.PieceRequestQueue.Take();

                Log.Logger.Info($"+++Piece Reqeuest {request.pieceNumber} {request.blockOffset} {request.blockSize}.");

                PieceBuffer requestBuffer = GetPieceFromTorrent(request.remotePeer, request.pieceNumber);
                byte[] requestBlock = new byte[request.blockSize];

                Array.Copy(requestBuffer.Buffer, (Int32)request.blockOffset, requestBlock, 0, (Int32)request.blockSize);

                PWP.Piece(request.remotePeer, request.pieceNumber, request.blockOffset, requestBlock);

                _dc.TotalBytesUploaded += request.blockSize;

            }
        }
        /// <summary>
        /// Setup data and resources needed by downloader.
        /// </summary>
        /// <param name="filesToDownload">Files to download.</param>
        /// <param name="pieceLength">Piece length.</param>
        /// <param name="pieces">Pieces.</param>
        public Downloader(DownloadContext dc)
        {
            _dc = dc;
            _pieceBufferWriterTask = Task.Run(() => PieceBufferDiskWriter());
            _pieceRequestProcessingTask = Task.Run(() => PieceRequestProcessingTask());

            CreateLocalTorrentStructure();
            CreateTorrentBitfield();

        }

        /// <summary>
        /// Releases unmanaged resources and performs other cleanup operations before the
        /// downloader class is reclaimed by garbage collection.
        /// </summary>
        ~Downloader()
        {
            _dc.PieceWriteQueue.CompleteAdding();
        }

    }
}
