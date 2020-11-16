//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Local client network host details.
//
// Copyright 2020.
//
using System;
using System.Net;
using System.Net.Sockets;
namespace BitTorrentLibrary
{
    internal static class Host
    {
        /// <summary>
        /// The default port remote peers connect for uploads.
        /// </summary>
        /// <returns></returns>
        public static int DefaultPort => (6881);
        /// <summary>
        /// Gets the local host ip.
        /// </summary>
        /// <returns>The local host ip.</returns>
        public static string GetIP()
        {
            string localHostIP = "127.0.0.1";
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    localHostIP = endPoint.Address.ToString();
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex.Message);
                throw new Error("BitTorrent (Host) Error: " + ex.Message);
            }
            return localHostIP;
        }
    }
}
