//
// Author: Robert Tizzard
//
// Programs: Simple console application to use BitTorrent class library.
//
// Description: 
//
// Copyright 2020.
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BitTorrentLibrary;
using Terminal.Gui;
namespace ClientUI
{
    /// <summary>
    /// 
    /// </summary>
    public class TorrentFileNameText : TextField
    {
        public Torrent Torrent { get; set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="view"></param>
        /// <returns></returns>
        public override bool OnLeave(View view)
        {
            Torrent = new Torrent(Text.ToString());

            return base.OnEnter(view);
        }

    }

}
