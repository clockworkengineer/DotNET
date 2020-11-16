//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: BitTorrent Bencoding encoding/decoding support that reads
// torrent file data to be processed.
// 
// TODO: Conatins a fair bit of duplication that can be refactored at somme point.
//
// Copyright 2020.
//
using System;
using System.Collections.Generic;
using System.Text;
namespace BitTorrentLibrary
{
    /// <summary>
    /// Base for BNode Tree.
    /// </summary>
    internal class BNodeBase
    {
    }
    /// <summary>
    /// Dictionary BNode.
    /// </summary>
    internal class BNodeDictionary : BNodeBase
    {
        public Dictionary<string, BNodeBase> dict;
        public BNodeDictionary()
        {
            dict = new Dictionary<string, BNodeBase>();
        }
    }
    /// <summary>
    /// List BNode.
    /// </summary>
    internal class BNodeList : BNodeBase
    {
        public List<BNodeBase> list;
        public BNodeList()
        {
            list = new List<BNodeBase>();
        }
    }
    /// <summary>
    /// Number BNode.
    /// </summary>
    internal class BNodeNumber : BNodeBase
    {
        public byte[] number;
    }
    /// <summary>
    /// String BNode.
    /// </summary>
    internal class BNodeString : BNodeBase
    {
        public byte[] str;
    }
    /// <summary>
    /// Support methods for Bencoding.
    /// </summary>
    internal static class Bencoding
    {
        /// <summary>
        /// Extracts a Bencoded string and returns its bytes.
        /// </summary>
        /// <returns>The bytes for the string.</returns>
        /// <param name="buffer">buffer - Bencoded string input buffer.</param>
        /// <param name="position">position - Position in buffer. Updated to character after string on exit.</param>
        static private byte[] ExtractString(byte[] buffer, ref int position)
        {
            int end = position;
            while (buffer[end] != ':')
            {
                end++;
            }
            byte[] lengthBytes = new byte[end - position];
            Buffer.BlockCopy(buffer, position, lengthBytes, 0, end - position);
            int lengthOfString = int.Parse(Encoding.ASCII.GetString(lengthBytes));
            if (end + lengthOfString > buffer.Length)
            {
                lengthOfString = buffer.Length - end - 1;
            }
            position = end + lengthOfString + 1;
            byte[] retBuffer = new byte[lengthOfString];
            Buffer.BlockCopy(buffer, end + 1, retBuffer, 0, lengthOfString);
            return retBuffer;
        }
        /// <summary>
        /// Extracts a Bencoded number and returns its bytes.
        /// </summary>
        /// <returns>The bytes(characters) for the number.</returns>
        /// <param name="buffer">buffer - Bencoded number input buffer.</param>
        /// <param name="position">position - Position in buffer. Updated to character after number on exit.</param>
        static private BNodeBase DecodeNumber(byte[] buffer, ref int position)
        {
            BNodeNumber bNode = new BNodeNumber();
            position++;
            int end = position;
            while (buffer[end] != (byte)'e')
            {
                end++;
            }
            bNode.number = new byte[end - position];
            Buffer.BlockCopy(buffer, position, bNode.number, 0, end - position);
            position = end + 1;
            return bNode;
        }
        /// <summary>
        /// Creates a BNode string node.
        /// </summary>
        /// <returns>A string BNode.</returns>
        /// <param name="buffer">buffer - Bencoded string input buffer.</param>
        /// <param name="position">position - Position in buffer. Updated to character after string on exit.</param>
        static private BNodeBase DecodeString(byte[] buffer, ref int position)
        {
            BNodeString bNode = new BNodeString
            {
                str = ExtractString(buffer, ref position)
            };
            return bNode;
        }
        /// <summary>
        /// Decodes a dictionary string key.
        /// </summary>
        /// <returns>String value of the key.</returns>
        /// <param name="buffer">buffer - Bencoded string input buffer.</param>
        /// <param name="position">position - Position in buffer. Updated to character after string on exit.</param>
        static private string DecodeKey(byte[] buffer, ref int position)
        {
            string key = Encoding.ASCII.GetString(ExtractString(buffer, ref position));
            return key;
        }
        /// <summary>
        /// Create a list BNode.
        /// </summary>
        /// <returns>A list BNode.</returns>
        /// <param name="buffer">buffer - Bencoded list input buffer.</param>
        /// <param name="position">position - Position in buffer. Updated to character after list on exit.</param>
        static private BNodeBase DecodeList(byte[] buffer, ref int position)
        {
            BNodeList bNode = new BNodeList();
            position++;
            while (buffer[position] != (byte)'e')
            {
                switch ((char)buffer[position])
                {
                    case 'd':
                        bNode.list.Add(DecodeDictionary(buffer, ref position));
                        break;
                    case 'l':
                        bNode.list.Add(DecodeList(buffer, ref position));
                        break;
                    case 'i':
                        bNode.list.Add(DecodeNumber(buffer, ref position));
                        break;
                    default:
                        bNode.list.Add(DecodeString(buffer, ref position));
                        break;
                }
                if (position == buffer.Length) break;
            }
            position++;
            return bNode;
        }
        /// <summary>
        /// Create a dictionary BNode.
        /// </summary>
        /// <returns>A dictionary BNode.</returns>
        /// <param name="buffer">buffer - Bencoded dictionary input buffer.</param>
        /// <param name="position">position - Position in buffer. Updated to character after dictionary on exit.</param>
        static private BNodeBase DecodeDictionary(byte[] buffer, ref int position)
        {
            BNodeDictionary bNode = new BNodeDictionary();
            position++;
            while (buffer[position] != (byte)'e')
            {
                string key = DecodeKey(buffer, ref position);
                switch ((char)buffer[position])
                {
                    case 'd':
                        bNode.dict[key] = DecodeDictionary(buffer, ref position);
                        break;
                    case 'l':
                        bNode.dict[key] = DecodeList(buffer, ref position);
                        break;
                    case 'i':
                        bNode.dict[key] = DecodeNumber(buffer, ref position);
                        break;
                    default:
                        bNode.dict[key] = DecodeString(buffer, ref position);
                        break;
                }
                if (position == buffer.Length) break;
            }
            position++;
            return bNode;
        }
        /// <summary>
        /// Recursively parse a Bencoded string and return its BNode tree.
        /// </summary>
        /// <returns>Root of Bnode tree.</returns>
        /// <param name="buffer">buffer - Bencoded input buffer.</param>
        /// <param name="position">position - Position in buffer. SHould be at end of buffer on exit.</param>
        static private BNodeBase DecodeBNodes(byte[] buffer, ref int position)
        {
            BNodeBase bNode = null;
            while (buffer[position] != (byte)'e')
            {
                switch ((char)buffer[position])
                {
                    case 'd':
                        bNode = DecodeDictionary(buffer, ref position);
                        break;
                    case 'l':
                        bNode = DecodeList(buffer, ref position);
                        break;
                    case 'i':
                        bNode = DecodeNumber(buffer, ref position);
                        break;
                    default:
                        bNode = DecodeString(buffer, ref position);
                        break;
                }
                if (position == buffer.Length) break;
            }
            return bNode;
        }
        /// <summary>
        /// Decode the specified Bendcoded buffer.
        /// </summary>
        /// <returns>The root BNode.</returns>
        /// <param name="buffer">buffer - Bencoded input buffer.</param>
        static public BNodeBase Decode(byte[] buffer)
        {
            BNodeBase bNodeRoot;
            try
            {
                int position = 0;
                bNodeRoot = DecodeBNodes(buffer, ref position);
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new BitTorrentError("(BEncoding) Error: Failure on decoding torrent file into BNode tree."+ex.Message);
            }
            return bNodeRoot;
        }
        /// <summary>
        /// Produce Bencoded output given a root BNode.
        /// </summary>
        /// <returns>Bencoded representation of BNode tree.</returns>
        /// <param name="bNode">Root <paramref name="bNode"/>.</param>
        static public byte[] Encode(BNodeBase bNode)
        {
            List<byte> result = new List<byte>();
            try
            {
                if (bNode is BNodeDictionary bNodeDictionary)
                {
                    result.Add((byte)'d');
                    foreach (var key in (bNodeDictionary).dict.Keys)
                    {
                        result.AddRange(Encoding.ASCII.GetBytes($"{key.Length}:{key}"));
                        result.AddRange(Encode((bNodeDictionary).dict[key]));
                    }
                    result.Add((byte)'e');
                }
                else if (bNode is BNodeList bNodeList)
                {
                    result.Add((byte)'l');
                    foreach (var node in (bNodeList).list)
                    {
                        result.AddRange(Encode(node));
                    }
                    result.Add((byte)'e');
                }
                else if (bNode is BNodeNumber bNodeNumber)
                {
                    result.Add((byte)'i');
                    result.AddRange((bNodeNumber).number);
                    result.Add((byte)'e');
                }
                else if (bNode is BNodeString bNodeString)
                {
                    result.AddRange(Encoding.ASCII.GetBytes($"{(bNodeString).str.Length}:"));
                    result.AddRange((bNodeString).str);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new BitTorrentError("(BEncoding) Error: Failure to encode BNode Tree."+ex.Message);
            }
            return result.ToArray();
        }
        /// <summary>
        /// Get a BNode entry for a given dictionary key. Note that it recursively searches
        /// until the key is found in the BNode tree structure.
        /// </summary>
        /// <returns>BNode entry for dictionary key.</returns>
        /// <param name="bNode">bNode - Bnode root of dictionary.</param>
        /// <param name="entryKey">entryKey - Dictionary key of Bnode entry to return.</param>
        static public BNodeBase GetDictionaryEntry(BNodeBase bNode, string entryKey)
        {
            BNodeBase bNodeEntry = null;
            try
            {
                if (bNode is BNodeDictionary bNodeDictionary)
                {
                    if ((bNodeDictionary).dict.ContainsKey(entryKey))
                    {
                        return (bNodeDictionary).dict[entryKey];
                    }
                    foreach (var key in (bNodeDictionary).dict.Keys)
                    {
                        bNodeEntry = GetDictionaryEntry((bNodeDictionary).dict[key], entryKey);
                        if (bNodeEntry != null) break;
                    }
                }
                else if (bNode is BNodeList bNodeList)
                {
                    foreach (var node in (bNodeList).list)
                    {
                        bNodeEntry = GetDictionaryEntry(node, entryKey);
                        if (bNodeEntry != null) break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new BitTorrentError("(BEncoding) Error: Could not get dictionary from BNode tree."+ex.Message);
            }
            return bNodeEntry;
        }
        /// <summary>
        /// Return string representation of a given BNode dictionary entry.
        /// </summary>
        /// <returns>String value of a given byte encoded number or string.</returns>
        /// <param name="bNode">bNode - Entry to extract.</param>
        /// <param name="entryKey">entryKey - Key of dictionary entry to extract.</param>
        static public string GetDictionaryEntryString(BNodeBase bNode, string entryKey)
        {
            BNodeBase entryNode;
            try
            {
                entryNode = GetDictionaryEntry(bNode, entryKey);
                if (entryNode != null)
                {
                    if (entryNode is BNodeString bNodeString)
                    {
                        return Encoding.ASCII.GetString((bNodeString).str);
                    }
                    if (entryNode is BNodeNumber bNodeNumber)
                    {
                        return Encoding.ASCII.GetString((bNodeNumber).number);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new BitTorrentError("(BEncoding) Error: Could not get string from BNode tree."+ex.Message);
            }
            return "";
        }
    }
}
