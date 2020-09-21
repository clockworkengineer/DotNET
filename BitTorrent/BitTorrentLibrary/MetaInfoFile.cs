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
        private byte[] _metaInfoData;
        public Dictionary<string, byte[]> MetaInfoDict { get; set; }
        public string TorrentFileName { get; set; } = string.Empty;

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
                UInt32 fileNo = 0;
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
                                path += Constants.PathSeparator + Encoding.ASCII.GetString(((BitTorrent.BNodeString)file).str);
                            }
                            fileEntry = path;
                        }
                        fileEntry += ",";
                        fileEntry += Bencoding.GetDictionaryEntryString(fileDictionaryItem, "length");
                        fileEntry += ",";
                        fileEntry += Bencoding.GetDictionaryEntryString(fileDictionaryItem, "md5string");
                        MetaInfoDict[fileNo.ToString()] = Encoding.ASCII.GetBytes(fileEntry);
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
                MetaInfoDict[field] = (bNodeNumber).number;
            }
            else if (fieldBytes is BNodeString bNodeString)
            {
                MetaInfoDict[field] = (bNodeString).str;
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
                MetaInfoDict[field] = Encoding.ASCII.GetBytes(string.Join(",", listString));
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
                byte[] infoHash = Bencoding.Encode(infoEncodedBytes);
                SHA1 sha = new SHA1CryptoServiceProvider();
                MetaInfoDict["info hash"] = sha.ComputeHash(infoHash);
            }
        }

        /// <summary>
        /// Initializes a new instance of the MetInfoFile class.
        /// </summary>
        /// <param name="fileName">File name.</param>
        public MetaInfoFile(string fileName)
        {
            TorrentFileName = fileName;
            MetaInfoDict = new Dictionary<string, byte[]>();
        }

        /// <summary>
        /// Load torrent file contents into memory for parsing.
        /// </summary>
        public void Load()
        {
            try
            {
                _metaInfoData = System.IO.File.ReadAllBytes(TorrentFileName);
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                throw new Error($"Error: Could not find torrent file {TorrentFileName}");
            }
            catch (System.IO.FileNotFoundException)
            {
                throw new Error($"Error: Could not find torrent file {TorrentFileName}");
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw;
            }
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

                foreach (var key in MetaInfoDict.Keys)
                {
                    if ((key != "pieces") && (key != "info") && (key != "info hash"))
                    {
                        Log.Logger.Debug($"{key}={Encoding.ASCII.GetString(MetaInfoDict[key])}");
                    }
                }
            }
            catch (Error)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex);
                throw;
            }
        }

        /// <summary>
        /// Generate list of local files in torrent to download from peers and total torrent size in bytes
        /// and return as a tuple.
        /// </summary>
        public ValueTuple<UInt64, List<FileDetails>>LocalFilesToDownloadList(string downloadPath)
        {
            List<FileDetails> filesToDownload = new List<FileDetails>();
            UInt64 totalBytes = 0;
            
            try
            {
                if (!MetaInfoDict.ContainsKey("0"))
                {
                    FileDetails fileDetail = new FileDetails
                    {
                        name = downloadPath + Constants.PathSeparator + Encoding.ASCII.GetString(MetaInfoDict["name"]),
                        length = UInt64.Parse(Encoding.ASCII.GetString(MetaInfoDict["length"])),
                        offset = 0
                    };
                    filesToDownload.Add(fileDetail);
                    totalBytes = fileDetail.length;
                }
                else
                {
                    UInt32 fileNo = 0;

                    string name = Encoding.ASCII.GetString(MetaInfoDict["name"]);
                    while (MetaInfoDict.ContainsKey(fileNo.ToString()))
                    {
                        string[] details = Encoding.ASCII.GetString(MetaInfoDict[fileNo.ToString()]).Split(',');
                        FileDetails fileDetail = new FileDetails
                        {
                            name = downloadPath + Constants.PathSeparator + name + details[0],
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
                throw new Error("BitTorrent Error (MetaInfoFile): Failed to create download file list.");
            }
            return (totalBytes, filesToDownload);
        }
    }
}
