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
        private MainWindow _mainWindow;
        private double _currentProgress = 0;
        private Downloader _downloader;
        private Tracker _tracker;
        public Agent DownloadAgent { get; set; }
        public TorrentContext Tc { get; set; }
/// <summary>
/// 
/// </summary>
/// <param name="obj"></param>
        private void UpdateInformation(Object obj)
        {
            Torrent torrent = (Torrent)obj;
            TorrentDetails torrentDetails = torrent.DownloadAgent.GetTorrentDetails(torrent.Tc);
            List<string> peers = new List<string>();
            foreach (var peer in torrentDetails.peers)
            {
                peers.Add(peer.ip + ":" + peer.port.ToString());
            }
            Application.MainLoop.Invoke(() =>
            {

                _mainWindow.InformationWindow.UpdatePeers(new ListView(peers.ToArray())
                {
                    X = 0,
                    Y = 0,
                    Width = Dim.Fill(),
                    Height = Dim.Fill(),
                    CanFocus = false
                });

                _mainWindow.InformationWindow.InfoTextFields[0].Text = InfoHashToString(torrentDetails.infoHash);
                _mainWindow.InformationWindow.InfoTextFields[1].Text = torrentDetails.downloadedBytes.ToString();
                _mainWindow.InformationWindow.InfoTextFields[2].Text = torrentDetails.uploadedBytes.ToString();
                _mainWindow.InformationWindow.InfoTextFields[3].Text = torrentDetails.missingPiecesCount.ToString();
                _mainWindow.InformationWindow.InfoTextFields[4].Text = torrentDetails.status.ToString();
                _mainWindow.InformationWindow.InfoTextFields[5].Text = torrentDetails.swarmSize.ToString();
                _mainWindow.InformationWindow.InfoTextFields[6].Text = torrentDetails.deadPeers.ToString();
            });
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        private void UpdateProgress(Object obj)
        {
            Torrent torrent = (Torrent)obj;
            double progress = (double)Tc.TotalBytesDownloaded /
            (double)Tc.TotalBytesToDownload;
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
        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
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
        private static string InfoHashToString(byte[] infoHash)
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

                
                lock (_mainWindow.StartupLock)
                {
                    _torrentFile = new MetaInfoFile(_torrentFileName);

                    _torrentFile.Load();
                    _torrentFile.Parse();

                    Application.MainLoop.Invoke(() =>
                                  {
                                      _mainWindow.DownloadProgress.Fraction = 0;
                                      _mainWindow.InformationWindow.TrackerText.Text = _torrentFile.MetaInfoDict["announce"];
                                  });

                    _downloader = new Downloader() {
                        CallBack = UpdateProgress,
                        CallBackData = this
                    };

                    Tc = new TorrentContext(_torrentFile, new Selector(), _downloader, "/home/robt/utorrent")
                    {
                        CallBack = DownloadComplete,
                        CallBackData = mainWindow
                    };

                    DownloadAgent = new Agent(new Assembler());
                    DownloadAgent.Add(Tc);

                    _tracker = new Tracker(Tc) {
                        CallBack = UpdateInformation,
                        CallBackData = this
                    };

                    _tracker.SetPeerSwarmQueue(DownloadAgent.PeerSwarmQueue);
                    
                    _tracker.StartAnnouncing();

                    DownloadAgent.Start(Tc);

                    DownloadAgent.Download(Tc);

                    DownloadAgent.Remove(Tc);

                    DownloadAgent.ShutDown();
                }


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
