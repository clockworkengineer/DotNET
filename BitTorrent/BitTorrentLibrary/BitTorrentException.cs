//
// Author: Rob Tizzard
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
    public class BitTorrentException : Exception
    {
        public BitTorrentException()
        {
        }
        /// <summary>
        /// Create with a specific error message
        /// </summary>
        /// <param name="message">Message.</param>
        public BitTorrentException(string message) : base("BitTorrent Error: "+message)
        {
        }
        /// <summary>
        /// Create with inner exceptions stored away.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="innerException">Inner exception.</param>
        public BitTorrentException(string message, Exception innerException) : base("BitTorrent Error: "+ message, innerException)
        {
        }
        /// <summary>
        /// BitTorrent Error class for serialization purposes.
        /// </summary>
        /// <param name="info">Info.</param>
        /// <param name="context">Context.</param>
        protected BitTorrentException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
