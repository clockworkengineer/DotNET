//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Class containing reference to logger used.
//
// Copyright 2019.
//

using System;
using System.Collections.Generic;
using NLog;

namespace BitTorrentLibrary
{
    public static class Log
    {
       public static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    }
}