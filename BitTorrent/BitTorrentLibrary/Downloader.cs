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
using System.Text;
using System.IO;
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
        public DownloadContext Dc { get; set; }              // Torrent download context

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
            Dc.MarkPieceLocal(pieceNumber,pieceThere);
        }

        /// <summary>
        /// Creates the piece map from the current disc which details the state of the pieces
        /// within a torrentdownload. This could be whether a piece  is present on a remote peer,
        /// has been requested or has already been downloaded.
        /// </summary>
        private void CreatePieceMap()
        {
            byte [] pieceBuffer = new byte[Dc.PieceLength];
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
        /// Task to take a queued download piece and write it away to the relevant file
        /// sections to which it belongs.
        /// </summary>
        private void PieceBufferWriter()
        {
            while(!Dc.PieceBufferWriteQueue.IsCompleted)
            {
                PieceBuffer pieceBuffer = Dc.PieceBufferWriteQueue.Take();

                if (Dc.CheckPieceHash(pieceBuffer.Number, pieceBuffer.Buffer, Dc.PieceMap[pieceBuffer.Number].pieceLength))
                {
                    Log.Logger.Debug($"Write piece ({pieceBuffer.Number}) to file.");

                    UInt64 startOffset = pieceBuffer.Number * Dc.PieceLength;
                    UInt64 endOffset = startOffset + Dc.PieceLength;

                    foreach (var file in _filesToDownload)
                    {
                        if ((startOffset <= (file.offset + file.length)) && (file.offset <= endOffset))
                        {
                            UInt64 startWrite = Math.Max(startOffset, file.offset);
                            UInt64 endWrite = Math.Min(endOffset, file.offset + file.length);
                            using (Stream stream = new FileStream(file.name, FileMode.OpenOrCreate))
                            {
                                stream.Seek((Int64)(startWrite - file.offset), SeekOrigin.Begin);
                                stream.Write(pieceBuffer.Buffer, (Int32)(startWrite % Dc.PieceLength), (Int32)(endWrite - startWrite));
                                stream.Flush();
                            }
                        }
                    }

                     Log.Logger.Debug($"Piece ({pieceBuffer.Number}) written to file.");
                }
                else
                {
                     Log.Logger.Error($"BiTorrent (Downloader) Error: Hash for piece {pieceBuffer.Number} is invalid.");
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the FileDownloader class.
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
            _pieceBufferWriterTask = new Task(PieceBufferWriter);
            _pieceBufferWriterTask.Start();
        }

        /// <summary>
        /// Releases unmanaged resources and performs other cleanup operations before the
        /// FileDownloader class is reclaimed by garbage collection.
        /// </summary>
        ~Downloader()
        {
            Dc.PieceBufferWriteQueue.CompleteAdding();
        }

        /// <summary>
        /// Build already downloaded pieces map.
        /// </summary>
        public void BuildDownloadedPiecesMap()
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
 
    }
}
