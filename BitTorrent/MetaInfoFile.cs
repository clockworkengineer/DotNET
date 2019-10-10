using System;
using System.IO;
using System.Collections.Generic;

namespace BitTorrent
{
    public class MetaInfoFile
    {
        private string _fileName = string.Empty;
        private Dictionary<string, string> _metaInfoDict;
        private string _metaInfoData;
        private  string _kPathSeparator = $"{Path.DirectorySeparatorChar}";

        public string FileName { get => _fileName; set => _fileName = value; }

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
            var position = metaInfoData.IndexOf(encodeBenString(field));
            return (position);
        }

        private void parseString(string field)
        {
            if (_metaInfoDict.ContainsKey(field))
            {
                string metaInfoData = _metaInfoDict[field];
                var position = fieldIndex(metaInfoData, field);
                if (position != -1)
                {
                    position += $"{field.Length}:{field}".Length;
                    _metaInfoDict[field] = decodeBenString(metaInfoData, ref position);
                }
            }
        }

        private void parseInteger(string field)
        {
            if (_metaInfoDict.ContainsKey(field))
            {
                string metaInfoData = _metaInfoDict[field];
                var benString = $"{field.Length}:{field}";
                var position = metaInfoData.IndexOf(benString);
                if (position != -1)
                {
                    position += benString.Length + 1;
                    int end = metaInfoData.IndexOf('e', position);
                    _metaInfoDict[field] = metaInfoData.Substring(position, end - position);

                }
            }
        }

        private void parseStringList(string field)
        {
            if (_metaInfoDict.ContainsKey(field))
            {
                string metaInfoData = _metaInfoDict[field];
                var benString = $"{field.Length}:{field}";
                var position = metaInfoData.IndexOf(benString);
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
                    _metaInfoDict[field] = string.Join(",", announceList);
                }
            }
        }

        private void parseFiles(string field)
        {

            if (_metaInfoDict.ContainsKey(field))
            {
                string metaInfoData = _metaInfoDict[field];
                var benString = $"{field.Length}:{field}";
                var position = metaInfoData.IndexOf(benString, StringComparison.Ordinal);
                if (position != -1)
                {
                    string[] sep = { "eed6" };
                    List<string> lengths = new List<string>();
                    List<string> fileNames = new List<string>();
                    position += benString.Length;
                    _metaInfoDict[field] = metaInfoData.Substring(position, metaInfoData.LastIndexOf('e') - position);
                    string[] files = _metaInfoDict[field].Split(sep, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var file in files)
                    {
                        position = file.IndexOf(":length", StringComparison.Ordinal);
                        if (position != -1)
                        {
                            position += ":length".Length + 1;
                            int end = file.IndexOf('e', position);
                            lengths.Add(file.Substring(position, end - position));
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
                            fileNames.Add(string.Join(_kPathSeparator, announceList));
                        }
                    }
                    for (var fileNo=0; fileNo < files.Length; fileNo++)
                    {
                        _metaInfoDict[fileNo.ToString()] = _kPathSeparator + fileNames[fileNo] + ", " + lengths[fileNo];
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

            for (var section = 0; section < sections.Count-1; section++)
            {
                var length = sections[section+1].Item2 - sections[section].Item2;
                _metaInfoDict[sections[section].Item1] = _metaInfoData.Substring(sections[section].Item2,length);
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

            _metaInfoDict.Remove("files");

            foreach (var key in _metaInfoDict.Keys)
            {
                if (key != "pieces")
                {
                    Console.WriteLine($"{key}={_metaInfoDict[key]}");
                }
            }

        }

        public MetaInfoFile(string fileName)
        {
            FileName = fileName;
            _metaInfoDict = new Dictionary<string, string>();
        }

        public void load()
        {
            _metaInfoData = File.ReadAllText(_fileName);
        }

        public void parse()
        {

            loadTorrentDictionary();

        }

    }
}
