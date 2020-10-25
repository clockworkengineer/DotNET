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
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using BitTorrentLibrary;
using Terminal.Gui;

namespace ClientUI
{
    /// <summary>
    /// 
    /// </summary>
    public class Torrent
    {
        private readonly string _torrentFileName;
        private MetaInfoFile _torrentFile;
        private DownloadContext _dc;
        private Assembler _assembler;
        private MainWindow _mainWindow;
        private double _currentProgress = 0;
        private Tracker _tracker;
        private ListView _peerListView;
        public Agent DownloadAgent { get; set; }

        private void UpdateInformation(Object obj)
        {
            Torrent torrent = (Torrent)obj;
            TorrentDetails torrentDetails = torrent.DownloadAgent.GetTorrentDetails();
            List<string> peers = new List<string>();
            foreach (var peer in torrentDetails.peers)
            {
                peers.Add(peer.ip + ":" + peer.port.ToString());
            }
            Application.MainLoop.Invoke(() =>
            {
                if (_peerListView != null)
                {
                    _mainWindow.InformationWindow.peersWindow.Remove(_peerListView);
                }
                _peerListView = new ListView(peers.ToArray())
                {
                    X = 0,
                    Y = 0,
                    Width = Dim.Fill(),
                    Height = Dim.Fill(),
                    CanFocus = false
                };
                _mainWindow.InformationWindow.peersWindow.Add(_peerListView);
                _mainWindow.InformationWindow._infoHashText.Text = InfoHashToString(torrentDetails.infoHash);
                _mainWindow.InformationWindow._bytesDownloadedText.Text = torrentDetails.downloadedBytes.ToString();
                _mainWindow.InformationWindow._bytesUploadedText.Text = torrentDetails.uploadedBytes.ToString();
                _mainWindow.InformationWindow._missingPiecesText.Text = torrentDetails.missingPiecesCount.ToString();
                _mainWindow.InformationWindow._statusText.Text = torrentDetails.status.ToString();
            });
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        private void UpdateProgress(Object obj)
        {
            Torrent torrent = (Torrent)obj;
            double progress = (double)_dc.TotalBytesDownloaded /
            (double)_dc.TotalBytesToDownload;
            if (progress - _currentProgress > 0.05)
            {
                Application.MainLoop.Invoke(() =>
                {
                    _mainWindow.DownloadProgress.Fraction = (float)progress;
                });
                _mainWindow.DownloadProgress.Fraction = (float)progress;
                _currentProgress = progress;
            }

        }

        private void DownloadComplete(Object obj)
        {
            MainWindow mainWindow = (MainWindow)obj;

            Application.MainLoop.Invoke(() =>
                {
                    mainWindow.DownloadProgress.Fraction = 1.0F;
                });
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="torrentFileName"></param>
        public Torrent(string torrentFileName)
        {
            _torrentFileName = torrentFileName;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="infoHash"></param>
        /// <returns></returns>
        public static string InfoHashToString(byte[] infoHash)
        {
            StringBuilder hex = new StringBuilder(infoHash.Length * 2);
            foreach (byte b in infoHash)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString().ToLower();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="mainWindow"></param>
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
                                  _mainWindow.DownloadProgress.Fraction = 0;
                                  _mainWindow.InformationWindow.TrackerText.Text = _torrentFile.MetaInfoDict["announce"];
                              });

                _dc = new DownloadContext(_torrentFile, new Selector(), new Downloader(), "/home/robt/utorrent");
                _assembler = new Assembler(_dc, this.UpdateProgress, this);
                DownloadAgent = new Agent(_dc, _assembler);

                _tracker = new Tracker(DownloadAgent, _dc);

                _tracker.StartAnnouncing();

                _dc.SetDownloadCompleteCallBack(DownloadComplete, _mainWindow);
                _tracker.SetTrackerCallBack(UpdateInformation, this);

                DownloadAgent.Start();

                DownloadAgent.Download();

            }
            catch (Exception ex)
            {
                Application.MainLoop.Invoke(() =>
                        {
                            MessageBox.Query("Error", ex.Message, "Ok");
                        });
                _mainWindow.DownloadingTorrent = false;
            }

        }
    }
}
