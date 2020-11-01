
//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: The Downloader class encapsulates all code and
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

    public interface IDownloader
    {
        AsyncQueue<PieceBuffer> PieceWriteQueue { get; }
        AsyncQueue<PieceRequest> PieceRequestQueue { get; }
        ProgessCallBack CallBack { get; set; }
        object CallBackData { get; set; }

        void CreateLocalTorrentStructure(TorrentContext tc);
        void CreateTorrentBitfield(TorrentContext tc);
        void FullyDownloadedTorrentBitfield(TorrentContext tc);
    }

    /// <summary>
    /// File downloader.
    /// </summary>
    public class Downloader : IDownloader
    {
        private readonly CancellationTokenSource _cancelTaskSource;    // Task cancellation source
        public AsyncQueue<PieceBuffer> PieceWriteQueue { get; }        // Piece buffer write queue
        public AsyncQueue<PieceRequest> PieceRequestQueue { get; }     // Task request read queue
        public ProgessCallBack CallBack { get; set; }                  // Download progress function
        public Object CallBackData { get; set; }                       // Download progress function data

        /// <summary>
        /// Read/Write piece buffers to/from torrent on disk.
        /// </summary>
        /// <param name="transferBuffer"></param>
        /// <param name="read"></param>
        private void TransferPiece(TorrentContext tc, PieceBuffer transferBuffer, bool read)
        {
            int bytesTransferred = 0;
            UInt64 startOffset = transferBuffer.Number * tc.PieceLength;
            UInt64 endOffset = startOffset + tc.PieceLength;

            foreach (var file in tc.FilesToDownload)
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
                            stream.Read(transferBuffer.Buffer, (Int32)(startTransfer % tc.PieceLength), (Int32)(endTransfer - startTransfer));
                        }
                        else
                        {
                            stream.Write(transferBuffer.Buffer, (Int32)(startTransfer % tc.PieceLength), (Int32)(endTransfer - startTransfer));
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
        /// <param name="pieceNumber">Piece number.</param>
        /// <param name="pieceBuffer">Piece buffer.</param>
        /// <param name="numberOfBytes">Number of bytes in piece.</param>
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
        private PieceBuffer GetPieceFromTorrent(Peer remotePeer, UInt32 pieceNumber)
        {

            PieceBuffer pieceBuffer = new PieceBuffer(remotePeer, pieceNumber, remotePeer.Tc.GetPieceLength(pieceNumber));

            Log.Logger.Debug($"Read piece ({pieceBuffer.Number}) from file.");

            TransferPiece(remotePeer.Tc, pieceBuffer, true);

            Log.Logger.Debug($"Piece ({pieceBuffer.Number}) read from file.");

            return pieceBuffer;
        }
        /// <summary>
        /// Task to take a queued download piece and write it away to the relevant file
        /// sections to which it belongs within a torrent.
        /// </summary>
        private async Task PieceBufferDiskWriterAsync(CancellationToken cancelTask)
        {
            try
            {
                while (true)
                {
                    PieceBuffer pieceBuffer = await PieceWriteQueue.DequeueAsync(cancelTask);

                    Log.Logger.Debug($"Write piece ({pieceBuffer.Number}) to file.");

                    TransferPiece(pieceBuffer.RemotePeer.Tc, pieceBuffer, false);

                    pieceBuffer.RemotePeer.Tc.TotalBytesDownloaded += pieceBuffer.RemotePeer.Tc.GetPieceLength((pieceBuffer.Number));
                    Log.Logger.Info((pieceBuffer.RemotePeer.Tc.TotalBytesDownloaded / (double)pieceBuffer.RemotePeer.Tc.TotalBytesToDownload).ToString("0.00%"));
                    Log.Logger.Debug($"Piece ({pieceBuffer.Number}) written to file.");

                    CallBack?.Invoke(CallBackData);

                    if (pieceBuffer.RemotePeer.Tc.BytesLeftToDownload() == 0)
                    {
                        pieceBuffer.RemotePeer.Tc.DownloadFinished.Set();
                        pieceBuffer.RemotePeer.Tc.CallBack?.Invoke(pieceBuffer.RemotePeer.Tc.CallBackData);
                    }

                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex.Message);
            }

        }
        /// <summary>
        /// Process any piece requests in buffer and send to remote peer.
        /// </summary>
        private async Task PieceRequestProcessingAsync(CancellationToken cancelTask)
        {
            try
            {
                while (true)
                {

                    PieceRequest request = await PieceRequestQueue.DequeueAsync(cancelTask);
                    try
                    {
                        Log.Logger.Info($"+++Piece Reqeuest {request.pieceNumber} {request.blockOffset} {request.blockSize}.");

                        PieceBuffer requestBuffer = GetPieceFromTorrent(request.remotePeer, request.pieceNumber);
                        byte[] requestBlock = new byte[request.blockSize];

                        Array.Copy(requestBuffer.Buffer, (Int32)request.blockOffset, requestBlock, 0, (Int32)request.blockSize);

                        PWP.Piece(request.remotePeer, request.pieceNumber, request.blockOffset, requestBlock);

                        request.remotePeer.Tc.TotalBytesUploaded += request.blockSize;
                    }
                    catch (Exception ex)
                    {
                        // Remote peer most probably closed socket so close connection
                        Log.Logger.Debug(ex);
                        request.remotePeer.Close();
                    }

                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex.Message);
            }



        }
        /// <summary>
        /// Setup data and resources needed by downloader.
        /// </summary>
        public Downloader()
        {
            _cancelTaskSource = new CancellationTokenSource();
            PieceWriteQueue = new AsyncQueue<PieceBuffer>();
            PieceRequestQueue = new AsyncQueue<PieceRequest>();
            CancellationToken cancelTask = _cancelTaskSource.Token;
            Task.Run(() => Task.WaitAll(PieceBufferDiskWriterAsync(cancelTask), PieceRequestProcessingAsync(cancelTask)));
        }
        /// <summary>
        /// Releases unmanaged resources and performs other cleanup operations before the
        /// downloader class is reclaimed by garbage collection.
        /// </summary>
        ~Downloader()
        {
            _cancelTaskSource.Cancel();
        }
        /// <summary>
        /// Creates the empty files on disk as place holders of files to be downloaded.
        /// </summary>
        public void CreateLocalTorrentStructure(TorrentContext tc)
        {
            Log.Logger.Debug("Creating empty files as placeholders for downloading ...");

            foreach (var file in tc.FilesToDownload)
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
        public void CreateTorrentBitfield(TorrentContext tc)
        {
            byte[] pieceBuffer = new byte[tc.PieceLength];
            UInt32 pieceNumber = 0;
            UInt32 bytesInBuffer = 0;
            int bytesRead = 0;

            Log.Logger.Debug("Generate pieces downloaded map from local files ...");

            foreach (var file in tc.FilesToDownload)
            {
                Log.Logger.Debug($"File: {file.name}");

                using (var inFileSteam = new FileStream(file.name, FileMode.Open))
                {
                    while ((bytesRead = inFileSteam.Read(pieceBuffer, (Int32)bytesInBuffer, pieceBuffer.Length - (Int32)bytesInBuffer)) > 0)
                    {
                        bytesInBuffer += (UInt32)bytesRead;

                        if (bytesInBuffer == tc.PieceLength)
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
        public void FullyDownloadedTorrentBitfield(TorrentContext tc)
        {
            UInt64 totalBytesToDownload = tc.TotalBytesToDownload;
            for (UInt32 pieceNumber = 0; pieceNumber < tc.NumberOfPieces; pieceNumber++)
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
