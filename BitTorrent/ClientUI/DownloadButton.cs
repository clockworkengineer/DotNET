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
    /// <summary>
    /// 
    /// </summary>
    public class DownloadButton : Button
    {

        private Task _downloadTorrentTask;
        private readonly MainWindow _mainWindow;
        public bool DownloadingTorent { get; set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="mainWindow"></param>
        /// <returns></returns>
        public DownloadButton(string name, MainWindow mainWindow) : base(name)
        {
            _mainWindow = mainWindow;
        }
        /// <summary>
        /// 
        /// </summary>
        public void ButtonPressed()
        {
            if (!DownloadingTorent)
            {
                _downloadTorrentTask = Task.Run(() => _mainWindow.TorrentFileText.Torrent.Download(_mainWindow));
            }
        }
    }
}