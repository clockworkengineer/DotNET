//
// Author: Rob Tizzard
//
// Programs: A simple console based torrent client.
//
// Description: Class containing all application configuration setting data
// and related functionality.
//
// Copyright 2020.
//
using System;
using Microsoft.Extensions.Configuration;
using BitTorrentLibrary;
namespace ClientUI
{
    public class Config
    {
        // Cofig values
        public string SeedDirectory { get; set; } = "";          // Directory containing torrent files that are seeding
        public string DestinationDirectory { get; set; } = "";   // Destination for torrents downloaded
        public string TorrentFileDirectory { get; set; } = "";   // Default path for torrent field
        public bool SeedingMode { get; set; } = true;            // == true dont check torrents disk inage on startup
        public bool SeedingTorrents { get; set; } = true;        // == true load seeding torrent
        /// <summary>
        /// Load config settings
        /// </summary>
        public void Load()
        {
            try
            {
                IConfiguration config = new ConfigurationBuilder()
                  .AddJsonFile("appsettings.json", true, true)
                  .Build();
                TorrentFileDirectory = config["TorrentFileDirectory"];
                DestinationDirectory = config["DestinationTorrentDirectory"];
                SeedDirectory = config["SeedFileDirectory"];
                SeedingMode = bool.Parse(config["SeedingMode"]);
                SeedingTorrents = bool.Parse(config["LoadSeedingTorrents"]);
            }
            catch (Exception ex)
            {
                Log.Logger.Debug("Application Error : " + ex.Message);
            }
        }
    }
}
