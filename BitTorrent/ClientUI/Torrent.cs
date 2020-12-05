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
        private string _fileName;                   // Currently downloading torrent filename
        private Tracker _tracker;                   // Torrent tracker
        private readonly Manager _manager;          // Torrent manager
        private readonly Selector _selector;        // Torrent selector
        private readonly DiskIO _diskIO;            // Torrent diskIO
        private readonly Agent _agent;              // Torrent agent
        private TorrentContext _tc;                 // Currently downloading torrent context
        public ICollection<TorrentContext> TorrentList => _agent.TorrentList;   // List of all managed torrents
        /// <summary>
        /// Update download information. This is used as the tracker callback to be invoked
        /// when the next announce response is recieved back for the torrent being downloaded.
        /// </summary>
        /// <param name="obj"></param>
        private void UpdateDownloadInformation(Object obj)
        {
            TorrentClient main = (TorrentClient)obj;
            TorrentDetails torrentDetails = _agent.GetTorrentDetails(_tc);
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
                main.ClientWindow.UpdatProgressBar((float)((double)_tc.TotalBytesDownloaded / (double)_tc.TotalBytesToDownload));
            });
            if (_tc.TotalBytesToDownload - _tc.TotalBytesDownloaded == 0)
            {
                _tracker.CallBack = null;
                _tracker.CallBackData = null;
                _tracker = null;
                _tc.CallBack = null;
                _tc.CallBackData = null;
                _tc = null;
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
            _agent = new Agent(_manager, new Assembler());
            _agent.Startup();
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
                _tc = new TorrentContext(torrentFile, _selector, _diskIO, main.Configuration.DestinationDirectory)
                {
                    CallBack = UpdateDownloadProgress,
                    CallBackData = main
                };
                _tracker = new Tracker(_tc)
                {
                    CallBack = UpdateDownloadInformation,
                    CallBackData = main
                };
                // Hookup tracker to agent, add torrent and startup everyhing up
                _agent.AddTorrent(_tc);
                _agent.AttachPeerSwarmQueue(_tracker);
                _tracker.StartAnnouncing();
                _agent.StartTorrent(_tc);
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
        /// Set download torrent file name
        /// </summary>
        /// <param name="fileName"></param>
        public void SetDownloadTorrent(string fileName)
        {
            _fileName = fileName;
        }
        /// <summary>
        /// Get download torrent file name
        /// </summary>
        /// <returns></returns>
        public string GetDownloadTorrent() {
            return _fileName;
        }
        /// <summary>
        /// Get details of all currently seeding torrents
        /// </summary>
        /// <returns></returns>
        public List<TorrentDetails> GetSeedingTorrentDetails()
        {
            return (from torrent in _agent.TorrentList
                    let torrentDetails = _agent.GetTorrentDetails(torrent)
                    where torrentDetails.status == TorrentStatus.Seeding
                    select torrentDetails).ToList();
        }
        /// <summary>
        /// Add seeding torrent to agent
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
                _agent.AddTorrent(tc);
                _agent.AttachPeerSwarmQueue(tracker);
                tracker.StartAnnouncing();
                _agent.StartTorrent(tc);
            }
            catch (Exception)
            {
                if (tc != null)
                {
                    _agent.CloseTorrent(tc);
                    _agent.RemoveTorrent(tc);
                }
            }
        }
        /// <summary>
        /// Shutdown torrent agent
        /// </summary>
        public void Shutdown()
        {
            _agent.Shutdown();
        }
        /// <summary>
        /// Close and remove currently downloading torrent
        /// </summary>
        public void CloseDownloadingTorrent()
        {
            _agent.CloseTorrent(_tc);
            _agent.RemoveTorrent(_tc);
        }
        /// <summary>
        /// Get a torrents detaisl from its context
        /// </summary>
        /// <param name="tc"></param>
        /// <returns></returns>
        public TorrentDetails GetTorrentDetails(TorrentContext tc) {
            return _agent.GetTorrentDetails(tc);
        }
    }
}
