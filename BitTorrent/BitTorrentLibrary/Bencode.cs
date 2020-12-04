//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: BitTorrent Bencode format encoding/decoding support 
// for the reading of torrent files and decoding HTTP tracker replies.
//
// Copyright 2020.
//
using System;
using System.Collections.Generic;
using System.Text;
namespace BitTorrentLibrary
{
    /// <summary>
    /// Base for BNode structure.
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
        public BNodeNumber(byte[] number)
        {
            this.number = number;
        }
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
    /// Class for Bencode encode/decode.
    /// </summary>
    internal class Bencode
    {
        private readonly List<byte> _workBuffer = new List<byte>(); // Temporary work buffer
        private int _position;                                      // Current deoce buffer postion
        private byte[] _buffer;                                     // Decode buffer
        /// <summary>
        /// Decode Integer digits from decode buffer into work buffer.
        /// </summary>
        private void DecodeInteger()
        {
            _workBuffer.Clear();
            while (char.IsDigit((char)_buffer[_position]))
            {
                _workBuffer.Add(_buffer[_position++]);
            }
            _position++;
        }
        /// <summary>
        /// Decode Bencode string in buffer.
        /// </summary>
        /// <returns>The bytes for the string.</returns>
        private byte[] DecodeString()
        {
            DecodeInteger();
            int lengthBytes = int.Parse(Encoding.ASCII.GetString(_workBuffer.ToArray()));
            _workBuffer.Clear();
            while (lengthBytes-- > 0)
            {
                _workBuffer.Add(_buffer[_position++]);
                if (_position == _buffer.Length) break;
            }
            return _workBuffer.ToArray();
        }
        /// <summary>
        /// Recursively parse a Bencoded buffer and return its BNode structure.
        /// </summary>
        /// <returns>Root of BNode structure.</returns>
        private BNodeBase DecodeBNode()
        {
            switch ((char)_buffer[_position])
            {
                case 'd':
                    var bNodeDictionary = new BNodeDictionary();
                    _position++;
                    while (_buffer[_position] != (byte)'e')
                    {
                        string key = Encoding.ASCII.GetString(DecodeString());
                        bNodeDictionary.dict[key] = DecodeBNode();
                    }
                    _position++;
                    return bNodeDictionary;
                case 'l':
                    var bNodeList = new BNodeList();
                    _position++;
                    while (_buffer[_position] != (byte)'e')
                    {
                        bNodeList.list.Add(DecodeBNode());
                    }
                    _position++;
                    return bNodeList;
                case 'i':
                    _position++;
                    DecodeInteger();
                    return new BNodeNumber(_workBuffer.ToArray());
                default:
                    return new BNodeString { str = DecodeString() };
            }
        }
        /// <summary>
        /// Recursively parse BNode structure to produce Bencoding for it.
        /// </summary>
        /// <returns>Bencoded representation of BNode structure.</returns>
        /// <param name="bNode">Root <paramref name="bNode"/>.</param>
        private void EncodeFromBNode(BNodeBase bNode)
        {
            try
            {
                if (bNode is BNodeDictionary bNodeDictionary)
                {
                    _workBuffer.Add((byte)'d');
                    foreach (var key in (bNodeDictionary).dict.Keys)
                    {
                        _workBuffer.AddRange(Encoding.ASCII.GetBytes($"{key.Length}:{key}"));
                        EncodeFromBNode((bNodeDictionary).dict[key]);
                    }
                    _workBuffer.Add((byte)'e');
                }
                else if (bNode is BNodeList bNodeList)
                {
                    _workBuffer.Add((byte)'l');
                    foreach (var node in (bNodeList).list)
                    {
                        EncodeFromBNode(node);
                    }
                    _workBuffer.Add((byte)'e');
                }
                else if (bNode is BNodeNumber bNodeNumber)
                {
                    _workBuffer.Add((byte)'i');
                    _workBuffer.AddRange((bNodeNumber).number);
                    _workBuffer.Add((byte)'e');
                }
                else if (bNode is BNodeString bNodeString)
                {
                    _workBuffer.AddRange(Encoding.ASCII.GetBytes($"{(bNodeString).str.Length}:"));
                    _workBuffer.AddRange((bNodeString).str);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Exception("Failure to encode BNode structure." + ex.Message);
            }
        }
        /// <summary>
        /// Initialise Bencode data and resources.
        /// </summary>
        /// <param name="buffer"></param>
        public Bencode(byte[] buffer = null)
        {
            _buffer = buffer;
        }
        /// <summary>
        /// Produce BNode structure from a Bencoded buffer.
        /// </summary>
        /// <returns>The root BNode.</returns>
        /// <param name="buffer">buffer - Bencoded input buffer.</param>
        public BNodeBase Decode(byte[] buffer = null)
        {
            try
            {
                _position = 0;
                _buffer = buffer;
                return DecodeBNode();
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new Exception("Failure on decoding torrent file into BNode structure." + ex.Message);
            }
        }
        /// <summary>
        /// Produce Bencoded output given the root of a BNode structure.
        /// </summary>
        /// <param name="bNode"></param>
        /// <returns></returns>
        public byte[] Encode(BNodeBase bNode)
        {
            _workBuffer.Clear();
            EncodeFromBNode(bNode);
            return _workBuffer.ToArray();
        }
        /// <summary>
        /// Get a BNode entry for a given dictionary key. Note that it recursively searches
        /// until the key is found in the BNode structure structure.
        /// </summary>
        /// <returns>BNode entry for dictionary key.</returns>
        /// <param name="bNode">bNode - Bnode root of dictionary.</param>
        /// <param name="entryKey">entryKey - Dictionary key of Bnode entry to return.</param>
        public BNodeBase GetDictionaryEntry(BNodeBase bNode, string entryKey)
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
                throw new Exception("Could not get dictionary from BNode structure." + ex.Message);
            }
            return bNodeEntry;
        }
        /// <summary>
        /// Return string representation of a given BNode dictionary entry.
        /// </summary>
        /// <returns>String value of a given byte encoded number or string.</returns>
        /// <param name="bNode">bNode - Entry to extract.</param>
        /// <param name="entryKey">entryKey - Key of dictionary entry to extract.</param>
        public string GetDictionaryEntryString(BNodeBase bNode, string entryKey)
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
                throw new Exception("Could not get string from BNode structure." + ex.Message);
            }
            return "";
        }
    }
}
