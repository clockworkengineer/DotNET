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

    public class DownloadButton : Button
    {

        private Task _downloadTorrentTask;
        private MainWindow _mainWindow;
        public bool DownloadingTorent { get; set; } = false;

        public DownloadButton(string name, MainWindow mainWindow) : base(name)
        {
            _mainWindow = mainWindow;
        }
        public void ButtonPressed()
        {
            if (!DownloadingTorent)
            {
                _downloadTorrentTask = Task.Run(() => _mainWindow.torrentFileText.Torrent.Download(_mainWindow));
            }
        }
    }
}