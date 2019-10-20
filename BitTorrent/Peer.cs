using System;
namespace BitTorrent
{
    public class Peer
    {   
        private string _ip;
        private int _port;
        private bool _peerChoking=true;
        private bool _amChoking=true;

        public Peer(string ip ,int port)
        {
            _ip = ip;
            _port = port;
        }
    }
}
