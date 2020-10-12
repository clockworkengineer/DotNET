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
    public class Error : Exception
    {
        public Error()
        {
        }

        /// <summary>
        /// Create with a specific error message
        /// </summary>
        /// <param name="message">Message.</param>
        public Error(string message) : base(message)
        {
        }

        /// <summary>
        /// Create with inner exceptions stored away.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="innerException">Inner exception.</param>
        public Error(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// BitTorrent Error class for serialization purposes.
        /// </summary>
        /// <param name="info">Info.</param>
        /// <param name="context">Context.</param>
        protected Error(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
