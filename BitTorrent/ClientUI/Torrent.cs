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
using System.Linq;
namespace ClientUI
{
    /// <summary>
    /// 
    /// </summary>
    public class Torrent
    {
        private string _fileName;                   // Torrent filename
        private Tracker _tracker;                   // Torrent tracker
        private readonly Manager _manager;          // Manager for all torrents
        public readonly Selector _selector;         // Selector for all torrents
        public readonly DiskIO _diskIO;             // DiskIO for all torrents
        public Agent MainAgent { get; set; }        // Agent for handling all torrents
        public TorrentContext Tc { get; set; }      // Torrent download context
        /// <summary>
        /// Update download information. This is used as the tracker callback to be invoked
        /// when the next announce response is recieved back for the torrent being downloaded.
        /// </summary>
        /// <param name="obj"></param>
        private void UpdateDownloadInformation(Object obj)
        {
            TorrentClient main = (TorrentClient)obj;
            TorrentDetails torrentDetails = MainAgent.GetTorrentDetails(Tc);
            main.ClientWindow.InfoWindow.Update(torrentDetails);
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
                main.ClientWindow.UpdatProgressBar((float)((double)Tc.TotalBytesDownloaded / (double)Tc.TotalBytesToDownload));
            });
            if (Tc.TotalBytesToDownload - Tc.TotalBytesDownloaded == 0)
            {
                _tracker.CallBack = null;
                _tracker.CallBackData = null;
                _tracker = null;
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
        public Torrent()
        {
            _selector = new Selector();
            _manager = new Manager();
            _diskIO = new DiskIO(_manager);
            _manager.AddToDeadPeerList("192.168.1.1");
            MainAgent = new Agent(_manager, new Assembler());
            MainAgent.Startup();
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
                MetaInfoFile torrentFile = new MetaInfoFile(_fileName);
                torrentFile.Parse();
                Application.MainLoop.Invoke(() =>
                {
                    main.ClientWindow.UpdatProgressBar(0);
                    main.ClientWindow.InfoWindow.SetTracker(torrentFile.GetTracker());
                });
                // Create torrent context and tracker
                Tc = new TorrentContext(torrentFile, _selector, _diskIO, main.Configuration.DestinationDirectory)
                {
                    CallBack = UpdateDownloadProgress,
                    CallBackData = main
                };
                _tracker = new Tracker(Tc)
                {
                    CallBack = UpdateDownloadInformation,
                    CallBackData = main
                };
                // Hookup tracker to agent, add torrent and startup everyhing up
                MainAgent.AddTorrent(Tc);
                MainAgent.AttachPeerSwarmQueue(_tracker);
                _tracker.StartAnnouncing();
                MainAgent.StartTorrent(Tc);
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
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        public void SetDownloadTorrent(string fileName)
        {
            _fileName = fileName;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public List<TorrentDetails> GetSeedingTorrentDetails()
        {
            return (from torrent in MainAgent.TorrentList
                    let torrentDetails = MainAgent.GetTorrentDetails(torrent)
                    where torrentDetails.status == TorrentStatus.Seeding
                    select torrentDetails).ToList();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="config"></param>
        public void AddSeedingTorrent(string fileName, Config config)
        {
            TorrentContext tc = null;
            try
            {
                MetaInfoFile seederFile = new MetaInfoFile(fileName);
                seederFile.Parse();
                tc = new TorrentContext(seederFile, _selector, _diskIO, config.DestinationDirectory, config.SeedingMode);
                Tracker tracker = new Tracker(tc);
                MainAgent.AddTorrent(tc);
                MainAgent.AttachPeerSwarmQueue(tracker);
                tracker.StartAnnouncing();
                MainAgent.StartTorrent(tc);
            }
            catch (Exception)
            {
                if (tc != null)
                {
                    MainAgent.CloseTorrent(tc);
                    MainAgent.RemoveTorrent(tc);
                }
            }
        }
    }
}
