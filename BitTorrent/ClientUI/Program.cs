//
// Author: Robert Tizzard
//
// Programs: Simple console application to use BitTorrent class library.
//
// Description: Terminal.Gui intialisation and run framework.
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
            DemoTorrentApplication main = new DemoTorrentApplication();
            main.Run();
        }
    }
}
