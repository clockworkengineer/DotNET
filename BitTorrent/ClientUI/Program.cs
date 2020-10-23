//
// Author: Robert Tizzard
//
// Programs: Simple console application to use BitTorrent class library.
//
// Description: 
//
// Copyright 2020.
//


using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BitTorrentLibrary;
using Terminal.Gui;
namespace ClientUI
{
    class Demo
    {
        private void UpdateTimer()
        {

        }
        static void Main()
        {

            Application.Init();
            var top = Application.Top;

            var mainApplicationWindow = new MainWindow("BitTorrent Demo Application")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            top.Add(mainApplicationWindow);

            Application.Run();

        }
    }
}
