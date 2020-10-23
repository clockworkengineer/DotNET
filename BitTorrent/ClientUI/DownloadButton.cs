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
        private readonly TorrentFileNameText _torrentFileName;
        public ProgressBar ProgressBar { get; }
        public bool DownloadingTorent { get; set; } = false;

        public DownloadButton(string name, TorrentFileNameText torrentFileName, ProgressBar progressBar) : base(name)
        {
            _torrentFileName = torrentFileName;
            ProgressBar = progressBar;
        }
        public void ButtonPressed()
        {
            if (!DownloadingTorent)
            {
                _downloadTorrentTask = Task.Run(() => _torrentFileName.Torrent.Download(this));
            }
        }
    }
}