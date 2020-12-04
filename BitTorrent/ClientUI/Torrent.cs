//
// Author: Rob Tizzard
//
// Programs: A simple console based torrent client.
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
        public Tracker TorrentTracker { get; set; } // Torrent tracker
        /// <summary>
        /// Unhook update callback and set tracker, torrent context to null to
        /// allow more downloads plus reset information window. The torrent is still
        /// seeding and is accessible now view the seeder list window.
        /// </summary>
        /// <param name="main"></param>
        private void RemoveDownloadingTorrentFromScreen(TorrentClient main)
        {
            TorrentTracker.CallBack = null;
            TorrentTracker.CallBackData = null;
            TorrentTracker = null;
            Tc.CallBack = null;
            Tc.CallBackData = null;
            Tc = null;
            main.MainAppicationWindow.InfoWindow.ClearData();
            main.MainStatusBar.Display(Status.Shutdown);
            main.MainAppicationWindow.DownloadProgress.Fraction = 0;
        }
        /// <summary>
        /// Update download information. This is used as the tracker callback to be invoked
        /// when the next announce response is recieved back for the torrent being downloaded.
        /// </summary>
        /// <param name="obj"></param>
        private void UpdateDownloadInformation(Object obj)
        {
            TorrentClient main = (TorrentClient)obj;
            TorrentDetails torrentDetails = main.TorrentAgent.GetTorrentDetails(main.MainAppicationWindow.Torrent.Tc);
            List<string> peers = new List<string>();
            foreach (var peer in torrentDetails.peers)
            {
                peers.Add(peer.ip + ":" + peer.port.ToString());
            }
            Application.MainLoop.Invoke(() =>
            {
                main.MainAppicationWindow.InfoWindow.UpdatePeers(peers.ToArray());
                main.MainAppicationWindow.InfoWindow.UpdateInformation(torrentDetails);
                if (torrentDetails.trackerStatus == TrackerStatus.Stalled)
                {
                    MessageBox.Query("Error", torrentDetails.trackerStatusMessage, "Ok");
                }
            });
        }
        /// <summary>
        /// Update torrent download progress bar (this is the torrent context progress callback).
        /// On completion of download copy torrent file to seeding directory and clear the main
        /// download information screen to allow a new torrent to be downloaded.
        /// </summary>
        /// <param name="obj"></param>
        private void UpdateDownloadProgress(Object obj)
        {
            TorrentClient main = (TorrentClient)obj;
            Application.MainLoop.Invoke(() =>
            {
                main.MainAppicationWindow.DownloadProgress.Fraction = (float)((double)Tc.TotalBytesDownloaded / (double)Tc.TotalBytesToDownload);
            });
            if (Tc.TotalBytesToDownload - Tc.TotalBytesDownloaded == 0)
            {
                File.Copy(main.MainAppicationWindow.Torrent.Tc.FileName, main.Configuration.SeedDirectory + 
                          Path.GetFileName(main.MainAppicationWindow.Torrent.Tc.FileName));
                RemoveDownloadingTorrentFromScreen(main);
            }
        }
        /// <summary>
        /// Initialise torrent related data and resources.
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
        public void Download(TorrentClient main)
        {
            try
            {
                // Update status bar for starting download
                Application.MainLoop.Invoke(() =>
                {
                    main.MainStatusBar.Display(Status.StartingUp);
                });
                // Load torrent file and parse
                MetaInfoFile torrentFile = new MetaInfoFile(_torrentFileName);
                torrentFile.Parse();
                Application.MainLoop.Invoke(() =>
                {
                    main.MainAppicationWindow.DownloadProgress.Fraction = 0;
                    main.MainAppicationWindow.InfoWindow.TrackerText.Text = torrentFile.GetTracker();
                });
                // Create torrent context and tracker
                Tc = new TorrentContext(torrentFile, main.TorrentSelector, main.TorrentDiskIO, main.Configuration.DestinationDirectory)
                {
                    CallBack = UpdateDownloadProgress,
                    CallBackData = main
                };
                TorrentTracker = new Tracker(Tc)
                {
                    CallBack = UpdateDownloadInformation,
                    CallBackData = main
                };
                // Hookup tracker to agent, add torrent and startup everyhing up
                main.TorrentAgent.AddTorrent(Tc);
                main.TorrentAgent.AttachPeerSwarmQueue(TorrentTracker);
                TorrentTracker.StartAnnouncing();
                main.TorrentAgent.StartTorrent(Tc);
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
