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

        private string decodeBenString(string metaInfoData, ref int position)
        {

            int end = metaInfoData.IndexOf(':', position);
            int length = int.Parse(metaInfoData.Substring(position, end - position));

            if (end + length > metaInfoData.Length)
            {
                length = metaInfoData.Length - end - 1;
            }

            position = end + length + 1;

            return (metaInfoData.Substring(end + 1, length));


        }

        private byte[] decodeBenString(byte[] metaInfoData, ref int position)
        {
            int end = position;

            while (metaInfoData[end] != ':')
            {
                end++;
            }

            byte[] lengthBytes = new byte[end - position];
            Buffer.BlockCopy(metaInfoData, position, lengthBytes, 0, end - position);

            int length = int.Parse(Encoding.ASCII.GetString(lengthBytes));

            if (end + length > metaInfoData.Length)
            {
                length = metaInfoData.Length - end - 1; 
            }

            position = end + length + 1;

            byte[] retBuffer = new byte[length];
            Buffer.BlockCopy(metaInfoData, end + 1, retBuffer, 0, length);

            return (retBuffer);

        }

        private string encodeBenString(string field)
        {
            return($"{field.Length}:{field}");
        }

        private int fieldIndex(byte[] metaInfoData, string field, int start = 0)
        {
            int found = -1;
            bool matched = false;
            byte[] searchBytes = Encoding.ASCII.GetBytes(encodeBenString(field));

            //only look at this if we have a populated search array and search bytes with a sensible start
            if (metaInfoData.Length > 0 && searchBytes.Length > 0 && start <= 
                (metaInfoData.Length - searchBytes.Length) && metaInfoData.Length >= searchBytes.Length)
            {
                //iterate through the array to be searched
                for (int i = start; i <= metaInfoData.Length - searchBytes.Length; i++)
                {
                    //if the start bytes match we will start comparing all other bytes
                    if (metaInfoData[i] == searchBytes[0])
                    {
                        if (metaInfoData.Length > 1)
                        {
                            //multiple bytes to be searched we have to compare byte by byte
                            matched = true;
                            for (int y = 1; y <= searchBytes.Length - 1; y++)
                            {
                                if (metaInfoData[i + y] != searchBytes[y])
                                {
                                    matched = false;
                                    break;
                                }
                            }
                            //everything matched up
                            if (matched)
                            {
                                found = i;
                                break;
                            }

                        }
                        else
                        {
                            //search byte is only one bit nothing else to do
                            found = i;
                            break; //stop the loop
                        }

                    }
                }

            }
            return found;
        }

        private void parseString(string field)
        {
            if (MetaInfoDict.ContainsKey(field))
            {
                byte[] metaInfoData = MetaInfoDict[field];
                int position = fieldIndex(metaInfoData, field);
                if (position != -1)
                {
                    position += encodeBenString(field).Length;
                    MetaInfoDict[field] = decodeBenString(metaInfoData, ref position);
                }
            }
        }

        private void parseInteger(string field)
        {
            if (MetaInfoDict.ContainsKey(field))
            {
                byte[] metaInfoData = MetaInfoDict[field];
                int position = fieldIndex(metaInfoData, field);
                if (position != -1)
                {
                    position += encodeBenString(field).Length + 1;
                    int end = position;
                    while(metaInfoData[end]!='e')
                    {
                        end++;
                    }
                    byte[] buffer = new byte[end - position];
                    Buffer.BlockCopy(metaInfoData, position, buffer, 0, end - position);
                    MetaInfoDict[field] = buffer;

                }
            }
        }

        private void parseStringList(string field)
        {
            if (MetaInfoDict.ContainsKey(field))
            {
                byte[] metaInfoData = MetaInfoDict[field];
                int position = fieldIndex(metaInfoData, field);
                if (position != -1)
                {
                    position += encodeBenString(field).Length+1;
                    List<string> announceList = new List<string>();
                    while (metaInfoData[position] != 'e')
                    {
                        if (metaInfoData[position] == 'l') position++;
                        announceList.Add(Encoding.ASCII.GetString(decodeBenString(metaInfoData, ref position)));
                        if (metaInfoData[position] == 'e') position++;
                    }
                    MetaInfoDict[field] = Encoding.ASCII.GetBytes(string.Join(",", announceList));
                }
            }
        }

        private void parseFiles(string field)
        {

            if (MetaInfoDict.ContainsKey(field))
            {
                byte[] metaInfoData = MetaInfoDict[field];

                int position = fieldIndex(metaInfoData, field);
                if (position != -1)
                {
                    string[] sep = { "eed6" };
                    string fileName = string.Empty;
                    string length = string.Empty;
                    int fileNo = 0;
                    string[] files = Encoding.ASCII.GetString(MetaInfoDict[field]).Split(sep, StringSplitOptions.RemoveEmptyEntries);

                    position += encodeBenString(field).Length;
                    int end = metaInfoData.Length-1;
                    while (metaInfoData[end]!='e')
                    {
                        end--;
                    }
                    byte[] buffer = new byte[end - position];
                    Buffer.BlockCopy(metaInfoData, position, buffer, 0, end - position);
                    MetaInfoDict[field] = buffer;

                    foreach (var file in files)
                    {
                        position = file.IndexOf(":length", StringComparison.Ordinal);
                        if (position != -1)
                        {
                            position += ":length".Length + 1;
                            end = file.IndexOf('e', position);
                            length = file.Substring(position, end - position);
                        }
                        position = file.IndexOf("4:path", StringComparison.Ordinal);
                        if (position != -1)
                        {
                            position += "4:path".Length + 1;
                            List<string> announceList = new List<string>();
                            while (position!=file.Length)
                            {
                                if (file[position] == 'e') break;
                                announceList.Add(decodeBenString(file, ref position));

                            }
                            fileName = string.Join(_kPathSeparator, announceList);
                        }
                        MetaInfoDict[fileNo.ToString()] = Encoding.ASCII.GetBytes(_kPathSeparator + fileName + ", " + length);
                        fileNo++;
                    }

                }

            }
        }

        private void getStringOrNumeric(Bencoding.BNodeBase bNodeRoot, string field)
        {
            Bencoding.BNodeBase fieldBytes = Bencoding.getDictionaryEntry(bNodeRoot, field);
            if (fieldBytes is Bencoding.BNodeNumber)
            {
                _metaInfoDict[field] = ((Bencoding.BNodeNumber)fieldBytes).number;
            }
            else if (fieldBytes is Bencoding.BNodeString)
            {
                _metaInfoDict[field] = ((Bencoding.BNodeString)fieldBytes).str;
            }

        }

        private void getListOfStrings(Bencoding.BNodeBase bNodeRoot, string field)
        {
            Bencoding.BNodeBase fieldBytes = Bencoding.getDictionaryEntry(bNodeRoot, field);
            if (fieldBytes is Bencoding.BNodeList)
            {
                List<string> listString = new List<string>();
                foreach(var innerList in ((Bencoding.BNodeList)(fieldBytes)).list)
                {
                    if (innerList is Bencoding.BNodeList)
                    {
                        Bencoding.BNodeString stringItem = (Bencoding.BNodeString)((Bencoding.BNodeList)(innerList)).list[0];
                        listString.Add(Encoding.ASCII.GetString(stringItem.str));
                    }
                }
                _metaInfoDict[field] = Encoding.ASCII.GetBytes(string.Join(",",listString));
            }

        }

        private void splitMetaInfoData()
        {

            Bencoding.BNodeBase bNodeRoot = Bencoding.decode(_metaInfoData);

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

            //List<ValueTuple<string, int>> sections = new List<ValueTuple<string, int>>();

            //sections.Add(("announce", fieldIndex(_metaInfoData, "announce")));
            //sections.Add(("announce-list", fieldIndex(_metaInfoData, "announce-list")));
            //sections.Add(("comment", fieldIndex(_metaInfoData, "comment")));
            //sections.Add(("created by", fieldIndex(_metaInfoData, "created by")));
            //sections.Add(("creation date", fieldIndex(_metaInfoData, "creation date")));
            //sections.Add(("name", fieldIndex(_metaInfoData, "name")));
            //sections.Add(("piece length", fieldIndex(_metaInfoData, "piece length")));
            //sections.Add(("pieces", fieldIndex(_metaInfoData, "pieces")));
            //sections.Add(("private", fieldIndex(_metaInfoData, "private")));
            //sections.Add(("url-list", fieldIndex(_metaInfoData, "url-list")));


            //if (fieldIndex(_metaInfoData, "files") == -1) 
            //{
            //    sections.Add(("length", fieldIndex(_metaInfoData, "length")));
            //    sections.Add(("md5sum", fieldIndex(_metaInfoData, "md5sum")));
            //}
            //else
            //{
            //    sections.Add(("files", fieldIndex(_metaInfoData, "files")));
            //}
            //sections.Add(("end", _metaInfoData.Length));

            //sections.RemoveAll(tuple => tuple.Item2 == -1);
            //sections.Sort((tuple1, tuple2) => tuple1.Item2.CompareTo(tuple2.Item2));

            //for (int section = 0; section < sections.Count-1; section++)
            //{
            //    int length = sections[section+1].Item2 - sections[section].Item2;
            //    byte[] buffer = new byte[length];
            //    Buffer.BlockCopy(_metaInfoData, sections[section].Item2, buffer, 0, length);
            //    MetaInfoDict[sections[section].Item1] = buffer;
            //} 

            //sections.Clear();
            //sections.Add(("info", fieldIndex(_metaInfoData, "info")));
            //sections.Add(("url-list", fieldIndex(_metaInfoData, "url-list")));
            //sections.Add(("end", _metaInfoData.Length));
            //sections.RemoveAll(tuple => tuple.Item2 == -1);

            //for (int section = 0; section < sections.Count - 1; section++)
            //{
            //    int length = sections[section + 1].Item2 - sections[section].Item2;
            //    byte[] buffer = new byte[length-6];
            //    Buffer.BlockCopy(_metaInfoData, sections[section].Item2+6, buffer, 0, length-6);
            //    MetaInfoDict[sections[section].Item1] = buffer;
            //}

            //byte[] infoHash = _metaInfoDict["info"];

            //if (!_metaInfoDict.ContainsKey("url-list"))
            //{
            //    byte[] truncArray = new byte[infoHash.Length - 1];
            //    Array.Copy(infoHash, truncArray, truncArray.Length);
            //    infoHash = truncArray;
            //}

            //SHA1 sha = new SHA1CryptoServiceProvider();

            //_metaInfoDict["info hash"] = sha.ComputeHash(infoHash);


        }

        private void loadTorrentDictionary()
        {

            splitMetaInfoData();

            //parseString("announce");
            //parseStringList("announce-list");
            //parseString("comment");
            //parseString("created by");
            //parseInteger("creation date");
            //parseString("name");
            //parseInteger("piece length");
            //parseString("pieces");
            //parseString("md5sum");
            //parseInteger("length");
            //parseInteger("private");
            //parseString("url-list");

            //parseFiles("files");

            //MetaInfoDict.Remove("files");

            foreach (var key in MetaInfoDict.Keys)
            {
                if ((key != "pieces")&&(key!="info"))
                {
                    Console.WriteLine($"{key}={Encoding.ASCII.GetString(MetaInfoDict[key])}");
                }
            }

  
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
