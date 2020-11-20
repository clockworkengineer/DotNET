//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: Utility methods.
//
// Copyright 2020.
//
using System;
using System.Text;
namespace BitTorrentLibrary
{
    // Used to hold hold and calculate an average
    public struct Average
    {
        private long _total;
        private long _totalAdds;
        public void Add(long addition)
        {
            _total += addition; _totalAdds++;
        }
        public long Get()
        {
            if (_totalAdds != 0)
            {
                return _total / _totalAdds;
            }
            return 0;
        }
    }
    internal static class Util
    {
        /// <summary>
        /// Pack UInt64 MSB first into 8 bytes buffer.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="value"></param>
        public static byte[] PackUInt64(UInt64 uInt64value)
        {
            byte[] packedUInt64 = new byte[Constants.SizeOfUInt32 * 2];
            packedUInt64[0] = (byte)(uInt64value >> 56);
            packedUInt64[1] = (byte)(uInt64value >> 48);
            packedUInt64[2] = (byte)(uInt64value >> 40);
            packedUInt64[3] = (byte)(uInt64value >> 32);
            packedUInt64[4] = (byte)(uInt64value >> 24);
            packedUInt64[5] = (byte)(uInt64value >> 16);
            packedUInt64[6] = (byte)(uInt64value >> 8);
            packedUInt64[7] = (byte)(uInt64value);
            return packedUInt64;
        }
        /// <summary>
        /// Pack UInt32 MSB first into 8 bytes buffer.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="value"></param>
        public static byte[] PackUInt32(UInt64 uInt32value)
        {
            byte[] packedUInt32 = new byte[Constants.SizeOfUInt32];
            packedUInt32[0] = (byte)(uInt32value >> 24);
            packedUInt32[1] = (byte)(uInt32value >> 16);
            packedUInt32[2] = (byte)(uInt32value >> 8);
            packedUInt32[3] = (byte)(uInt32value);
            return packedUInt32;
        }
        /// <summary>
        /// Unpack into a UInt64 MSB from an byte buffer at a given offset.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static UInt64 UnPackUInt64(byte[] buffer, UInt32 offset)
        {
            UInt64 value = ((UInt64)buffer[offset]) << 56;
            value |= ((UInt64)buffer[offset + 1]) << 48;
            value |= ((UInt64)buffer[offset + 2]) << 40;
            value |= ((UInt64)buffer[offset + 3]) << 32;
            value |= ((UInt64)buffer[offset + 4]) << 24;
            value |= ((UInt64)buffer[offset + 5]) << 16;
            value |= ((UInt64)buffer[offset + 6]) << 8;
            value |= ((UInt64)buffer[offset + 7]);
            return value;
        }
        /// <summary>
        /// Unpack into a UInt32 MSB from an byte buffer at a given offset.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static UInt32 UnPackUInt32(byte[] buffer, UInt32 offset)
        {
            UInt32 value = ((UInt32)buffer[offset]) << 24;
            value |= ((UInt32)buffer[offset + 1]) << 16;
            value |= ((UInt32)buffer[offset + 2]) << 8;
            value |= ((UInt32)buffer[offset + 3]);
            return value;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="infoHash"></param>
        /// <returns></returns>
        public static string InfoHashToString(byte[] infoHash)
        {
            StringBuilder hex = new StringBuilder(infoHash.Length * 2);
            foreach (byte b in infoHash)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString().ToLower();
        }
    }
}
