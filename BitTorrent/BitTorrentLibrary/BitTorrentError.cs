//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Class used in reporting any BitTorrent internal
// errors.
//
// Copyright 2020.
//
using System;
using System.Runtime.Serialization;
namespace BitTorrentLibrary
{
    public class BitTorrentError : Exception
    {
        public BitTorrentError()
        {
        }
        /// <summary>
        /// Create with a specific error message
        /// </summary>
        /// <param name="message">Message.</param>
        public BitTorrentError(string message) : base("BitTorrent "+message)
        {
        }
        /// <summary>
        /// Create with inner exceptions stored away.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="innerException">Inner exception.</param>
        public BitTorrentError(string message, Exception innerException) : base("BitTorrent "+message, innerException)
        {
        }
        /// <summary>
        /// BitTorrent Error class for serialization purposes.
        /// </summary>
        /// <param name="info">Info.</param>
        /// <param name="context">Context.</param>
        protected BitTorrentError(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
