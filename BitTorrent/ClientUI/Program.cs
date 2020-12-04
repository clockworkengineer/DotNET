//
// Author: Rob Tizzard
//
// Programs: A simple console based torrent client.
//
// Description: Terminal.Gui intialisation and run framework for torrent
// client application.
//
// Copyright 2020.
//
using Terminal.Gui;
namespace ClientUI
{
    class App
    {
        /// <summary>
        /// Creat application object and run it.
        /// </summary>
        static void Main(string[] _)
        {
            TorrentClient main = new TorrentClient();
            main.Run();
        }
    }
}
