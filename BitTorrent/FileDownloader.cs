using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace BitTorrent
{

    public class FileDownloader
    {
        public struct BlockData
        {
            public bool mapped;
            public int size;
        }

        public struct FileRecievedMap{
            public BlockData[] blocks;
        }

        private string _fileName = String.Empty;
        private UInt64 _length = 0;
        private int _pieceLength = 0;
        private int _blocksPerPiece = 0;
        private byte[] _pieces;
        private int _numberOfPieces = 0;
        private FileRecievedMap[] _receivedMap;
        private FileRecievedMap[] _remotePeerMap;
        private UInt64 _totalBytesDownloaded = 0;
        private byte[] _currentPiece;
        private List<FileDetails> _filesToDownload;

        public ulong Length { get => _length; set => _length = value; }
        public int BlocksPerPiece { get => _blocksPerPiece; set => _blocksPerPiece = value; }
        public int NumberOfPieces { get => _numberOfPieces; set => _numberOfPieces = value; }
        public FileRecievedMap[] ReceivedMap { get => _receivedMap; set => _receivedMap = value; }
        public FileRecievedMap[] RemotePeerMap { get => _remotePeerMap; set => _remotePeerMap = value; }
        public ulong TotalBytesDownloaded { get => _totalBytesDownloaded; set => _totalBytesDownloaded = value; }
     
        private void createEmptyFilesOnDisk()
        {
            foreach (var file in _filesToDownload)
            {
                if (!System.IO.File.Exists(file.name))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file.name));
                    using (var fs = new FileStream(file.name, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                    {
                        fs.SetLength((Int64)file.length);
                    }

                }

            }

        }

        public bool checkPieceHash(byte[] hash, int pieceNumber)
        {
            int pieceOffset = pieceNumber * Constants.kHashLength;
            for (var byteNumber = 0; byteNumber < Constants.kHashLength; byteNumber++)
            {
                if (hash[byteNumber] != _pieces[pieceOffset + byteNumber])
                {
                    return (false);
                }
            }
            return (true);

        }

        private void createReceivedMap()
        {
            SHA1 sha = new SHA1CryptoServiceProvider();
            List<byte> pieceBuffer = new List<byte>();
            int pieceNumber = 0;

            foreach (var file in _filesToDownload)
            {
                using (var inFileSteam = new FileStream(file.name, FileMode.Open))
                {
                    int bytesRead = inFileSteam.Read(_currentPiece, 0, _currentPiece.Length - pieceBuffer.Count);

                    while (bytesRead > 0)
                    {
                        for (var byteNumber = 0; byteNumber < bytesRead; byteNumber++)
                        {
                            pieceBuffer.Add(_currentPiece[byteNumber]);
                        }

                        if (pieceBuffer.Count == _pieceLength)
                        {
                            byte[] hash = sha.ComputeHash(pieceBuffer.ToArray());
                            bool pieceThere = checkPieceHash(hash, pieceNumber);
                            if (pieceThere)
                            {
                                _totalBytesDownloaded += (UInt64) _pieceLength;
                            }
                            for (int block = 0; block < _blocksPerPiece; block++)
                            {
                                _receivedMap[pieceNumber].blocks[block].mapped = pieceThere;
                                _receivedMap[pieceNumber].blocks[block].size = Constants.kBlockSize;
                                _remotePeerMap[pieceNumber].blocks[block].mapped = false;
                                _remotePeerMap[pieceNumber].blocks[block].size = Constants.kBlockSize;

                            }
                            pieceBuffer.Clear();
                            pieceNumber++;
                        }
                        bytesRead = inFileSteam.Read(_currentPiece, 0, _currentPiece.Length - pieceBuffer.Count);

                    }

                }

            }

            if (pieceBuffer.Count > 0)
            {
                byte[] hash = sha.ComputeHash(pieceBuffer.ToArray());
                bool pieceThere = checkPieceHash(hash, pieceNumber);
                if (pieceThere)
                {
                    _totalBytesDownloaded += (UInt64)pieceBuffer.Count;
                }
                for (int block = 0; block < pieceBuffer.Count/Constants.kBlockSize; block++)
                {
                    _receivedMap[pieceNumber].blocks[block].mapped = pieceThere;
                    _receivedMap[pieceNumber].blocks[block].size = Constants.kBlockSize;
                    _remotePeerMap[pieceNumber].blocks[block].mapped = false;
                    _remotePeerMap[pieceNumber].blocks[block].size = Constants.kBlockSize;

                }
                if (pieceBuffer.Count % Constants.kBlockSize != 0)
                {
                    _receivedMap[pieceNumber].blocks[(pieceBuffer.Count / Constants.kBlockSize)].mapped = pieceThere;
                    _receivedMap[pieceNumber].blocks[(pieceBuffer.Count / Constants.kBlockSize)].size = pieceBuffer.Count % Constants.kBlockSize;
                    _remotePeerMap[pieceNumber].blocks[(pieceBuffer.Count / Constants.kBlockSize)].mapped = false;
                    _remotePeerMap[pieceNumber].blocks[(pieceBuffer.Count / Constants.kBlockSize)].size = pieceBuffer.Count % Constants.kBlockSize;
                }
            }
        }

        public void writePieceToFile(FileDetails file, UInt64 startOffset, UInt64 length)
        {

            using (Stream stream = new FileStream(file.name, FileMode.OpenOrCreate))
            {
                stream.Seek((int)(startOffset - file.offset), SeekOrigin.Begin);
                stream.Write(_currentPiece, (int)(file.offset - ((file.offset / (UInt64)_pieceLength) * (UInt64)_pieceLength)), (int)length);;
            }

        }

        public FileDownloader(List<FileDetails> filesToDownload, int pieceLength, byte[] pieces)
        {

            _filesToDownload = filesToDownload;
            _pieceLength = pieceLength;
            _pieces = pieces;
            _numberOfPieces = _pieces.Length / Constants.kHashLength;
            _blocksPerPiece = _pieceLength / Constants.kBlockSize;

            foreach (var file in filesToDownload)
            {
                _length += file.length;
            }

            _currentPiece = new byte[_pieceLength];
            _remotePeerMap = new FileRecievedMap[_numberOfPieces];
            _receivedMap = new FileRecievedMap[_numberOfPieces];

            for (var pieceNuber=0; pieceNuber < _numberOfPieces; pieceNuber++)
            {
                _receivedMap[pieceNuber].blocks = new BlockData[_blocksPerPiece];
                _remotePeerMap[pieceNuber].blocks = new BlockData[_blocksPerPiece];
            }

        }

        public void buildDownloadedPiecesMap()
        {

            createEmptyFilesOnDisk();
            createReceivedMap();

        }

        public bool havePiece(int pieceNumber)
        {
            for (int block=0; block < _blocksPerPiece; block++)
            {
                if (!_receivedMap[pieceNumber].blocks[block].mapped)
                {
                    return (false);
                }
            }
            return (true);
        }

        public int selectNextPiece()
        {

            for (var pieceNumber = 0; pieceNumber < _numberOfPieces; pieceNumber++)
            {
                for (var blockNumber = 0; blockNumber < _blocksPerPiece; blockNumber++)
                {
                    if (_remotePeerMap[pieceNumber].blocks[blockNumber].mapped && !_receivedMap[pieceNumber].blocks[blockNumber].mapped)
                    {
                        return (pieceNumber);
                    }
                }
            }
            return (-1);

        }
     
        public void placeBlockIntoPiece (byte[] buffer, int offset, int blockOffset, int length)
        {
            Buffer.BlockCopy(buffer, 9, _currentPiece, blockOffset, length);
        }

        public void writePieceToFiles(int pieceNumber)
        {

            UInt64 startOffset = (UInt64) (pieceNumber * _pieceLength);
            UInt64 endOffset = startOffset+ (UInt64) _pieceLength;

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
    }
}
