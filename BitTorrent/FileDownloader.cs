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
        private byte[] _pieces;
        private int _receivedMapEnries = 0;
        private bool[] _receivedMap;
        private int _remotePeerMapEnries = 0;
        private bool[] _remotePeerMap;

        public bool[] ReceivedMap { get => _receivedMap; set => _receivedMap = value; }
        public int ReceivedMapEnries { get => _receivedMapEnries; set => _receivedMapEnries = value; }
        public int RemotePeerMapEnries { get => _remotePeerMapEnries; set => _remotePeerMapEnries = value; }
        public bool[] RemotePeerMap { get => _remotePeerMap; set => _remotePeerMap = value; }

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

            ReceivedMap = new bool[_pieces.Length / 20];
            RemotePeerMap = new bool[_pieces.Length / 20];

            using (var inFileSteam = new FileStream(_name, FileMode.Open))
            {
                SHA1 sha = new SHA1CryptoServiceProvider();

                int pieceNumber = 0;
                int bytesRead = inFileSteam.Read(buffer, 0, buffer.Length);

                while (bytesRead > 0)
                {
                    byte[] hash = sha.ComputeHash(buffer);
                    ReceivedMap[pieceNumber] = compareBytes(hash, pieceNumber);
                    if (ReceivedMap[pieceNumber])
                    {
                        ReceivedMapEnries++;
                    }
                    bytesRead = inFileSteam.Read(buffer, 0, buffer.Length);
                    pieceNumber++;

                }

            }

        }

        private void createFile()
        {
            System.IO.File.WriteAllBytes(_name, new byte[_length]);
            ReceivedMap = new bool[_pieces.Length / 20];
            RemotePeerMap = new bool[_pieces.Length / 20];
        }

        public FileDownloader(String name, int length, int pieceLength, byte[] pieces)
        {
            _name = name;
            _length = length;
            _pieceLength = pieceLength;
            _pieces = pieces;

            RemotePeerMap = new bool[pieceLength / 20];

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

        public int selectNextPiece()
        {
            for (var pieceNumber=0; pieceNumber < _remotePeerMap.Length; pieceNumber++)
            {
                if (_remotePeerMap[pieceNumber]&&!_receivedMap[pieceNumber])
                {
                    return (pieceNumber);
                }
            }
            return (-1);
        }
    }
}
