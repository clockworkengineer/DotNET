using System;
using System.IO;
using System.Security.Cryptography;

namespace BitTorrent
{
    public class FileDownloader
    {
        private string _name = String.Empty;
        private int _length = 0;
        private int _pieceLength = 0;
        private int _blocksPerPiece = 0;
        private byte[] _pieces;
        private int _numberOfPieces = 0;
        private bool[,] _receivedMap;
        private bool[,] _remotePeerMap;
        private int totalBytesDownloaded = 0;

        public int PieceLength { get => _pieceLength; set => _pieceLength = value; }
        public int Length { get => _length; set => _length = value; }
        public int BlocksPerPiece { get => _blocksPerPiece; set => _blocksPerPiece = value; }
        public int NumberOfPieces { get => _numberOfPieces; set => _numberOfPieces = value; }
        public bool[,] ReceivedMap { get => _receivedMap; set => _receivedMap = value; }
        public bool[,] RemotePeerMap { get => _remotePeerMap; set => _remotePeerMap = value; }
        public int TotalBytesDownloaded { get => totalBytesDownloaded; set => totalBytesDownloaded = value; }

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

            RemotePeerMap = new bool[NumberOfPieces, BlocksPerPiece];
            ReceivedMap = new bool[NumberOfPieces, BlocksPerPiece];

            using (var inFileSteam = new FileStream(_name, FileMode.Open))
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
                            _receivedMap[pieceNumber, block] = true;
                        }
                    }
                    bytesRead = inFileSteam.Read(buffer, 0, buffer.Length);
                    pieceNumber++;

                }

            }

        }

        private void createFile()
        {
            System.IO.File.WriteAllBytes(_name, new byte[Length]);
            RemotePeerMap = new bool[NumberOfPieces, BlocksPerPiece];
            ReceivedMap = new bool[NumberOfPieces, BlocksPerPiece];
        }

        public FileDownloader(String name, int length, int pieceLength, byte[] pieces)
        {
            _name = name;
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

          //  RemotePeerMap = new bool[NumberOfPieces, BlocksPerPiece];

        }

        public void check()
        {
            if (!System.IO.File.Exists(_name))
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
                if (!_receivedMap[pieceNumber, block])
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
                    if (RemotePeerMap[pieceNumber, blockNumber] && !ReceivedMap[pieceNumber, blockNumber])
                    {
                        return (pieceNumber);
                    }
                }
            }
            return (-1);
        }
    }
}
