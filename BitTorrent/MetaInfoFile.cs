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

        public string TorrentFileName { get => _torrentFileName; set => _torrentFileName = value; }
        public Dictionary<string, byte[]> MetaInfoDict { get => _metaInfoDict; set => _metaInfoDict = value; }

        private void getListOfDictionarys(BNodeBase bNodeRoot, string field)
        {
            BNodeBase fieldBytes = Bencoding.getDictionaryEntry(bNodeRoot, field);

            if (fieldBytes is BNodeList)
            {
                int fileNo = 0;
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
                                path += "/"+Encoding.ASCII.GetString(((BitTorrent.BNodeString)file).str);
                            }
                            fileEntry = path;
                        
                        }
                        fileEntry += ",";
                        fileField = Bencoding.getDictionaryEntry(fileDictionaryItem, "length");
                        if (fileField != null)
                        {
                            fileEntry+= Encoding.ASCII.GetString(((BNodeNumber)fileField).number);
                        }
                        fileEntry += ",";
                        fileField = Bencoding.getDictionaryEntry(fileDictionaryItem, "md5sum");
                        if (fileField != null)
                        {
                            fileEntry += Encoding.ASCII.GetString(((BNodeString)fileField).str);
                        }
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

        private void calcInfoHash(BNodeBase bNodeRoot)
        {
            BNodeBase fieldBytes = Bencoding.getDictionaryEntry(bNodeRoot, "info");
            if (fieldBytes != null)
            {
                byte[] infoHash = Bencoding.encode(fieldBytes);
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

            BNodeBase tmp = Bencoding.getDictionaryEntry(bNodeRoot, "files");
            if (tmp == null)
            {
                getStringOrNumeric(bNodeRoot, "length");
                getStringOrNumeric(bNodeRoot, "md5sum");
            }
            else
            {
                getListOfDictionarys(bNodeRoot, "files");
            }

            calcInfoHash(bNodeRoot);


            //foreach (var key in MetaInfoDict.Keys)
            //{
            //    if ((key != "pieces")&&(key!="info"))
            //    {
            //        Console.WriteLine($"{key}={Encoding.ASCII.GetString(MetaInfoDict[key])}");
            //    }
            //}


        }

        public MetaInfoFile(string fileName)
        {
            TorrentFileName = fileName;
            MetaInfoDict = new Dictionary<string, byte[]>();
        }

        public void load()
        {
            _metaInfoData = File.ReadAllBytes(TorrentFileName);
        }

        public void parse()
        {
            loadTorrentDictionary();
        }

    }
}
