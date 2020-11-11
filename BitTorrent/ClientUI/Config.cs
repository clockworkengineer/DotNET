//
// Author: Robert Tizzard
//
// Programs: Simple console application to use BitTorrent class library.
//
// Description: Class containing all application configuration setting data
// and functionality.
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
        public string SeedFileDirectory { get; set; } = "";             // Directory containign torrent files that are seeding
        public string DestinationTorrentDirectory { get; set; } = "";   // Destination for torrents downloaded
        public string TorrentFileDirectory { get; set; } = "";          // Default path for torrent field field
        public bool SeedingMode { get; set; } = true;                   // == true dont check torrents disk inage on startup
        public bool SeedingTorrents { get; set; } = true;               // == true load seeding torren

        /// <summary>
        /// Read config settings
        /// </summary>
        public void Load()
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

    }
}
