using System;
using System.IO;
using System.Collections.Generic;

namespace BitTorrent
{
    public class MetaInfoFile
    {
        private string _torrentFileName = string.Empty;
        private Dictionary<string, string> _metaInfoDict;
        private string _metaInfoData;
        private  string _kPathSeparator = $"{Path.DirectorySeparatorChar}";

        public string TorrentFileName { get => _torrentFileName; set => _torrentFileName = value; }
        public Dictionary<string, string> MetaInfoDict { get => _metaInfoDict; set => _metaInfoDict = value; }

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

        private string encodeBenString(string field)
        {
            return($"{field.Length}:{field}");
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
                string metaInfoData = MetaInfoDict[field];
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
                string metaInfoData = MetaInfoDict[field];
                string benString = encodeBenString(field);
                int position = metaInfoData.IndexOf(benString, StringComparison.Ordinal);
                if (position != -1)
                {
                    position += benString.Length + 1;
                    int end = metaInfoData.IndexOf('e', position);
                    MetaInfoDict[field] = metaInfoData.Substring(position, end - position);

                }
            }
        }

        private void parseStringList(string field)
        {
            if (MetaInfoDict.ContainsKey(field))
            {
                string metaInfoData = MetaInfoDict[field];
                string benString = encodeBenString(field);
                int position = metaInfoData.IndexOf(benString, StringComparison.Ordinal);
                if (position != -1)
                {
                    position += benString.Length+1;
                    List<string> announceList = new List<string>();
                    while (metaInfoData[position] != 'e')
                    {
                        if (metaInfoData[position] == 'l') position++;
                        announceList.Add(decodeBenString(metaInfoData, ref position));
                        if (metaInfoData[position] == 'e') position++;
                    }
                    MetaInfoDict[field] = string.Join(",", announceList);
                }
            }
        }

        private void parseFiles(string field)
        {

            if (MetaInfoDict.ContainsKey(field))
            {
                string metaInfoData = MetaInfoDict[field];
                string benString = encodeBenString(field);
                int position = metaInfoData.IndexOf(benString, StringComparison.Ordinal);

                if (position != -1)
                {
                    string[] sep = { "eed6" };
                    string fileName = string.Empty;
                    string length = string.Empty;
                    int fileNo = 0;
                    string[] files = MetaInfoDict[field].Split(sep, StringSplitOptions.RemoveEmptyEntries);

                    position += benString.Length;
                    MetaInfoDict[field] = metaInfoData.Substring(position, metaInfoData.LastIndexOf('e') - position);

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
                        MetaInfoDict[fileNo.ToString()] = _kPathSeparator + fileName + ", " + length;
                        fileNo++;
                    }

                }

            }
        }

        private void splitMetaInfoData()
        {
     
            List<ValueTuple<string, int>> sections = new List<ValueTuple<string, int>>();

            sections.Add(("announce", fieldIndex(_metaInfoData, "announce")));
            sections.Add(("announce-list", fieldIndex(_metaInfoData, "announce-list")));
            sections.Add(("comment", fieldIndex(_metaInfoData, "comment")));
            sections.Add(("created by", fieldIndex(_metaInfoData, "created by")));
            sections.Add(("creation date", fieldIndex(_metaInfoData, "creation date")));
            sections.Add(("name", fieldIndex(_metaInfoData, "name")));
            sections.Add(("piece length", fieldIndex(_metaInfoData, "piece length")));
            sections.Add(("pieces", fieldIndex(_metaInfoData, "pieces")));

            if (fieldIndex(_metaInfoData, "files") == -1)
            {
                sections.Add(("length", fieldIndex(_metaInfoData, "length")));
                sections.Add(("md5sum", fieldIndex(_metaInfoData, "md5sum")));
            }
            else
            {
                sections.Add(("files", fieldIndex(_metaInfoData, "files")));
            }
            sections.Add(("end", _metaInfoData.Length));

            sections.RemoveAll(tuple => tuple.Item2 == -1);
            sections.Sort((tuple1, tuple2) => tuple1.Item2.CompareTo(tuple2.Item2));

            for (int section = 0; section < sections.Count-1; section++)
            {
                int length = sections[section+1].Item2 - sections[section].Item2;
                MetaInfoDict[sections[section].Item1] = _metaInfoData.Substring(sections[section].Item2,length);
            }


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

            parseFiles("files");

            MetaInfoDict.Remove("files");

            foreach (var key in MetaInfoDict.Keys)
            {
                if (key != "pieces")
                {
                    Console.WriteLine($"{key}={MetaInfoDict[key]}");
                }
            }

        }

        public MetaInfoFile(string fileName)
        {
            TorrentFileName = fileName;
            MetaInfoDict = new Dictionary<string, string>();
        }

        public void load()
        {
            _metaInfoData = File.ReadAllText(TorrentFileName);
        }

        public void parse()
        {
            loadTorrentDictionary();
        }

    }
}
