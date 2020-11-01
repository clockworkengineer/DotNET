//
// Author: Robert Tizzard
//
// Programs: Simple console application to use BitTorrent class library.
//
// Description: Terminal.Gui intialise and run framework.
//
// Copyright 2020.
//

using Terminal.Gui;

namespace ClientUI
{
    class App {

        /// <summary>
        /// Intialise and run Terminal.Gui appication.
        /// </summary>
        private static void Run()
        {
            Application.Init();
            Application.Run();
        }

        /// <summary>
        /// Creat application object and run it.
        /// </summary>
        static void Main()
        {
            DemoTorrentApplication main = new DemoTorrentApplication();
            App.Run();
        }
    }
}
