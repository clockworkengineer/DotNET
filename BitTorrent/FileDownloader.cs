using System;
using System.IO;
using System.Security.Cryptography;

namespace BitTorrent
{
    public class FileDownloader
    {
        public struct FileRecievedMap{
            public bool[] blocks;
            public UInt32 pieceSize;
        }

        private string _fileName = String.Empty;
        private int _length = 0;
        private int _pieceLength = 0;
        private int _blocksPerPiece = 0;
        private byte[] _pieces;
        private int _numberOfPieces = 0;
        private FileRecievedMap[] _receivedMap;
        private FileRecievedMap[] _remotePeerMap;
        private int totalBytesDownloaded = 0;
        private byte[] _currentPiece;

        public int PieceLength { get => _pieceLength; set => _pieceLength = value; }
        public int Length { get => _length; set => _length = value; }
        public int BlocksPerPiece { get => _blocksPerPiece; set => _blocksPerPiece = value; }
        public int NumberOfPieces { get => _numberOfPieces; set => _numberOfPieces = value; }
        public int TotalBytesDownloaded { get => totalBytesDownloaded; set => totalBytesDownloaded = value; }
        public byte[] CurrentPiece { get => _currentPiece; set => _currentPiece = value; }
        public FileRecievedMap[] ReceivedMap { get => _receivedMap; set => _receivedMap = value; }
        public FileRecievedMap[] RemotePeerMap { get => _remotePeerMap; set => _remotePeerMap = value; }
        public string FileName { get => _fileName; set => _fileName = value; }

        private void createPieceMaps()
        {
            RemotePeerMap = new FileRecievedMap[NumberOfPieces];
            ReceivedMap = new FileRecievedMap[NumberOfPieces];

            for (int pieceNubmer = 0; pieceNubmer < NumberOfPieces; pieceNubmer++) {
                ReceivedMap[pieceNubmer].blocks = new bool[BlocksPerPiece];
                ReceivedMap[pieceNubmer].pieceSize = (UInt32) PieceLength;
                RemotePeerMap[pieceNubmer].blocks = new bool[BlocksPerPiece];
                RemotePeerMap[pieceNubmer].pieceSize = (UInt32) PieceLength;
            }

            ReceivedMap[NumberOfPieces - 1].pieceSize = (UInt32)(Length % PieceLength);
            RemotePeerMap[NumberOfPieces - 1].pieceSize = (UInt32)(Length % PieceLength);


        }

        public bool compareBytes(byte[] hash, int pieceNumber)
        {
            int pieceOffset = pieceNumber * 20;
            for (var byteNumber = 0; byteNumber < 20; byteNumber++)
            {
                if (hash[byteNumber] != _pieces[pieceOffset + byteNumber])
                {
                    return (false);
                }
            }
            return (false);

        }

        private void generateMap()
        {
            byte[] buffer = new byte[_pieceLength];

            createPieceMaps();

            using (var inFileSteam = new FileStream(FileName, FileMode.Open))
            {
                SHA1 sha = new SHA1CryptoServiceProvider();

                int pieceNumber = 0;
                int bytesRead = inFileSteam.Read(buffer, 0, buffer.Length);

                while (bytesRead > 0)
                {
                    byte[] hash = sha.ComputeHash(buffer);
                    if (compareBytes(hash, pieceNumber)) {
                        for (int block =0; block < _blocksPerPiece; block++)
                        {
                            ReceivedMap[pieceNumber].blocks[block] = true;
                        }
                    }
                    bytesRead = inFileSteam.Read(buffer, 0, buffer.Length);
                    pieceNumber++;

                }

            }

        }

        private void createFile()
        {
            System.IO.File.WriteAllBytes(FileName, new byte[Length]);
            createPieceMaps();
        }

        public FileDownloader(String name, int length, int pieceLength, byte[] pieces)
        {
            FileName = name;
            _length = length;
            _pieceLength = pieceLength;
            _pieces = pieces;;

            BlocksPerPiece = (pieceLength / 1024);
            if (pieceLength % 1024 != 0)
            {
                BlocksPerPiece++;
            }
            NumberOfPieces = (_length / _pieceLength );
            if (_length %_pieceLength != 0)
            {
                int rem = _length % _pieceLength;
                NumberOfPieces++;
            }

            CurrentPiece = new byte[pieceLength];

        }

        public void check()
        {
            if (!System.IO.File.Exists(FileName))
            {
                createFile();
            }
            else
            {
                generateMap();
            }
        }

        public bool havePiece(int pieceNumber)
        {
            for (int block=0; block < _blocksPerPiece; block++)
            {
                if (!ReceivedMap[pieceNumber].blocks[block])
                {
                    return (false);
                }
            }
            return (true);
        }
        public int selectNextPiece()
        {
            for (var pieceNumber=0; pieceNumber < NumberOfPieces; pieceNumber++)
            {
                for (var blockNumber = 0; blockNumber < BlocksPerPiece; blockNumber++)
                {
                    if (RemotePeerMap[pieceNumber].blocks[blockNumber] && !ReceivedMap[pieceNumber].blocks[blockNumber])
                    {
                        return (pieceNumber);
                    }
                }
            }
            return (-1);
        }

        public void writePieceToFile(int pieceNumber)
        {
            using (Stream stream = new FileStream(FileName, FileMode.OpenOrCreate))
            {
                stream.Seek(pieceNumber*PieceLength, SeekOrigin.Begin);
                stream.Write(_currentPiece, 0, (int) RemotePeerMap[pieceNumber].pieceSize);
                Array.Clear(CurrentPiece, 0, CurrentPiece.Length);
            }

        }
    }
}
