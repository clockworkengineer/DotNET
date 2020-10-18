//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: The Downloader class encapsulates all code and
// data relating to the readining/writing of the local torrent files
// to determine which pieces are missing and need downloading and
// written to the correct positions. NEEDS FUNCTIONALITY TO READ PIECES
// THAT HAVE BEEN REQUESTED FROM DISK AND BUFFERED FOR REQUESTS FROM
// PEERS.
//
// Copyright 2020.
//

using System;
using System.Text;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BitTorrentLibrary
{
    /// <summary>
    /// File downloader.
    /// </summary>
    public class Downloader
    {
        private readonly List<FileDetails> _filesToDownload; // Files in torrent to be downloaded
        private readonly Task _pieceBufferWriterTask;        // Task for piece buffer writer 
        private readonly Task _pieceRequestProcessingTask;   // Task for processing piece requests from remote peers
        public DownloadContext Dc { get; set; }              // Torrent download context

        /// <summary>
        /// Read/Write piece buffers to/from torrent on disk.
        /// </summary>
        /// <param name="transferBuffer"></param>
        /// <param name="read"></param>
        private void TransferPiece(ref PieceBuffer transferBuffer, bool read)
        {
            int bytesTransferred = 0;
            UInt64 startOffset = transferBuffer.Number * Dc.PieceLength;
            UInt64 endOffset = startOffset + Dc.PieceLength;

            foreach (var file in _filesToDownload)
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
                            stream.Read(transferBuffer.Buffer, (Int32)(startTransfer % Dc.PieceLength), (Int32)(endTransfer - startTransfer));
                        }
                        else
                        {
                            stream.Write(transferBuffer.Buffer, (Int32)(startTransfer % Dc.PieceLength), (Int32)(endTransfer - startTransfer));
                        }
                        bytesTransferred += (Int32)(endTransfer - startTransfer);
                        if (bytesTransferred == Dc.PieceMap[transferBuffer.Number].pieceLength)
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
        /// Generates the piece map from buffer.
        /// </summary>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="pieceBuffer">Piece buffer.</param>
        /// <param name="numberOfBytes">Number of bytes in piece.</param>
        private void GeneratePieceMapFromBuffer(UInt32 pieceNumber, byte[] pieceBuffer, UInt32 numberOfBytes)
        {
            bool pieceThere = Dc.CheckPieceHash(pieceNumber, pieceBuffer, numberOfBytes);
            if (pieceThere)
            {
                Dc.TotalBytesDownloaded += numberOfBytes;
            }
            Dc.PieceMap[pieceNumber].pieceLength = numberOfBytes;
            Dc.MarkPieceLocal(pieceNumber, pieceThere);
        }

        /// <summary>
        /// Creates the piece map from the current disc which details the state of the pieces
        /// within a torrentdownload. This could be whether a piece  is present on a remote peer 
        /// or has already been downloaded.
        /// </summary>
        private void CreatePieceMap()
        {
            byte[] pieceBuffer = new byte[Dc.PieceLength];
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

                        if (bytesInBuffer == Dc.PieceLength)
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
        /// Read piece from torrent
        /// </summary>
        public PieceBuffer GetPieceFromTorrent(UInt32 pieceNumber)
        {

            PieceBuffer pieceBuffer = new PieceBuffer(pieceNumber, Dc.PieceMap[pieceNumber].pieceLength);

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
            while (!Dc.PieceBufferWriteQueue.IsCompleted)
            {
                PieceBuffer pieceBuffer = Dc.PieceBufferWriteQueue.Take();

                Log.Logger.Debug($"Write piece ({pieceBuffer.Number}) to file.");

                TransferPiece(ref pieceBuffer, false);

                Dc.TotalBytesDownloaded += Dc.PieceMap[pieceBuffer.Number].pieceLength;
                Log.Logger.Info((Dc.TotalBytesDownloaded / (double)Dc.TotalBytesToDownload).ToString("0.00%"));
                Log.Logger.Debug($"Piece ({pieceBuffer.Number}) written to file.");

                if (Dc.BytesLeftToDownload() == 0)
                {
                    Dc.PieceSelector.DownloadComplete();
                }

            }
        }
        /// <summary>
        /// Process any piece requests in buffer and send to remote peer.
        /// </summary>
        private void PieceRequestProcessingTask()
        {
            while (!Dc.PieceRequestQueue.IsCompleted)
            {
                PieceRequest request = Dc.PieceRequestQueue.Take();

                Log.Logger.Info($"+++Piece Reqeuest {request.pieceNumber} {request.blockOffset} {request.blockSize}.");

                PieceBuffer requestBuffer = GetPieceFromTorrent(request.pieceNumber);
                byte[] requestBlock = new byte[request.blockSize];

                Array.Copy(requestBuffer.Buffer, (Int32) request.blockOffset, requestBlock, 0, (Int32) request.blockSize);

                PWP.Piece(request.remotePeer, request.pieceNumber, request.blockOffset, requestBlock);

                Dc.TotalBytesUploaded += request.blockSize;

            }
        }

        /// <summary>
        /// Build already downloaded pieces map.
        /// </summary>
        private void BuildDownloadedPiecesMap()
        {
            try
            {
                CreateLocalTorrentStructure();
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
        /// Setup data and resources needed by downloader.
        /// </summary>
        /// <param name="filesToDownload">Files to download.</param>
        /// <param name="pieceLength">Piece length.</param>
        /// <param name="pieces">Pieces.</param>
        public Downloader(MetaInfoFile torrentMetaInfo, string downloadPath)
        {
            (var totalDownloadLength, var filesToDownload) = torrentMetaInfo.LocalFilesToDownloadList(downloadPath);
            _filesToDownload = filesToDownload;

            Dc = new DownloadContext(totalDownloadLength,
                uint.Parse(Encoding.ASCII.GetString(torrentMetaInfo.MetaInfoDict["piece length"])),
                torrentMetaInfo.MetaInfoDict["pieces"]);

            _pieceBufferWriterTask = Task.Run(() => PieceBufferDiskWriter());
            _pieceRequestProcessingTask  =  Task.Run(() => PieceRequestProcessingTask());
            
            BuildDownloadedPiecesMap();
            
        }

        /// <summary>
        /// Releases unmanaged resources and performs other cleanup operations before the
        /// downloader class is reclaimed by garbage collection.
        /// </summary>
        ~Downloader()
        {
            Dc.PieceBufferWriteQueue.CompleteAdding();
        }

    }
}
