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
        public TorrentContext Tc { get; set; }      // Torrent download context

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

                main.MainWindow.InfoWindow.UpdatePeers(peers.ToArray());
                main.MainWindow.InfoWindow.UpdateInformation(torrentDetails);

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
                main.MainWindow.DownloadProgress.Fraction = (float)((double)Tc.TotalBytesDownloaded / (double)Tc.TotalBytesToDownload);
            });

            if (Tc.TotalBytesToDownload - Tc.TotalBytesDownloaded == 0)
            {
                File.Copy(main.MainWindow.Torrent.Tc.FileName,
                         main.Configuration.SeedDirectory + Path.GetFileName(main.MainWindow.Torrent.Tc.FileName));
            }

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

                Application.MainLoop.Invoke(() =>
                {
                    main.MainStatusBar.Display(Status.Starting);
                });

                MetaInfoFile torrentFile = new MetaInfoFile(_torrentFileName);

                torrentFile.Parse();

                Application.MainLoop.Invoke(() =>
                {
                    main.MainWindow.DownloadProgress.Fraction = 0;
                    main.MainWindow.InfoWindow.TrackerText.Text = torrentFile.MetaInfoDict["announce"];
                });

                Tc = new TorrentContext(torrentFile, new Selector(), new DiskIO()
                {
                    CallBack = UpdateDownloadProgress,
                    CallBackData = main
                }, main.Configuration.DestinationDirectory);

                main.DownloadAgent.AddTorrent(Tc);

                Tracker _tracker = new Tracker(Tc)
                {
                    CallBack = UpdateDownloadInformation,
                    CallBackData = main
                };

                main.DownloadAgent.AttachPeerSwarmQueue(_tracker);

                _tracker.StartAnnouncing();

                main.DownloadAgent.StartTorrent(Tc);

                Application.MainLoop.Invoke(() =>
                {
                    main.MainStatusBar.Display(Status.Downloading);
                });

            }
            catch (Exception ex)
            {
                Application.MainLoop.Invoke(() =>
                {
                    MessageBox.Query("Error", ex.Message, "Ok");
                    main.MainStatusBar.Display(Status.Shutdown);
                });
            }

        }
    }
}
