//
// Author: Robert Tizzard
//
// Library: C# class library to implement the BitTorrent protocol.
//
// Description: The MetaInfoFile class loads and parses torrent files.
// It uses the BEncoding class in the parsing of the files; data which
// is extracted during this process is placed into a dictionary for retrieval
// by other modules.
//
// NOTE: So as I can see there is no underlying need to make this classes
// disctionary threadsafe (ie. use ConcurrentDictionary<>).
//
// Copyright 2020.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;
namespace BitTorrentLibrary
{>
    public class MetaInfoFile
    {
        private byte[] _metaInfoData;                           // Raw data of torrent file
        internal Dictionary<string, byte[]> metaInfoDict;       // Dictionary of torrent file contents
        public string TorrentFileName { get; }                  // Torrent file name
        /// <summary>
        /// Get a list of dictionaries from metainfo file that have been come under the main level dictionary
        /// key of "files". The output being a entry in the internal dictionary that contains a comma
        /// separated string of the fields (name, length, md5sum) for each list entry (file). Each file found is
        /// stored under the numeric key  value representing the number of the dictionary within the list
        /// (ie. "0", "1"..... "N").
        /// </summary>
        /// <param name="bNodeRoot">BNode root of list.</param>
        /// <param name="field">Field.</param>
        private void GetListOfDictionarys(BNodeBase bNodeRoot, string field)
        {
            BNodeBase fieldBytes = Bencoding.GetDictionaryEntry(bNodeRoot, field);
            if (fieldBytes is BNodeList bNodeList)
            {
                int fileNo = 0;
                foreach (var listItem in (bNodeList).list)
                {
                    if (listItem is BNodeDictionary bNodeDictionary)
                    {
                        BNodeBase fileDictionaryItem = (bNodeDictionary);
                        BNodeBase fileField = null;
                        string fileEntry = String.Empty;
                        fileField = Bencoding.GetDictionaryEntry(fileDictionaryItem, "path");
                        if (fileField != null)
                        {
                            string path = string.Empty;
                            foreach (var file in ((BNodeList)(fileField)).list)
                            {
                                path += $"{Path.DirectorySeparatorChar}" + Encoding.ASCII.GetString(((BitTorrentLibrary.BNodeString)file).str);
                            }
                            fileEntry = path;
                        }
                        fileEntry += ",";
                        fileEntry += Bencoding.GetDictionaryEntryString(fileDictionaryItem, "length");
                        fileEntry += ",";
                        fileEntry += Bencoding.GetDictionaryEntryString(fileDictionaryItem, "md5string");
                        metaInfoDict[fileNo.ToString()] = Encoding.ASCII.GetBytes(fileEntry);
                        fileNo++;
                    }
                }
            }
        }
        /// <summary>
        /// Gets the bytes representing a string or number (characters of number).
        /// </summary>
        /// <param name="bNodeRoot">BNode root of string number.</param>
        /// <param name="field">Field.</param>
        private void GetStringOrNumeric(BNodeBase bNodeRoot, string field)
        {
            BNodeBase fieldBytes = Bencoding.GetDictionaryEntry(bNodeRoot, field);
            if (fieldBytes is BNodeNumber bNodeNumber)
            {
                metaInfoDict[field] = (bNodeNumber).number;
            }
            else if (fieldBytes is BNodeString bNodeString)
            {
                metaInfoDict[field] = (bNodeString).str;
            }
        }
        /// <summary>
        /// Gets the list of strings from a BNode and create a comma separated string representing the
        /// list in the internal dictionary under the key value of field.
        /// </summary>
        /// <param name="bNodeRoot">BNode root of list of strings.</param>
        /// <param name="field">Field.</param>
        private void GetListOfStrings(BNodeBase bNodeRoot, string field)
        {
            BNodeBase fieldBytes = Bencoding.GetDictionaryEntry(bNodeRoot, field);
            if (fieldBytes is BNodeList bNodeList)
            {
                List<string> listString = new List<string>();
                foreach (var innerList in (bNodeList).list)
                {
                    if (innerList is BNodeList bNodeList2)
                    {
                        BNodeString stringItem = (BNodeString)(bNodeList2).list[0];
                        listString.Add(Encoding.ASCII.GetString(stringItem.str));
                    }
                }
                metaInfoDict[field] = Encoding.ASCII.GetBytes(string.Join(",", listString));
            }
        }
        /// <summary>
        /// Calculates the info hash for metainfo and stores in internal dictionary.
        /// </summary>
        /// <param name="bNodeRoot">BNode root of meta info.</param>
        private void CalculateInfoHash(BNodeBase bNodeRoot)
        {
            BNodeBase infoEncodedBytes = Bencoding.GetDictionaryEntry(bNodeRoot, "info");
            if (infoEncodedBytes != null)
            {
                metaInfoDict["info hash"] = new SHA1CryptoServiceProvider().ComputeHash(Bencoding.Encode(infoEncodedBytes));
            }
        }
        /// <summary>
        /// Load torrent file contents into memory for parsing.
        /// </summary>
        private void Load()
        {
            try
            {
                _metaInfoData = System.IO.File.ReadAllBytes(TorrentFileName);
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                throw new BitTorrentException($"Could not find torrent file {TorrentFileName}");
            }
            catch (System.IO.FileNotFoundException)
            {
                throw new BitTorrentException($"Could not find torrent file {TorrentFileName}");
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw;
            }
        }
        /// <summary>
        /// Initializes a new instance of the MetInfoFile class.
        /// </summary>
        /// <param name="fileName">File name.</param>
        public MetaInfoFile(string fileName)
        {
            try
            {
                TorrentFileName = fileName;
                metaInfoDict = new Dictionary<string, byte[]>();
                Load();
            }
            catch (Exception ex)
            {
                throw new BitTorrentException($"BitTorrent (MetaInfoFile) Error:" + ex.Message);
            }
        }
        /// <summary>
        /// Generate list of local files in torrent to download from peers and total torrent size in bytes
        /// and return as a tuple.
        /// </summary>
        internal ValueTuple<UInt64, List<FileDetails>> LocalFilesToDownloadList(string downloadPath)
        {
            List<FileDetails> filesToDownload = new List<FileDetails>();
            UInt64 totalBytes = 0;
            try
            {
                if (!metaInfoDict.ContainsKey("0"))
                {
                    FileDetails fileDetail = new FileDetails
                    {
                        name = downloadPath + $"{Path.DirectorySeparatorChar}" + Encoding.ASCII.GetString(metaInfoDict["name"]),
                        length = UInt64.Parse(Encoding.ASCII.GetString(metaInfoDict["length"])),
                        offset = 0
                    };
                    filesToDownload.Add(fileDetail);
                    totalBytes = fileDetail.length;
                }
                else
                {
                    int fileNo = 0;
                    string name = Encoding.ASCII.GetString(metaInfoDict["name"]);
                    while (metaInfoDict.ContainsKey(fileNo.ToString()))
                    {
                        string[] details = Encoding.ASCII.GetString(metaInfoDict[fileNo.ToString()]).Split(',');
                        FileDetails fileDetail = new FileDetails
                        {
                            name = downloadPath + $"{Path.DirectorySeparatorChar}" + name + details[0],
                            length = UInt64.Parse(details[1]),
                            md5sum = details[2],
                            offset = totalBytes
                        };
                        filesToDownload.Add(fileDetail);
                        fileNo++;
                        totalBytes += fileDetail.length;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new BitTorrentException("BitTorrent (MetaInfoFile) Error: Failed to create download file list." + ex.Message);
            }
            return (totalBytes, filesToDownload);
        }
        /// <summary>
        /// Decode Bencoded torrent file and load internal dictionary from its contents
        /// for later retrieval by other modules.
        /// </summary>
        public void Parse()
        {
            try
            {
                BNodeBase bNodeRoot = Bencoding.Decode(_metaInfoData);
                GetStringOrNumeric(bNodeRoot, "announce");
                GetListOfStrings(bNodeRoot, "announce-list");
                GetStringOrNumeric(bNodeRoot, "comment");
                GetStringOrNumeric(bNodeRoot, "created by");
                GetStringOrNumeric(bNodeRoot, "creation date");
                GetStringOrNumeric(bNodeRoot, "name");
                GetStringOrNumeric(bNodeRoot, "piece length");
                GetStringOrNumeric(bNodeRoot, "pieces");
                GetStringOrNumeric(bNodeRoot, "private");
                GetStringOrNumeric(bNodeRoot, "url-list");
                if (Bencoding.GetDictionaryEntry(bNodeRoot, "files") == null)
                {
                    GetStringOrNumeric(bNodeRoot, "length");
                    GetStringOrNumeric(bNodeRoot, "md5sum");
                }
                else
                {
                    GetListOfDictionarys(bNodeRoot, "files");
                }
                CalculateInfoHash(bNodeRoot);
                foreach (var key in metaInfoDict.Keys)
                {
                    if ((key != "pieces") && (key != "info") && (key != "info hash"))
                    {
                        Log.Logger.Debug($"{key}={Encoding.ASCII.GetString(metaInfoDict[key])}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw new BitTorrentException($"BitTorrent (MetaInfoFile) Error:" + ex.Message);
            }
        }
        /// <summary>
        /// Get URL of tracker to announce too.
        /// </summary>
        /// <returns></returns>
        public string GetTracker()
        {
            try
            {
                return Encoding.ASCII.GetString(metaInfoDict["announce"]);
            }
            catch (Exception)
            {
                throw new BitTorrentException("BitTorrent (MetaInfoFile) Error : File has not been parsed.");
            }
        }
        /// <summary>
        /// Get torrent info hash.
        /// </summary>
        /// <returns></returns>
        public byte[] GetInfoHash()
        {
            try
            {
                return metaInfoDict["info hash"];
            }
            catch (Exception)
            {
                throw new BitTorrentException("BitTorrent (MetaInfoFile) Error : File has not been parsed.");
            }
        }
        /// <summary>
        /// Get torrent piece length.
        /// </summary>
        /// <returns></returns>
        public UInt32 GetPieceLength()
        {
            try
            {
                return UInt32.Parse(Encoding.ASCII.GetString(metaInfoDict["piece length"]));
            }
            catch (Exception)
            {
                throw new BitTorrentException("BitTorrent (MetaInfoFile) Error : File has not been parsed.");
            }
        }
        /// <summary>
        /// Get piece info hash table. 
        /// </summary>
        /// <returns></returns>
        public byte[] GetPiecesInfoHash()
        {
            try
            {
                return metaInfoDict["pieces"];
            }
            catch (Exception)
            {
                throw new BitTorrentException("BitTorrent (MetaInfoFile) Error : File has not been parsed.");
            }
        }
    }
}
