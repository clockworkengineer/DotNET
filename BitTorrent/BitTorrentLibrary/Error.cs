//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Class to used in reporting any BitTorrent internal
// errors.
//
// Copyright 2019.
//

using System;
using System.Runtime.Serialization;

namespace BitTorrent
{
    public class Error : Exception
    {
        /// <summary>
        /// Initializes a new instance of the BitTorrent Error class.
        /// </summary>
        public Error() 
        {
        }

        /// <summary>
        /// Initializes a new instance of the BitTorrent Error class
        /// </summary>
        /// <param name="message">Message.</param>
        public Error(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the BitTorrent Error class
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="innerException">Inner exception.</param>
        public Error(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the BitTorrent Error class for serializtion purposes.
        /// </summary>
        /// <param name="info">Info.</param>
        /// <param name="context">Context.</param>
        protected Error(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
