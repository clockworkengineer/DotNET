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
        private MainWindow _mainWindow;
        private double _currentProgress = 0;
        Tracker tracker;
        private ListView peerListView;
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
                    _mainWindow.torrentDownloadProgress.Fraction = (float)progress;
                });
                _mainWindow.torrentDownloadProgress.Fraction = (float)progress;
                _currentProgress = progress;
            }
            List<PeerDetails> peerList = torrent._agent.GetPeerSwarm();
            List<string> peers = new List<string>();
            foreach (var peer in peerList)
            {
                peers.Add(peer.ip + ":" + peer.port.ToString());
            }
            Application.MainLoop.Invoke(() =>
            {
                if (peerListView != null)
                {
                    _mainWindow.informationWindow.peersWindow.Remove(peerListView);
                }
                peerListView = new ListView(peers.ToArray())
                {
                    X = 0,
                    Y = 0,
                    Width = Dim.Fill(),
                    Height = Dim.Fill(),
                    CanFocus = false
                };
                _mainWindow.informationWindow.peersWindow.Add(peerListView);
            });

        }
        public void Download(MainWindow mainWindow)
        {
            try
            {
                _mainWindow = mainWindow;

                _torrentFile = new MetaInfoFile(_torrentFileName);

                _torrentFile.Load();
                _torrentFile.Parse();

                Application.MainLoop.Invoke(() =>
                              {
                                  _mainWindow.downloadButton.Text = "Working";
                                  _mainWindow.downloadButton.CanFocus = false;
                              });

                _downloader = new Downloader(_torrentFile, "/home/robt/utorrent");
                _selector = new Selector(_downloader.Dc);
                _assembler = new Assembler(_downloader, this.UpdateProgress, this);
                _agent = new Agent(_torrentFile, _downloader, _assembler);

                tracker = new Tracker(_agent, _downloader);

                tracker.StartAnnouncing();

                _agent.Start();

                _agent.Download();

                Application.MainLoop.Invoke(() =>
                                {
                                    _mainWindow.downloadButton.Text = "Download";
                                    _mainWindow.downloadButton.CanFocus = true;
                                });

                _agent.Close();

            }
            catch (Exception ex)
            {
                Application.MainLoop.Invoke(() =>
                        {
                            MessageBox.Query("Error", ex.Message, "Ok");
                        });
            }

            _mainWindow.downloadButton.DownloadingTorent = false;
        }
    }
}