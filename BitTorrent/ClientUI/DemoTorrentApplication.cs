//
// Author: Robert Tizzard
//
// Programs: Simple console application to use BitTorrent class library.
//
// Description: Top level application object.
//
// Copyright 2020.
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Terminal.Gui;
using System.IO;
using BitTorrentLibrary;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace ClientUI
{
    // Current application status
    public enum Status
    {
        Starting,
        Downloading,
        Shutdown
    };

    /// <summary>
    /// Torrent Demo Application.
    /// </summary>
    public class DemoTorrentApplication
    {
        public bool InformationWindow { get; set; } = true;   // == true information window displayed
        public MainApplicationWindow MainWindow { get; set; } // Main application 
        public Manager TorrentManager { get; set; }           // Torrent context manager
        public Agent DownloadAgent { get; set; }              // Agent for handling all torrents
        public MainStatusBar MainStatusBar { get; set; }      // Mains status bar
        public Toplevel Top { get; set; }                     // Top level application view

        // Cofig values
        public string SeedFileDirectory { get; set; } = "";             // Directory containign torrent files that are seeding
        public string DestinationTorrentDirectory { get; set; } = "";   // Destination for torrents downloaded
        public string TorrentFileDirectory { get; set; } = "";          // Default path for torrent field field
        public bool SeedingMode { get; set; } = true;                   // == true dont check torrents disk inage on startup
        public bool SeedingTorrents { get; set; } = true;               // == true load seeding torrents

        /// <summary>
        /// Read config settings
        /// </summary>
        private void ReadConfig()
        {
            try
            {
                IConfiguration config = new ConfigurationBuilder()
                  .AddJsonFile("appsettings.json", true, true)
                  .Build();

                TorrentFileDirectory = config["TorrentFileDirectory"];
                DestinationTorrentDirectory = config["DestinationTorrentDirectory"];
                SeedFileDirectory = config["SeedFileDirectory"];
                SeedingMode = bool.Parse(config["SeedingMode"]);
                SeedingTorrents = bool.Parse(config["LoadSeedingTorrents"]);

            }
            catch (Exception ex)
            {
                Log.Logger.Debug("Application Error : " + ex.Message);
            }

        }
        /// <summary>
        /// Build and run application.
        /// </summary>
        public DemoTorrentApplication()
        {

            Application.Init();
            Top = Application.Top;

            ReadConfig();

            MainWindow = new MainApplicationWindow(this, "BitTorrent Demo Application")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            MainStatusBar = new MainStatusBar(this);

            Top.Add(MainWindow, MainStatusBar);

            TorrentManager = new Manager();

            TorrentManager.AddToDeadPeerList("192.168.1.1");

            DownloadAgent = new Agent(TorrentManager, new Assembler());

            DownloadAgent.Startup();

            if (SeedingTorrents)
            {
                Task.Run(() => MainWindow.SeederListWindow.LoadSeedingTorrents());
            }

            MainWindow.TorrentFileText.Text = TorrentFileDirectory;

        }
        public void Run()
        {
            Application.Init();
            Application.Run();
        }
    }
}