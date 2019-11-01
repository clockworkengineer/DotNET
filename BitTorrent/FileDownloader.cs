using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace BitTorrent
{

    public class FileDownloader
    {
        public struct FileRecievedMap{
            public bool[] blocks;
            public int pieceSize;
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
        private List<FileAgent.FileDetails> _filesToDownload;

        public string FileName { get => _fileName; set => _fileName = value; }
        public ulong Length { get => _length; set => _length = value; }
        public int PieceLength { get => _pieceLength; set => _pieceLength = value; }
        public int BlocksPerPiece { get => _blocksPerPiece; set => _blocksPerPiece = value; }
        public byte[] Pieces { get => _pieces; set => _pieces = value; }
        public int NumberOfPieces { get => _numberOfPieces; set => _numberOfPieces = value; }
        public FileRecievedMap[] ReceivedMap { get => _receivedMap; set => _receivedMap = value; }
        public FileRecievedMap[] RemotePeerMap { get => _remotePeerMap; set => _remotePeerMap = value; }
        public ulong TotalBytesDownloaded { get => _totalBytesDownloaded; set => _totalBytesDownloaded = value; }
        public byte[] CurrentPiece { get => _currentPiece; set => _currentPiece = value; }
        public List<FileAgent.FileDetails> FilesToDownload { get => _filesToDownload; set => _filesToDownload = value; }

        private void createFileMapEntries(FileAgent.FileDetails fileDetails)
        {
            int lastPiece = fileDetails.startPiece + fileDetails.numberOfPieces;

            for (int pieceNubmer = fileDetails.startPiece; pieceNubmer < lastPiece; pieceNubmer++) {
                _receivedMap[pieceNubmer].blocks = new bool[_blocksPerPiece];
                _receivedMap[pieceNubmer].pieceSize = _pieceLength;
                _remotePeerMap[pieceNubmer].blocks = new bool[_blocksPerPiece];
                _remotePeerMap[pieceNubmer].pieceSize = _pieceLength;
            }

            _receivedMap[lastPiece - 1].pieceSize = (int) (fileDetails.length % ((UInt64)_pieceLength));
            _remotePeerMap[lastPiece - 1].pieceSize = (int) (fileDetails.length % ((UInt64)_pieceLength));

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

        private void fillInFilePiecesDownloaded(FileAgent.FileDetails fileDetails)
        {
            byte[] buffer = new byte[_pieceLength];

            createFileMapEntries(fileDetails);

            using (var inFileSteam = new FileStream(fileDetails.name, FileMode.Open))
            {
                SHA1 sha = new SHA1CryptoServiceProvider();

                int pieceNumber = fileDetails.startPiece;
                int bytesRead = inFileSteam.Read(buffer, 0, buffer.Length);

                while (bytesRead > 0)
                {
                    if (bytesRead < _pieceLength)
                    {
                        byte[] lastBuffer = new byte[bytesRead];
                        Buffer.BlockCopy(buffer, 0, lastBuffer, 0, lastBuffer.Length);
                        buffer = lastBuffer;
                    }
                    byte[] hash = sha.ComputeHash(buffer);
                    if (checkPieceHash(hash, pieceNumber)) {
                        for (int block =0; block < _blocksPerPiece; block++)
                        {
                            _receivedMap[pieceNumber].blocks[block] = true;
                           
                        }

                        _totalBytesDownloaded += (UInt64)_receivedMap[pieceNumber].pieceSize;
                    }
                    bytesRead = inFileSteam.Read(buffer, 0, buffer.Length);
                    pieceNumber++;

                }

            }

        }

        private void createEmptyFile(FileAgent.FileDetails fileDetails)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fileDetails.name));
            using (var fs = new FileStream(fileDetails.name, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            {
                fs.SetLength((Int64)fileDetails.length);
            }

            createFileMapEntries(fileDetails);

        }

        private FileAgent.FileDetails getFileFromPieceNumber(int pieceNumber)
        {
            foreach (var file in _filesToDownload)
            {
                if ((pieceNumber >= file.startPiece) && (pieceNumber < file.startPiece+file.numberOfPieces)) {
                    return (file);
                }
            }
            return (null);
        }

        public FileDownloader(List<FileAgent.FileDetails> filesToDownload, int pieceLength, byte[] pieces)
        {

            _filesToDownload = filesToDownload;
            _pieceLength = pieceLength;
            _pieces = pieces;

            foreach (var file in filesToDownload)
            {
                int filePieces = (int)(file.length / ((UInt64)_pieceLength));
                if (file.length % ((UInt64)_pieceLength) != 0)
                {
                    filePieces++;
                }

                _numberOfPieces += filePieces;
                _length += file.length;
            }

            _blocksPerPiece = (_pieceLength / Constants.kBlockSize);
            if (_pieceLength % Constants.kBlockSize != 0)
            {
                _blocksPerPiece++;
            }

            _currentPiece = new byte[_pieceLength];

            _remotePeerMap = new FileRecievedMap[_numberOfPieces];
            _receivedMap = new FileRecievedMap[_numberOfPieces];

        }

        public void check()
        {
            foreach (var file in _filesToDownload)
            {
                if (!System.IO.File.Exists(_fileName))
                {
                    createEmptyFile(file);
                }
                else
                {
                    fillInFilePiecesDownloaded(file);
                }
            }
        }

        public bool havePiece(int pieceNumber)
        {
            for (int block=0; block < _blocksPerPiece; block++)
            {
                if (!_receivedMap[pieceNumber].blocks[block])
                {
                    return (false);
                }
            }
            return (true);
        }
        public int selectNextPiece()
        {
            for (var pieceNumber=0; pieceNumber < _numberOfPieces; pieceNumber++)
            {
                for (var blockNumber = 0; blockNumber < _blocksPerPiece; blockNumber++)
                {
                    if (_remotePeerMap[pieceNumber].blocks[blockNumber] && !_receivedMap[pieceNumber].blocks[blockNumber])
                    {
                        return (pieceNumber);
                    }
                }
            }
            return (-1);
        }

        public void writePieceToFile(int pieceNumber)
        {
            FileAgent.FileDetails file = getFileFromPieceNumber(pieceNumber);

            using (Stream stream = new FileStream(file.name, FileMode.OpenOrCreate))
            {
                stream.Seek((pieceNumber-file.startPiece) * _pieceLength, SeekOrigin.Begin);
                stream.Write(_currentPiece, 0, _remotePeerMap[pieceNumber].pieceSize);
                Array.Clear(_currentPiece, 0, _currentPiece.Length);
            }

        }
    }
}
