using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace BitTorrent
{
    public class MetaInfoFile
    {
    
        private string _torrentFileName = string.Empty;
        private Dictionary<string, byte[]> _metaInfoDict;
        private byte[] _metaInfoData;
        private  string _kPathSeparator = $"{Path.DirectorySeparatorChar}";

        public Dictionary<string, byte[]> MetaInfoDict { get => _metaInfoDict; set => _metaInfoDict = value; }
      
        private void getListOfDictionarys(BNodeBase bNodeRoot, string field)
        {
            BNodeBase fieldBytes = Bencoding.getDictionaryEntry(bNodeRoot, field);

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
                        fileField = Bencoding.getDictionaryEntry(fileDictionaryItem, "path");
                        if (fileField != null)
                        {
                            string path = string.Empty;
                            foreach (var file in ((BNodeList)(fileField)).list)
                            {
                                path += _kPathSeparator + Encoding.ASCII.GetString(((BitTorrent.BNodeString)file).str);
                            }
                            fileEntry = path;
                        
                        }
                        fileEntry += ",";
                        fileEntry += Bencoding.getDictionaryEntryString(fileDictionaryItem, "length");
                        fileEntry += ",";
                        fileEntry += Bencoding.getDictionaryEntryString(fileDictionaryItem, "md5string");
                        _metaInfoDict[fileNo.ToString()] = Encoding.ASCII.GetBytes(fileEntry);
                        fileNo++;
                    }
                }

            }
        }

        private void getStringOrNumeric(BNodeBase bNodeRoot, string field)
        {
            BNodeBase fieldBytes = Bencoding.getDictionaryEntry(bNodeRoot, field);
            if (fieldBytes is BNodeNumber)
            {
                _metaInfoDict[field] = ((BNodeNumber)fieldBytes).number;
            }
            else if (fieldBytes is BNodeString)
            {
                _metaInfoDict[field] = ((BNodeString)fieldBytes).str;
            }

        }

        private void getListOfStrings(BNodeBase bNodeRoot, string field)
        {
            BNodeBase fieldBytes = Bencoding.getDictionaryEntry(bNodeRoot, field);
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

        private void calculateInfoHash(BNodeBase bNodeRoot)
        {
            BNodeBase infoEncodedBytes = Bencoding.getDictionaryEntry(bNodeRoot, "info");
            if (infoEncodedBytes != null)
            {
                byte[] infoHash = Bencoding.encode(infoEncodedBytes);
                SHA1 sha = new SHA1CryptoServiceProvider();
                _metaInfoDict["info hash"] = sha.ComputeHash(infoHash);
            
            }

        }

 
        private void loadTorrentDictionary()
        {

            BNodeBase bNodeRoot = Bencoding.decode(_metaInfoData);

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

            if (Bencoding.getDictionaryEntry(bNodeRoot, "files") == null)
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
                if ((key != "pieces")&&(key!="info")&&(key!="info hash"))
                {
                    Program.Logger.Debug($"{key}={Encoding.ASCII.GetString(MetaInfoDict[key])}");
                }
            }


        }

        public string getTrackerURL()
        {
            return(Encoding.ASCII.GetString(_metaInfoDict["announce"]));
        }

        public MetaInfoFile(string fileName)
        {
            _torrentFileName = fileName;
            _metaInfoDict = new Dictionary<string, byte[]>();
        }

        public void load()
        {
            _metaInfoData = System.IO.File.ReadAllBytes(_torrentFileName);
        }

        public void parse()
        {
            loadTorrentDictionary();
        }

    }
}
