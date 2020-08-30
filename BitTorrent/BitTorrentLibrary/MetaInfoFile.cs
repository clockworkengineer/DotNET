//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: The MetaInfoFile class loads and parses .torrent files.
// It uses the BEncoding class in the parsing of the files; data which
// is extracted during this process is placed into a dictionary for retrieval
// by other modules.
//
// Copyright 2019.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace BitTorrent
{
    /// <summary>
    /// Meta Info File class.
    /// </summary>
    public class MetaInfoFile
    {
    
        private string _torrentFileName = string.Empty;
        private Dictionary<string, byte[]> _metaInfoDict;
        private byte[] _metaInfoData;

        public Dictionary<string, byte[]> MetaInfoDict { get => _metaInfoDict; set => _metaInfoDict = value; }
        public string TorrentFileName { get => _torrentFileName; set => _torrentFileName = value; }

        /// <summary>
        /// Get a list of dictionaries from metainfo file that have been come under the main level dictionary 
        /// key of "files". The output being a entry in the internal dictionary that contains a comma 
        /// separated string of the fields (name, length, md5sum) for each list entry (file). Each file found is 
        /// stored under the numeric key  value representing the number of the dictionary within the list 
        /// (ie. "0", "1"..... "N"). 
        /// </summary>
        /// <param name="bNodeRoot">BNode root of list.</param>
        /// <param name="field">Field.</param>
        private void getListOfDictionarys(BNodeBase bNodeRoot, string field)
        {
            BNodeBase fieldBytes = Bencoding.GetDictionaryEntry(bNodeRoot, field);

            if (fieldBytes is BNodeList)
            {
                UInt32 fileNo = 0;
                List<string> listString = new List<string>();
                foreach (var listItem in ((BNodeList)(fieldBytes)).list)
                {
                    if (listItem is BNodeDictionary)
                    {
                        BNodeBase fileDictionaryItem = ((BitTorrent.BNodeDictionary)listItem);
                        BNodeBase fileField = null;
                        string fileEntry = String.Empty;
                        fileField = Bencoding.GetDictionaryEntry(fileDictionaryItem, "path");
                        if (fileField != null)
                        {
                            string path = string.Empty;
                            foreach (var file in ((BNodeList)(fileField)).list)
                            {
                                path += Constants.kPathSeparator + Encoding.ASCII.GetString(((BitTorrent.BNodeString)file).str);
                            }
                            fileEntry = path;
                        
                        }
                        fileEntry += ",";
                        fileEntry += Bencoding.GetDictionaryEntryString(fileDictionaryItem, "length");
                        fileEntry += ",";
                        fileEntry += Bencoding.GetDictionaryEntryString(fileDictionaryItem, "md5string");
                        _metaInfoDict[fileNo.ToString()] = Encoding.ASCII.GetBytes(fileEntry);
                        fileNo++;
                    }
                }

            }
        }

        /// <summary>
        /// Gets the bytes representing a string or number (characters of number).
        /// </summary>
        /// <param name="bNodeRoot">BNode root of of string number.</param>
        /// <param name="field">Field.</param>
        private void getStringOrNumeric(BNodeBase bNodeRoot, string field)
        {
            BNodeBase fieldBytes = Bencoding.GetDictionaryEntry(bNodeRoot, field);
            if (fieldBytes is BNodeNumber)
            {
                _metaInfoDict[field] = ((BNodeNumber)fieldBytes).number;
            }
            else if (fieldBytes is BNodeString)
            {
                _metaInfoDict[field] = ((BNodeString)fieldBytes).str;
            }

        }

        /// <summary>
        /// Gets the list of strings from a BNode and create a comma separated string representing the
        /// list in the internal dictionary under the key value of field.
        /// </summary>
        /// <param name="bNodeRoot">BNode root of list of strings.</param>
        /// <param name="field">Field.</param>
        private void getListOfStrings(BNodeBase bNodeRoot, string field)
        {
            BNodeBase fieldBytes = Bencoding.GetDictionaryEntry(bNodeRoot, field);
            if (fieldBytes is BNodeList)
            {
                List<string> listString = new List<string>();
                foreach(var innerList in ((BNodeList)(fieldBytes)).list)
                {
                    if (innerList is BNodeList)
                    {
                        BNodeString stringItem = (BNodeString)((BNodeList)(innerList)).list[0];
                        listString.Add(Encoding.ASCII.GetString(stringItem.str));
                    }
                }
                _metaInfoDict[field] = Encoding.ASCII.GetBytes(string.Join(",",listString));
            }

        }

        /// <summary>
        /// Calculates the info hash for metainfo and stores in internal dictionary.
        /// </summary>
        /// <param name="bNodeRoot">BNode root of meta info.</param>
        private void calculateInfoHash(BNodeBase bNodeRoot)
        {
            BNodeBase infoEncodedBytes = Bencoding.GetDictionaryEntry(bNodeRoot, "info");
            if (infoEncodedBytes != null)
            {
                byte[] infoHash = Bencoding.Encode(infoEncodedBytes);
                SHA1 sha = new SHA1CryptoServiceProvider();
                _metaInfoDict["info hash"] = sha.ComputeHash(infoHash);
            
            }

        }
    
        /// <summary>
        /// Initializes a new instance of the MetInfoFile class.
        /// </summary>
        /// <param name="fileName">File name.</param>
        public MetaInfoFile(string fileName)
        {
            TorrentFileName = fileName;
            _metaInfoDict = new Dictionary<string, byte[]>();
        }

        /// <summary>
        /// Gets the main tracker URL.
        /// </summary>
        /// <returns>The tracker URL as a string .</returns>
        public string GetTrackerURL()
        {
            try
            {
                return (Encoding.ASCII.GetString(_metaInfoDict["announce"]));
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
            }

            return ("");

        }

        /// <summary>
        /// Decode Bencoded torrent file and load internal dictionary from its contents
        /// for later retrieval by other modules.
        /// </summary>
        public void Parse()
        {
            try
            {
                _metaInfoData = System.IO.File.ReadAllBytes(TorrentFileName);

                BNodeBase bNodeRoot = Bencoding.Decode(_metaInfoData);

                getStringOrNumeric(bNodeRoot, "announce");
                getListOfStrings(bNodeRoot, "announce-list");
                getStringOrNumeric(bNodeRoot, "comment");
                getStringOrNumeric(bNodeRoot, "created by");
                getStringOrNumeric(bNodeRoot, "creation date");
                getStringOrNumeric(bNodeRoot, "name");
                getStringOrNumeric(bNodeRoot, "piece length");
                getStringOrNumeric(bNodeRoot, "pieces");
                getStringOrNumeric(bNodeRoot, "private");
                getStringOrNumeric(bNodeRoot, "url-list");

                if (Bencoding.GetDictionaryEntry(bNodeRoot, "files") == null)
                {
                    getStringOrNumeric(bNodeRoot, "length");
                    getStringOrNumeric(bNodeRoot, "md5sum");
                }
                else
                {
                    getListOfDictionarys(bNodeRoot, "files");
                }

                calculateInfoHash(bNodeRoot);

                foreach (var key in MetaInfoDict.Keys)
                {
                    if ((key != "pieces") && (key != "info") && (key != "info hash"))
                    {
                        Log.Logger.Debug($"{key}={Encoding.ASCII.GetString(MetaInfoDict[key])}");
                    }
                }

            }
            catch (System.IO.FileNotFoundException ex)
            {
                Log.Logger.Debug(ex);
                throw new Error($"Error: Could not find torrent file {TorrentFileName}");
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
            }
        }

    }
}
