using System;
using System.IO;
using System.Security.Cryptography;

namespace BitTorrent
{
    public class FileDownloader
    {
        public struct FileRecievedMap{
            public bool[] blocks;
            public int pieceSize;
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
        public byte[] Pieces { get => _pieces; set => _pieces = value; }

        private void createPieceMaps()
        {
            RemotePeerMap = new FileRecievedMap[NumberOfPieces];
            ReceivedMap = new FileRecievedMap[NumberOfPieces];

            for (int pieceNubmer = 0; pieceNubmer < NumberOfPieces; pieceNubmer++) {
                ReceivedMap[pieceNubmer].blocks = new bool[BlocksPerPiece];
                ReceivedMap[pieceNubmer].pieceSize = PieceLength;
                RemotePeerMap[pieceNubmer].blocks = new bool[BlocksPerPiece];
                RemotePeerMap[pieceNubmer].pieceSize =  PieceLength;
            }

            ReceivedMap[NumberOfPieces - 1].pieceSize = (Length % PieceLength);
            RemotePeerMap[NumberOfPieces - 1].pieceSize = (Length % PieceLength);

        }

        public bool checkPieceHash(byte[] hash, int pieceNumber)
        {
            int pieceOffset = pieceNumber * Constants.kHashLength;
            for (var byteNumber = 0; byteNumber < Constants.kHashLength; byteNumber++)
            {
                if (hash[byteNumber] != Pieces[pieceOffset + byteNumber])
                {
                    return (false);
                }
            }
            return (true);

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
            Pieces = pieces;;

            BlocksPerPiece = (pieceLength / Constants.kBlockSize);
            if (pieceLength % Constants.kBlockSize != 0)
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
                stream.Write(_currentPiece, 0,  RemotePeerMap[pieceNumber].pieceSize);
                Array.Clear(CurrentPiece, 0, CurrentPiece.Length);
            }

        }
    }
}
