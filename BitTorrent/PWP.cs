using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Threading;
using System.Net.Sockets;

namespace BitTorrent
{
    public class PWP
    {
        private static readonly byte[] _protocolName = Encoding.ASCII.GetBytes("BitTorrent protocol");

        private static bool validatePeerConnect(byte[] handshakePacket, byte[] handshakeResponse, out byte[] remotePeerID)
        {

            remotePeerID = null;

            for (int byteNumber = 0; byteNumber < _protocolName.Length + 1; byteNumber++)
            {
                if (handshakePacket[byteNumber] != handshakeResponse[byteNumber])
                {
                    return (false);
                }
            }
            for (int byteNumber = _protocolName.Length + 9; byteNumber < _protocolName.Length + 29; byteNumber++)
            {
                if (handshakePacket[byteNumber] != handshakeResponse[byteNumber])
                {
                    return (false);
                }
            }

            remotePeerID = new byte[20];

            Buffer.BlockCopy(handshakeResponse, _protocolName.Length + 29, remotePeerID, 0, remotePeerID.Length);

            return (true);

        }

        public PWP()
        {

        }

        public static ValueTuple<bool, byte[]> intialHandshake(NetworkStream peerStream, byte[] infoHash)
        {
            List<byte> handshakePacket = new List<byte>();
            byte[] remotePeerID;

            handshakePacket.Add((byte) _protocolName.Length);
            handshakePacket.AddRange(_protocolName);
            handshakePacket.AddRange(new byte[8]);
            handshakePacket.AddRange(infoHash);
            handshakePacket.AddRange(Encoding.ASCII.GetBytes(PeerID.get()));

            peerStream.Write(handshakePacket.ToArray(), 0, handshakePacket.Count);

            byte[] handshakeResponse = new byte[handshakePacket.Count];

            peerStream.Read(handshakeResponse, 0, handshakeResponse.Length);

            bool connected = validatePeerConnect(handshakePacket.ToArray(), handshakeResponse, out remotePeerID);

            return (connected, remotePeerID);

        }


        public static void processRemotePeerRead(NetworkStream peerStream)
        {
            byte[] messageLength = new byte[4];
            Int32 convertedLength = 0;

            peerStream.Read(messageLength, 0, messageLength.Length);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(messageLength);

            convertedLength = BitConverter.ToInt32(messageLength, 0);

            byte[] messageBody = new byte[convertedLength];

            peerStream.Read(messageBody, 0, convertedLength);

          
            switch (messageBody[0])
            {
                case 0x0:
                    Console.WriteLine("CHOKE");
                    break;
                case 0x1:
                    Console.WriteLine("UNCHOKE");
                    break;
                case 0x2:
                    Console.WriteLine("INTERESTED");
                    break;
                case 0x3:
                    Console.WriteLine("UNINTERESTED");
                    break;
                case 0x4:
                    Console.WriteLine("HAVE");
                    break;
                case 0x5:
                    Console.WriteLine("BITFIELD");
                    break;
                case 0x6:
                    Console.WriteLine("REQUEST");
                    break;
                case 0x7:
                    Console.WriteLine("PIECE");
                    break;
                case 0x8:
                    Console.WriteLine("CANCEL");
                    break;
                default:
                    Console.WriteLine("UNKOWN");
                    break;
            }
            Thread.Sleep(1000);
        }
    }
}
