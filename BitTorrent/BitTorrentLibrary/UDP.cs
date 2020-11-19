//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Simple wrapper class for the  requests that the UDP 
// tracker makes.
//
// Copyright 2020.
using System;
using System.Net;
using System.Net.Sockets;
namespace BitTorrentLibrary
{
    internal interface IUDP
    {
        void Close();
        void Connect(string URL);
        byte[] Receive();
        void Send(byte[] data);
    }
    internal class UDP : IUDP
    {
        private UdpClient _trackerConnection;   // UDP connection
        private IPEndPoint _trackerEndPoint;    // UDP endpoint
        /// <summary>
        /// Setup data and resources needed by UDP.
        /// </summary>
        public UDP()
        {
        }
        /// <summary>
        /// Perform UDP connect to tracker server.
        /// </summary>
        /// <param name="URL"></param>
        public void Connect(string URL)
        {
            _trackerConnection = new UdpClient();
            _trackerConnection.Client.ReceiveTimeout = 15000;   // 15 seconds
            Uri trackerURI = new Uri(URL);
            IPAddress[] trackerAddress = Dns.GetHostAddresses(trackerURI.Host);
            _trackerEndPoint = new IPEndPoint(trackerAddress[0], (int)trackerURI.Port);
            _trackerConnection.Connect(_trackerEndPoint);
        }
        /// <summary>
        /// Send tracker command to server.
        /// </summary>
        /// <param name="data"></param>
        public void Send(byte[] data)
        {
            _trackerConnection.Send(data, data.Length);
        }
        /// <summary>
        /// Get command response from tracker server.
        /// </summary>
        /// <returns></returns>
        public byte[] Receive()
        {
            return _trackerConnection.Receive(ref _trackerEndPoint);
        }
        /// <summary>
        /// Close connection to tracker server.
        /// </summary>
        public void Close()
        {
            _trackerConnection.Close();
        }
    }
}