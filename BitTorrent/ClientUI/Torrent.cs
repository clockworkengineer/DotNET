//
// Author: Robert Tizzard
//
// Programs: Simple console application to use BitTorrent class library.
//
// Description: Class relating to the main torrent download window and the
// associated resources/commands necessary to download the torrent.
//
// Copyright 2020.
//

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using BitTorrentLibrary;
using Terminal.Gui;

namespace ClientUI
{
    /// <summary>
    /// 
    /// </summary>
    public class Torrent
    {
        private readonly string _torrentFileName;   // Torrent filename
        private MetaInfoFile _torrentFile;          // Decoded torrent file
        private MainWindow _mainWindow;             // Main window
                                                    //    private double _currentProgress = 0;        // Value depicting the current download progress (float 0.0-1.0)
        private Tracker _tracker;                   // Tracker associated with torrent
        public TorrentContext Tc { get; set; }      // Torrent download context
        public DiskIO TorrentDiskIO { get; set; }   // Torrent DiskIO

        /// <summary>
        /// Update download information. This is used as the tracker callback to be invoked
        /// when the next announce response is recieved back.
        /// </summary>
        /// <param name="obj"></param>
        private void UpdateDownloadInformation(Object obj)
        {
            DemoTorrentApplication main = (DemoTorrentApplication)obj;
            TorrentDetails torrentDetails = main.DownloadAgent.GetTorrentDetails(main.MainWindow.Torrent.Tc);
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
                _mainWindow.InformationWindow.InfoTextFields[7].Text = torrentDetails.trackerStatus.ToString();
            });
        }
        /// <summary>
        /// Update torrent download progress bar. Thie is the diskio progress callback.
        /// </summary>
        /// <param name="obj"></param>
        private void UpdateDownloadProgress(Object obj)
        {

            DemoTorrentApplication main = (DemoTorrentApplication)obj;

            Application.MainLoop.Invoke(() =>
            {
                _mainWindow.DownloadProgress.Fraction = (float)((double)Tc.TotalBytesDownloaded / (double)Tc.TotalBytesToDownload);
            });

            if (Tc.TotalBytesToDownload - Tc.TotalBytesDownloaded == 0)
            {
                File.Copy(_mainWindow.Torrent.Tc.FileName,
                         main.SeedFileDirectory + Path.GetFileName(_mainWindow.Torrent.Tc.FileName));
            }

        }
        /// <summary>
        /// Convert torrent infohash to string.
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
        /// Initialise torrent
        /// </summary>
        /// <param name="torrentFileName"></param>
        public Torrent(string torrentFileName)
        {
            _torrentFileName = torrentFileName;
        }
        /// <summary>
        /// Initiate torrent download.
        /// </summary>
        /// <param name="mainWindow"></param>
        public void Download(DemoTorrentApplication main)
        {
            try
            {
                _mainWindow = main.MainWindow;

                Application.MainLoop.Invoke(() =>
                {
                    main.DisplayStatusBar(Status.Starting);
                });

                _torrentFile = new MetaInfoFile(_torrentFileName);

                _torrentFile.Load();
                _torrentFile.Parse();

                Application.MainLoop.Invoke(() =>
                {
                    _mainWindow.DownloadProgress.Fraction = 0;
                    _mainWindow.InformationWindow.TrackerText.Text = _torrentFile.MetaInfoDict["announce"];
                });

                TorrentDiskIO = new DiskIO()
                {
                    CallBack = UpdateDownloadProgress,
                    CallBackData = main
                };

                Tc = new TorrentContext(_torrentFile, new Selector(), TorrentDiskIO, "/home/robt/utorrent");

                main.DownloadAgent.Add(Tc);

                _tracker = new Tracker(Tc)
                {
                    CallBack = UpdateDownloadInformation,
                    CallBackData = main
                };

                main.DownloadAgent.AttachPeerSwarmQueue(_tracker);

                _ = _tracker.StartAnnouncingAsync();

                main.DownloadAgent.Start(Tc);

                Application.MainLoop.Invoke(() =>
                {
                    main.DisplayStatusBar(Status.Downloading);
                });

                _ = main.DownloadAgent.WaitForDownloadAsync(Tc);

            }
            catch (Exception ex)
            {
                Application.MainLoop.Invoke(() =>
                {
                    MessageBox.Query("Error", ex.Message, "Ok");
                    main.DisplayStatusBar(Status.Shutdown);
                });
            }

        }
    }
}
