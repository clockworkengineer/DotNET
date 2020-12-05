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
        private Tracker _torrentTracker;             // Torrent tracker
        public TorrentContext Tc { get; set; }      // Torrent download context
        /// <summary>
        /// Update download information. This is used as the tracker callback to be invoked
        /// when the next announce response is recieved back for the torrent being downloaded.
        /// </summary>
        /// <param name="obj"></param>
        private void UpdateDownloadInformation(Object obj)
        {
            TorrentClient main = (TorrentClient)obj;
            TorrentDetails torrentDetails = main.TorrentAgent.GetTorrentDetails(main.MainAppicationWindow.Torrent.Tc);
            main.MainAppicationWindow.InfoWindow.Update(torrentDetails);
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
                main.MainAppicationWindow.UpdateDownloadProgress((float)((double)Tc.TotalBytesDownloaded / (double)Tc.TotalBytesToDownload));
            });
            if (Tc.TotalBytesToDownload - Tc.TotalBytesDownloaded == 0)
            {
                _torrentTracker.CallBack = null;
                _torrentTracker.CallBackData = null;
                _torrentTracker = null;
                Tc.CallBack = null;
                Tc.CallBackData = null;
                Tc = null;
                main.ResetWindowAndCopySeedingFile();
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
                    main.MainAppicationWindow.UpdateDownloadProgress(0);
                    main.MainAppicationWindow.InfoWindow.SetTracker(torrentFile.GetTracker());
                });
                // Create torrent context and tracker
                Tc = new TorrentContext(torrentFile, main.TorrentSelector, main.TorrentDiskIO, main.Configuration.DestinationDirectory)
                {
                    CallBack = UpdateDownloadProgress,
                    CallBackData = main
                };
                _torrentTracker = new Tracker(Tc)
                {
                    CallBack = UpdateDownloadInformation,
                    CallBackData = main
                };
                // Hookup tracker to agent, add torrent and startup everyhing up
                main.TorrentAgent.AddTorrent(Tc);
                main.TorrentAgent.AttachPeerSwarmQueue(_torrentTracker);
                _torrentTracker.StartAnnouncing();
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
