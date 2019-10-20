using System;
using System.IO;
using System.Security.Cryptography;

namespace BitTorrent
{
    public class File
    {
        private string _name = String.Empty;
        private int _length = 0;
        private int _pieceLength = 0;
        private byte[] _pieces;
        private bool[] _receivedMap;

        public bool compareBytes(byte[] hash, int pieceNumber)
        {
            int pieceOffset = pieceNumber * 20;
            for (var byteNumber = 0; byteNumber < 20; byteNumber++)
            {
                if (hash[byteNumber]!=_pieces[pieceOffset+byteNumber])
                {
                    return (false);
                }
            }
            return (false);

        }

        private void generateMap()
        {
            byte[] buffer = new byte[_pieceLength];

            _receivedMap = new bool[_pieces.Length / 20];

            using (var inFileSteam = new FileStream(_name, FileMode.Open))
            {
                SHA1 sha = new SHA1CryptoServiceProvider();

                int pieceNumber = 0; 
                int bytesRead = inFileSteam.Read(buffer, 0, buffer.Length);

                while (bytesRead > 0)
                {
                    byte[] hash = sha.ComputeHash(buffer);
                    _receivedMap[pieceNumber] = compareBytes(hash, pieceNumber);
                    bytesRead = inFileSteam.Read(buffer, 0, buffer.Length);
                    pieceNumber++;
                }

            }

        }

        private void createFile()
        {
            System.IO.File.WriteAllBytes(_name, new byte[_length]);
            _receivedMap = new bool[_pieces.Length / 20];
        }

        public File(String name, int length, int pieceLength, byte[] pieces)
        {
            _name = name;
            _length = length;
            _pieceLength = pieceLength;
            _pieces = pieces;
   

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
    }
}
