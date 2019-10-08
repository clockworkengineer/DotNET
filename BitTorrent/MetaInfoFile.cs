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

        public string FileName { get => _fileName; set => _fileName = value; }

        private string decodeBenString(string metaInfoData, ref int position)
        {
            int end = position;
            while(metaInfoData[end]!=':')
            {
                end++;
            }

            if (end > position)
            {
                var length = int.Parse(metaInfoData.Substring(position, end - position));

                if (end + length > metaInfoData.Length)
                {
                    length = metaInfoData.Length - end - 1;
                }

                position = end + length + 1;

                return (metaInfoData.Substring(end + 1, length));

            }
            else
            {
                position++;
                return ("");
            }

        }

        private string encodeBenString(string field)
        {
            return($"{field.Length}:{field}");
        }

        private void parseInfo()
        {

            if (_metaInfoDict.ContainsKey("info"))
            {
                var infoData = _metaInfoDict["info"];

                loadString(infoData, "name");
                loadInteger(infoData, "length");
                loadString(infoData, "md5sum");
                loadInteger(infoData, "piece length");
                loadString(infoData, "piece");
                loadCollection(infoData, "files");
            
            }

        }

        private void parseFiles()
        {

            if (_metaInfoDict.ContainsKey("files"))
            {
                var infoFiles = _metaInfoDict["files"];

            }

        }

        private int fieldIndex(string metaInfoData, string field)
        {
            var position = metaInfoData.IndexOf(encodeBenString(field));
            return (position);
        }

        private void loadString(string metaInfoData, string field)
        {
            var position = fieldIndex(metaInfoData, field);
            if (position != -1)
            {
                position += $"{field.Length}:{field}".Length;
                _metaInfoDict[field] = decodeBenString(metaInfoData, ref position);
            }
        }

        private void loadInteger(string metaInfoData, string field)
        {
            var benString = $"{field.Length}:{field}";
            var position = metaInfoData.IndexOf(benString);
            if (position != -1)
            {
                position += benString.Length + 1;
                int end = position;
                while (metaInfoData[end] != 'e')
                {
                    end++;
                }
                _metaInfoDict[field] = metaInfoData.Substring(position, end-position);

            }
        }

        private void loadListString(string metaInfoData, string field)
        {
            var benString = $"{field.Length}:{field}";
            var position = metaInfoData.IndexOf(benString);
            if (position != -1)
            {
                position += benString.Length;
                if (metaInfoData[position] != 'l')
                {
                    throw new Exception("Error : missing string list.");
                }
                position++;
                List<string> announceList = new List<string>();
                while (metaInfoData[position]!='e')
                {
                    if (metaInfoData[position] == 'l') position++;
                    announceList.Add(decodeBenString(metaInfoData, ref position));
                    if (metaInfoData[position] == 'e') position++;
                }
                _metaInfoDict[field] = string.Join(",", announceList);
            }
        }

        private void loadCollection(string metaInfoData, string field)
        {
            var benString = $"{field.Length}:{field}";
            var position = metaInfoData.IndexOf(benString);
            if (position != -1)
            {
                position += benString.Length;
                _metaInfoDict[field] = metaInfoData.Substring(position, metaInfoData.LastIndexOf('e') - position);
            }
           
        }

        private void splitMetaInfoData(string metaInfoData)
        {
     
            List<ValueTuple<string, int>> sections = new List<ValueTuple<string, int>>();

            sections.Add(("announce", fieldIndex(_metaInfoData, "announce")));
            sections.Add(("announce-list", fieldIndex(_metaInfoData, "announce-list")));
            sections.Add(("comment", fieldIndex(_metaInfoData, "comment")));
            sections.Add(("created by", fieldIndex(_metaInfoData, "created by")));
            sections.Add(("creation date", fieldIndex(_metaInfoData, "creation date")));
            sections.Add(("info", fieldIndex(_metaInfoData, "info")));
            sections.Add(("end", _metaInfoData.Length));

            sections.RemoveAll(tuple => tuple.Item2 == -1);
            sections.Sort((tuple1, tuple2) => tuple1.Item2.CompareTo(tuple2.Item2));

            for (var section = 0; section < sections.Count-1; section++)
            {
                Console.WriteLine($"{sections[section].Item1}, {sections[section].Item2} {sections[section + 1].Item2 - 1}");
                var length = sections[section+1].Item2 - sections[section].Item2;
                _metaInfoDict[sections[section].Item1] = _metaInfoData.Substring(sections[section].Item2,length);
            }



        }

        private void loadTorrentDictionary()
        {

            splitMetaInfoData(_metaInfoData);

            loadString(_metaInfoDict["announce"], "announce");
            loadListString(_metaInfoDict["announce-list"], "announce-list");
            loadString(_metaInfoDict["comment"], "comment");
            loadString(_metaInfoDict["created by"], "created by");
            loadInteger(_metaInfoDict["creation date"], "creation date");

            loadCollection(_metaInfoDict["info"], "info");

            parseInfo();
            parseFiles();

            _metaInfoDict.Remove("info");
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
            if ((_metaInfoData[0] != 'd') || (_metaInfoData[_metaInfoData.Length-1]!='e'))
            {
                throw new Exception("Error : Invalid torrent file.");
            }

            loadTorrentDictionary();

        }

    }
}
