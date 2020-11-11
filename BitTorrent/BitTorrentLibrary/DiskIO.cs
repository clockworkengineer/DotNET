
//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: The DiskIO class encapsulates all code and
// data relating to the readining/writing of the local torrent files
// to determine which pieces are missing and need downloading and
// written to the correct positions.It also handles requests from
// remote peers for pieces and reads them the from torrent image 
// before sending on the remote peer.
//
// Copyright 2020.
//

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BitTorrentLibrary
{
    public delegate void ProgessCallBack(Object callBackData); // Download progress callback

    /// <summary>
    /// Torrent Disk IO subsystem.
    /// </summary>
    public class DiskIO
    {
        private readonly CancellationTokenSource _cancelTaskSource;    // Task cancellation source
        internal AsyncQueue<PieceBuffer> pieceWriteQueue;              // Piece buffer write queue
        internal AsyncQueue<PieceRequest> pieceRequestQueue;           // Piece request read queue

        /// <summary>
        /// Read/Write piece buffers to/from torrent on disk.
        /// </summary>
        /// <param name="tc"></param>
        /// <param name="transferBuffer"></param>
        /// <param name="read"></param>
        /// <summary>
        private void TransferPiece(TorrentContext tc, PieceBuffer transferBuffer, bool read)
        {
            int bytesTransferred = 0;
            UInt64 startOffset = transferBuffer.Number * tc.pieceLength;
            UInt64 endOffset = startOffset + tc.pieceLength;

            foreach (var file in tc.filesToDownload)
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
                            stream.Read(transferBuffer.Buffer, (Int32)(startTransfer % tc.pieceLength), (Int32)(endTransfer - startTransfer));
                        }
                        else
                        {
                            stream.Write(transferBuffer.Buffer, (Int32)(startTransfer % tc.pieceLength), (Int32)(endTransfer - startTransfer));
                        }
                        bytesTransferred += (Int32)(endTransfer - startTransfer);
                        if (bytesTransferred == tc.GetPieceLength(transferBuffer.Number))
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
        /// <param name="tc"></param>
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="pieceBuffer">Piece buffer.</param>
        /// <param name="numberOfBytes">Number of bytes in piece.</param>
        /// <summary>
        private void UpdateBitfieldFromBuffer(TorrentContext tc, UInt32 pieceNumber, byte[] pieceBuffer, UInt32 numberOfBytes)
        {
            bool pieceThere = tc.CheckPieceHash(pieceNumber, pieceBuffer, numberOfBytes);
            if (pieceThere)
            {
                tc.TotalBytesDownloaded += numberOfBytes;
            }
            tc.SetPieceLength(pieceNumber, numberOfBytes);
            tc.MarkPieceLocal(pieceNumber, pieceThere);
            if (!pieceThere)
            {
                tc.MarkPieceMissing(pieceNumber, true);
            }
        }
        /// <summary>
        /// Read piece from torrent
        /// </summary>
        /// <param name="remotePeer"></param>
        /// <param name="pieceNumber"></param>
        /// <returns></returns>
        private PieceBuffer GetPieceFromTorrent(Peer remotePeer, UInt32 pieceNumber)
        {

            PieceBuffer pieceBuffer = new PieceBuffer(remotePeer.Tc, pieceNumber, remotePeer.Tc.GetPieceLength(pieceNumber));

            Log.Logger.Debug($"Read piece ({pieceBuffer.Number}) from file.");

            TransferPiece(remotePeer.Tc, pieceBuffer, true);

            Log.Logger.Debug($"Piece ({pieceBuffer.Number}) read from file.");

            return pieceBuffer;
        }
        /// <summary>
        /// Task to take a queued download piece and write it away to the relevant file
        /// sections to which it belongs within a torrent.
        /// </summary>
        /// <param name="cancelTask"></param>
        /// <returns></returns>
        private async Task PieceBufferDiskWriterAsync(CancellationToken cancelTask)
        {
            try
            {
                Log.Logger.Info("Piece Buffer disk writer task starting...");

                while (true)
                {
                    PieceBuffer pieceBuffer = await pieceWriteQueue.DequeueAsync(cancelTask);

                    Log.Logger.Debug($"Write piece ({pieceBuffer.Number}) to file.");

                    if (!pieceBuffer.Tc.downloadFinished.WaitOne(0))
                    {
                        TransferPiece(pieceBuffer.Tc, pieceBuffer, false);

                        pieceBuffer.Tc.TotalBytesDownloaded += pieceBuffer.Tc.GetPieceLength((pieceBuffer.Number));
                        Log.Logger.Info((pieceBuffer.Tc.TotalBytesDownloaded / (double)pieceBuffer.Tc.TotalBytesToDownload).ToString("0.00%"));
                        Log.Logger.Debug($"Piece ({pieceBuffer.Number}) written to file.");

                        if (pieceBuffer.Tc.BytesLeftToDownload() == 0)
                        {
                            pieceBuffer.Tc.downloadFinished.Set();
                        }

                        // Make sure progress call back does not termiate the task.
                        try
                        {
                            pieceBuffer.Tc.CallBack?.Invoke(pieceBuffer.Tc.CallBackData);
                        }
                        catch (Exception ex)
                        {
                            Log.Logger.Debug(ex.Message);
                        }

                    }
                    else
                    {
                        Log.Logger.Debug("BitTorrent (DiskIO) Error: extra piece buffer removed after download finished.");
                    }

                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug("BitTorrent (DiskIO) Error: ", ex.Message);
            }

            Log.Logger.Info("Piece Buffer disk writer task terminated.");

        }
        /// <summary>
        /// Process any piece requests in buffer and send to remote peer.
        /// </summary>
        /// <param name="cancelTask"></param>
        /// <returns></returns>
        private async Task PieceRequestProcessingAsync(CancellationToken cancelTask)
        {
            try
            {
                Log.Logger.Info("Piece request processing task started...");

                while (true)
                {

                    PieceRequest request = await pieceRequestQueue.DequeueAsync(cancelTask);
                    try
                    {
                        Log.Logger.Info($"Piece Reqeuest {request.pieceNumber} {request.blockOffset} {request.blockSize}.");

                        PieceBuffer requestBuffer = GetPieceFromTorrent(request.remotePeer, request.pieceNumber);
                        byte[] requestBlock = new byte[request.blockSize];

                        Array.Copy(requestBuffer.Buffer, (Int32)request.blockOffset, requestBlock, 0, (Int32)request.blockSize);

                        PWP.Piece(request.remotePeer, request.pieceNumber, request.blockOffset, requestBlock);

                        request.remotePeer.Tc.TotalBytesUploaded += request.blockSize;
                    }
                    catch (Exception ex)
                    {
                        // Remote peer most probably closed socket so close connection
                        Log.Logger.Debug("BitTorrent (DiskIO) Error :", ex.Message);
                        request.remotePeer.QueueForClosure();
                    }

                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug("BitTorrent (DiskIO) Error :", ex.Message);
            }

            Log.Logger.Info("Piece request processing task terminated.");

        }
        /// <summary>
        /// Setup data and resources needed by DiskIO.
        /// </summary>
        public DiskIO()
        {
            _cancelTaskSource = new CancellationTokenSource();
            pieceWriteQueue = new AsyncQueue<PieceBuffer>();
            pieceRequestQueue = new AsyncQueue<PieceRequest>();
            CancellationToken cancelTask = _cancelTaskSource.Token;
            Task.Run(() => PieceBufferDiskWriterAsync(cancelTask));
            Task.Run(() => PieceRequestProcessingAsync(cancelTask));
        }
        /// <summary>
        /// Releases unmanaged resources and performs other cleanup operations before the
        /// DISKIO class is reclaimed by garbage collection.
        /// </summary>
        ~DiskIO()
        {
            _cancelTaskSource.Cancel();
        }
        /// <summary>
        /// Creates the empty files on disk as place holders of files to be downloaded.
        /// </summary>>
        /// <param name="tc"></param>
        internal void CreateLocalTorrentStructure(TorrentContext tc)
        {
            Log.Logger.Debug("Creating empty files as placeholders for downloading ...");

            foreach (var file in tc.filesToDownload)
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
        /// which details the state of the torrent. ie. what needs to be downloaded still.
        /// </summary>
        /// <param name="tc"></param>
        internal void CreateTorrentBitfield(TorrentContext tc)
        {
            byte[] pieceBuffer = new byte[tc.pieceLength];
            UInt32 pieceNumber = 0;
            UInt32 bytesInBuffer = 0;
            int bytesRead = 0;

            Log.Logger.Debug("Generate pieces downloaded map from local files ...");

            foreach (var file in tc.filesToDownload)
            {
                Log.Logger.Debug($"File: {file.name}");

                using (var inFileSteam = new FileStream(file.name, FileMode.Open))
                {
                    while ((bytesRead = inFileSteam.Read(pieceBuffer, (Int32)bytesInBuffer, pieceBuffer.Length - (Int32)bytesInBuffer)) > 0)
                    {
                        bytesInBuffer += (UInt32)bytesRead;

                        if (bytesInBuffer == tc.pieceLength)
                        {
                            UpdateBitfieldFromBuffer(tc, pieceNumber, pieceBuffer, bytesInBuffer);
                            bytesInBuffer = 0;
                            pieceNumber++;
                        }
                    }
                }
            }

            if (bytesInBuffer > 0)
            {
                UpdateBitfieldFromBuffer(tc, pieceNumber, pieceBuffer, bytesInBuffer);
            }

            Log.Logger.Debug("Finished generating downloaded map.");
        }
        /// <summary>
        /// Mark torrent as fully downloaded for when in seeding from startup. This
        /// means that the whole of the disk image of the torrent isn't checked so
        /// vastly inceasing start time.
        /// </summary>
        /// <param name="tc"></param>
        internal void FullyDownloadedTorrentBitfield(TorrentContext tc)
        {
            UInt64 totalBytesToDownload = tc.TotalBytesToDownload;
            for (UInt32 pieceNumber = 0; pieceNumber < tc.numberOfPieces; pieceNumber++)
            {
                tc.MarkPieceLocal(pieceNumber, true);
                tc.MarkPieceMissing(pieceNumber, false);
                if (totalBytesToDownload - Constants.BlockSize > Constants.BlockSize)
                {
                    tc.SetPieceLength(pieceNumber, Constants.BlockSize);
                }
                else
                {
                    tc.SetPieceLength(pieceNumber, (UInt32)totalBytesToDownload);
                }
                totalBytesToDownload -= Constants.BlockSize;
            }

        }
    }

}
