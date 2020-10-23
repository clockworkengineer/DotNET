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
    public class Torrent
    {
        private readonly string _torrentFileName;
        private MetaInfoFile _torrentFile;
        private Downloader _downloader;
        private Selector _selector;
        private Assembler _assembler;
        private Agent _agent;
        private DownloadButton _downloadButton;
        private double _currentProgress = 0;
        Tracker tracker;
        public Torrent(string torrentFileName)
        {
            _torrentFileName = torrentFileName;
        }

        public void UpdateProgress(Object obj)
        {
            Torrent torrent = (Torrent)obj;
            double progress = (double)_downloader.Dc.TotalBytesDownloaded /
            (double)_downloader.Dc.TotalBytesToDownload;
            if (progress - _currentProgress > 0.05)
            {
                Application.MainLoop.Invoke(() =>
                {
                    torrent._downloadButton.ProgressBar.Fraction = (float)progress;
                });
                torrent._downloadButton.ProgressBar.Fraction = (float)progress;
                _currentProgress = progress;
            }
        }
        public void Download(DownloadButton downloadButton)
        {
            try
            {
                _downloadButton = downloadButton;

                _torrentFile = new MetaInfoFile(_torrentFileName);

                _torrentFile.Load();
                _torrentFile.Parse();

                _downloadButton.FocusPrev();
                _downloadButton.Text = "Working";
                _downloadButton.CanFocus = false;

                _downloader = new Downloader(_torrentFile, "/home/robt/utorrent");
                _selector = new Selector(_downloader.Dc);
                _assembler = new Assembler(_downloader, this.UpdateProgress, this);
                _agent = new Agent(_torrentFile, _downloader, _assembler);

                tracker = new Tracker(_agent, _downloader);

                tracker.StartAnnouncing();

                _agent.Start();

                _agent.Download();

                _downloadButton.Text = "Download";
                _downloadButton.CanFocus = true;

            }
            catch (Exception ex)
            {

            }

            _downloadButton.DownloadingTorent = false;
        }
    }
}
