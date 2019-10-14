using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BitTorrent
{
    public class MetaInfoFile
    {
        private string _torrentFileName = string.Empty;
        private Dictionary<string, byte[]> _metaInfoDict;
        private byte[] _metaInfoDataRaw;
        private string _metaInfoData;
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

        private int fieldIndex(byte[] searchIn, string field, int start = 0)
        {
            int found = -1;
            bool matched = false;
            byte[] searchBytes = Encoding.ASCII.GetBytes(encodeBenString(field));

            //only look at this if we have a populated search array and search bytes with a sensible start
            if (searchIn.Length > 0 && searchBytes.Length > 0 && start <= (searchIn.Length - searchBytes.Length) && searchIn.Length >= searchBytes.Length)
            {
                //iterate through the array to be searched
                for (int i = start; i <= searchIn.Length - searchBytes.Length; i++)
                {
                    //if the start bytes match we will start comparing all other bytes
                    if (searchIn[i] == searchBytes[0])
                    {
                        if (searchIn.Length > 1)
                        {
                            //multiple bytes to be searched we have to compare byte by byte
                            matched = true;
                            for (int y = 1; y <= searchBytes.Length - 1; y++)
                            {
                                if (searchIn[i + y] != searchBytes[y])
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


        private int fieldIndex(string metaInfoData,string field)
        {
            int position = metaInfoData.IndexOf(encodeBenString(field), StringComparison.Ordinal);
            return position;
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
                string metaInfoData = Encoding.ASCII.GetString(MetaInfoDict[field]);

                int position = fieldIndex(metaInfoData, field);
                if (position != -1)
                {
                    string[] sep = { "eed6" };
                    string fileName = string.Empty;
                    string length = string.Empty;
                    int fileNo = 0;
                    string[] files = Encoding.ASCII.GetString(MetaInfoDict[field]).Split(sep, StringSplitOptions.RemoveEmptyEntries);

                    position += encodeBenString(field).Length;
                    MetaInfoDict[field] = Encoding.ASCII.GetBytes((metaInfoData.Substring(position, metaInfoData.LastIndexOf('e') - position)));

                    foreach (var file in files)
                    {
                        position = file.IndexOf(":length", StringComparison.Ordinal);
                        if (position != -1)
                        {
                            position += ":length".Length + 1;
                            int end = file.IndexOf('e', position);
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

        private void splitMetaInfoData()
        {
     
            List<ValueTuple<string, int>> sections = new List<ValueTuple<string, int>>();

            sections.Add(("announce", fieldIndex(_metaInfoDataRaw, "announce")));
            sections.Add(("announce-list", fieldIndex(_metaInfoDataRaw, "announce-list")));
            sections.Add(("comment", fieldIndex(_metaInfoDataRaw, "comment")));
            sections.Add(("created by", fieldIndex(_metaInfoDataRaw, "created by")));
            sections.Add(("creation date", fieldIndex(_metaInfoDataRaw, "creation date")));
            sections.Add(("name", fieldIndex(_metaInfoDataRaw, "name")));
            sections.Add(("piece length", fieldIndex(_metaInfoDataRaw, "piece length")));
            sections.Add(("pieces", fieldIndex(_metaInfoDataRaw, "pieces")));
            sections.Add(("url-list", fieldIndex(_metaInfoDataRaw, "url-list")));

            if (fieldIndex(_metaInfoData, "files") == -1) 
            {
                sections.Add(("length", fieldIndex(_metaInfoDataRaw, "length")));
                sections.Add(("md5sum", fieldIndex(_metaInfoDataRaw, "md5sum")));
            }
            else
            {
                sections.Add(("files", fieldIndex(_metaInfoDataRaw, "files")));
            }
            sections.Add(("end", _metaInfoDataRaw.Length));

            sections.RemoveAll(tuple => tuple.Item2 == -1);
            sections.Sort((tuple1, tuple2) => tuple1.Item2.CompareTo(tuple2.Item2));

            for (int section = 0; section < sections.Count-1; section++)
            {
                int length = sections[section+1].Item2 - sections[section].Item2;
                byte[] buffer = new byte[length];
                Buffer.BlockCopy(_metaInfoDataRaw, sections[section].Item2, buffer, 0, length);
                MetaInfoDict[sections[section].Item1] = buffer;
            } 

            //sections.Clear();
            //sections.Add(("info", fieldIndex(_metaInfoData, "info")));
            //sections.Add(("url-list", fieldIndex(_metaInfoData, "url-list")));
            //sections.Add(("end", _metaInfoData.Length));

            //for (int section = 0; section < sections.Count - 1; section++)
            //{
            //    int length = sections[section + 1].Item2 - sections[section].Item2;
            //    MetaInfoDict[sections[section].Item1] = Encoding.ASCII.GetBytes(_metaInfoData.Substring(sections[section].Item2, length));
            //}

            //byte[] data = new byte[sections[1].Item2- sections[0].Item2-6];

            //int ii = 0;
            //for (var i = sections[0].Item2+6; i < sections[1].Item2; i++)
            //{
            //    data[ii] = (byte)_metaInfoDataRaw[i];
            //    ii++;
            //}


            //string tmp1= Encoding.ASCII.GetString(data);
            //byte[] result;

            //SHA1 sha = new SHA1CryptoServiceProvider();

            //result = sha.ComputeHash(data);

            //StringBuilder hex = new StringBuilder(result.Length);
            //foreach (byte b in result)
            //    hex.AppendFormat("{0:x2}", b);
            //Console.WriteLine(hex);

        }

        private void loadTorrentDictionary()
        {

            splitMetaInfoData();

            parseString("announce");
            parseStringList("announce-list");
            parseString("comment");
            parseString("created by");
            parseInteger("creation date");
            parseString("name");
            parseInteger("piece length");
            parseString("pieces");
            parseString("md5sum");
            parseInteger("length");
            parseString("url-list");

            parseFiles("files");

            MetaInfoDict.Remove("files");

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
            _metaInfoDataRaw = File.ReadAllBytes(TorrentFileName);
            _metaInfoData = Encoding.ASCII.GetString(_metaInfoDataRaw);
        }

        public void parse()
        {
            loadTorrentDictionary();
        }

    }
}
