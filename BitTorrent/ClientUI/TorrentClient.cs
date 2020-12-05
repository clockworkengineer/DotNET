//
// Author: Rob Tizzard
//
// Programs: A simple console based torrent client.
//
// Description: Top level application class.
//
// Copyright 2020.
//
using System.IO;
using System.Threading.Tasks;
using Terminal.Gui;
namespace ClientUI
{
    // Current application status
    public enum Status
    {
        StartingUp,     // Starting up a torrent download
        Downloading,    // Downloading torrent
        Shutdown        // Shutdown download ready for more
    };
    /// <summary>
    /// Torrent Demo Application.
    /// </summary>
    public class TorrentClient
    {
        public MainWindow ClientWindow { get; set; }     // Main application window 
        public MainStatusBar MainStatusBar { get; set; } // Main status bar
        public Config Configuration { get; set; }        // Appication configuration data
        // 
        /// <summary>
        /// Build application.
        /// </summary>
        public TorrentClient()
        {
            Application.Init();
            Configuration = new Config();
            Configuration.Load();
            if (!Directory.Exists(Configuration.SeedDirectory))
            {
                Directory.CreateDirectory(Configuration.SeedDirectory);
            }
            if (!Directory.Exists(Configuration.DestinationDirectory))
            {
                Directory.CreateDirectory(Configuration.DestinationDirectory);
            }
            ClientWindow = new MainWindow("BitTorrent Client Application")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            MainStatusBar = new MainStatusBar(this);
            Application.Top.Add(ClientWindow, MainStatusBar);
            ClientWindow.TorrentFileText.Text = Configuration.TorrentFileDirectory;
        }
        /// <summary>
        /// Reset main window for when downlaod compete and copy torrent file to seeding directory
        /// so that it starts seeding when the client next starts up.
        /// </summary>
        public void ResetWindowAndCopySeedingFile()
        {
            ClientWindow.InfoWindow.ClearData();
            MainStatusBar.Display(Status.Shutdown);
            ClientWindow.UpdatProgressBar(0);
            File.Copy(ClientWindow.TorrentHandler.GetDownloadTorrent(), Configuration.SeedDirectory +
                      Path.GetFileName(ClientWindow.TorrentHandler.GetDownloadTorrent()));
        }
        /// <summary>
        /// Run client.
        /// </summary>
        public void Run()
        {
            if (Configuration.SeedingTorrents)
            {
                Task.Run(() => ClientWindow.SeederListWindow.LoadSeedingTorrents(this));
            }
            Application.Run();
        }
    }
}