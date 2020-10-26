//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: The Downloader class encapsulates all code and
// data relating to the readining/writing of the local torrent files
// to determine which pieces are missing and need downloading and
// written to the correct positions.It also handles requests from
// remote peers for pieces to be sent to them.
//
// Copyright 2020.
//

using System;
using System.IO;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace BitTorrentLibrary
{
    public interface IDownloader
    {
        BlockingCollection<PieceBuffer> PieceWriteQueue { get; set; }
        BlockingCollection<PieceRequest> PieceRequestQueue { get; set; }
        void CreateLocalTorrentStructure(DownloadContext dc);
        void CreateTorrentBitfield(DownloadContext dc);
    }

    /// <summary>
    /// File downloader.
    /// </summary>
    public class Downloader : IDownloader
    {
        public BlockingCollection<PieceBuffer> PieceWriteQueue { get; set; }
        public BlockingCollection<PieceRequest> PieceRequestQueue { get; set; }

        /// <summary>
        /// Read/Write piece buffers to/from torrent on disk.
        /// </summary>
        /// <param name="transferBuffer"></param>
        /// <param name="read"></param>
        private void TransferPiece(DownloadContext dc, PieceBuffer transferBuffer, bool read)
        {
            int bytesTransferred = 0;
            UInt64 startOffset = transferBuffer.Number * dc.PieceLength;
            UInt64 endOffset = startOffset + dc.PieceLength;

            foreach (var file in dc.FilesToDownload)
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
                            stream.Read(transferBuffer.Buffer, (Int32)(startTransfer % dc.PieceLength), (Int32)(endTransfer - startTransfer));
                        }
                        else
                        {
                            stream.Write(transferBuffer.Buffer, (Int32)(startTransfer % dc.PieceLength), (Int32)(endTransfer - startTransfer));
                        }
                        bytesTransferred += (Int32)(endTransfer - startTransfer);
                        if (bytesTransferred == dc.PieceData[transferBuffer.Number].pieceLength)
                        {
                            break;
                        }
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
        private void UpdateBitfieldFromBuffer(DownloadContext dc, UInt32 pieceNumber, byte[] pieceBuffer, UInt32 numberOfBytes)
        {
            bool pieceThere = dc.CheckPieceHash(pieceNumber, pieceBuffer, numberOfBytes);
            if (pieceThere)
            {
                dc.TotalBytesDownloaded += numberOfBytes;
            }
            dc.PieceData[pieceNumber].pieceLength = numberOfBytes;
            dc.MarkPieceLocal(pieceNumber, pieceThere);
            if (!pieceThere)
            {
                dc.MarkPieceMissing(pieceNumber, true);
            }
        }
        /// <summary>
        /// Read piece from torrent
        /// </summary>
        private PieceBuffer GetPieceFromTorrent(Peer remotePeer, UInt32 pieceNumber)
        {

            PieceBuffer pieceBuffer = new PieceBuffer(remotePeer, pieceNumber, remotePeer.Dc.PieceData[pieceNumber].pieceLength);

            Log.Logger.Debug($"Read piece ({pieceBuffer.Number}) from file.");

            TransferPiece(remotePeer.Dc, pieceBuffer, true);

            Log.Logger.Debug($"Piece ({pieceBuffer.Number}) read from file.");

            return pieceBuffer;
        }
        /// <summary>
        /// Task to take a queued download piece and write it away to the relevant file
        /// sections to which it belongs within a torrent.
        /// </summary>
        private void PieceBufferDiskWriter()
        {
            while (!PieceWriteQueue.IsCompleted)
            {
                PieceBuffer pieceBuffer = PieceWriteQueue.Take();

                Log.Logger.Debug($"Write piece ({pieceBuffer.Number}) to file.");

                TransferPiece(pieceBuffer.RemotePeer.Dc, pieceBuffer, false);

                pieceBuffer.RemotePeer.Dc.TotalBytesDownloaded += pieceBuffer.RemotePeer.Dc.PieceData[pieceBuffer.Number].pieceLength;
                Log.Logger.Info((pieceBuffer.RemotePeer.Dc.TotalBytesDownloaded / (double)pieceBuffer.RemotePeer.Dc.TotalBytesToDownload).ToString("0.00%"));
                Log.Logger.Debug($"Piece ({pieceBuffer.Number}) written to file.");

                if (pieceBuffer.RemotePeer.Dc.BytesLeftToDownload() == 0)
                {
                    pieceBuffer.RemotePeer.Dc.DownloadFinished.Set();
                    pieceBuffer.RemotePeer.Dc.DownloadCompleteCallBack?.Invoke(pieceBuffer.RemotePeer.Dc.DownloadCompleteCallBackData);
                }

            }
        }
        /// <summary>
        /// Process any piece requests in buffer and send to remote peer.
        /// </summary>
        private void PieceRequestProcessingTask()
        {
            while (!PieceRequestQueue.IsCompleted)
            {
                PieceRequest request = PieceRequestQueue.Take();

                Log.Logger.Info($"+++Piece Reqeuest {request.pieceNumber} {request.blockOffset} {request.blockSize}.");

                PieceBuffer requestBuffer = GetPieceFromTorrent(request.remotePeer, request.pieceNumber);
                byte[] requestBlock = new byte[request.blockSize];

                Array.Copy(requestBuffer.Buffer, (Int32)request.blockOffset, requestBlock, 0, (Int32)request.blockSize);

                PWP.Piece(request.remotePeer, request.pieceNumber, request.blockOffset, requestBlock);

                request.remotePeer.Dc.TotalBytesUploaded += request.blockSize;

            }
        }
        /// <summary>
        /// Setup data and resources needed by downloader.
        /// </summary>
        public Downloader()
        {
            PieceWriteQueue = new BlockingCollection<PieceBuffer>();
            PieceRequestQueue = new BlockingCollection<PieceRequest>();
            Task.Run(() => PieceBufferDiskWriter());
            Task.Run(() => PieceRequestProcessingTask());
        }
        /// <summary>
        /// Creates the empty files on disk as place holders of files to be downloaded.
        /// </summary>
        public void CreateLocalTorrentStructure(DownloadContext dc)
        {
            Log.Logger.Debug("Creating empty files as placeholders for downloading ...");

            foreach (var file in dc.FilesToDownload)
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
        /// Creates the torrent bitfield and piece information structures from the current disc 
        /// which details the state of the piece.
        /// </summary>
        public void CreateTorrentBitfield(DownloadContext dc)
        {
            byte[] pieceBuffer = new byte[dc.PieceLength];
            UInt32 pieceNumber = 0;
            UInt32 bytesInBuffer = 0;
            int bytesRead = 0;

            Log.Logger.Debug("Generate pieces downloaded map from local files ...");

            foreach (var file in dc.FilesToDownload)
            {
                Log.Logger.Debug($"File: {file.name}");

                using (var inFileSteam = new FileStream(file.name, FileMode.Open))
                {
                    while ((bytesRead = inFileSteam.Read(pieceBuffer, (Int32)bytesInBuffer, pieceBuffer.Length - (Int32)bytesInBuffer)) > 0)
                    {
                        bytesInBuffer += (UInt32)bytesRead;

                        if (bytesInBuffer == dc.PieceLength)
                        {
                            UpdateBitfieldFromBuffer(dc, pieceNumber, pieceBuffer, bytesInBuffer);
                            bytesInBuffer = 0;
                            pieceNumber++;
                        }
                    }
                }
            }

            if (bytesInBuffer > 0)
            {
                UpdateBitfieldFromBuffer(dc, pieceNumber, pieceBuffer, bytesInBuffer);
            }

            Log.Logger.Debug("Finished generating downloaded map.");
        }

        /// <summary>
        /// Releases unmanaged resources and performs other cleanup operations before the
        /// downloader class is reclaimed by garbage collection.
        /// </summary>
        ~Downloader()
        {
            PieceWriteQueue.CompleteAdding();
        }
    }
}
