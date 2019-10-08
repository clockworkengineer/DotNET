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

        private void parseInfo(string metaInfoData)
        {

            if (_metaInfoDict.ContainsKey("info"))
            {
                var infoDict = _metaInfoDict["info"];

                loadString(infoDict, "name");
                loadInteger(infoDict, "length");
                loadString(infoDict, "md5sum");
                loadInteger(infoDict, "piece length");
                loadString(infoDict, "piece");
                loadCollection(infoDict, "files");
            
            }

        }

        private void parseFiles(string metaInfoData)
        {

            if (_metaInfoDict.ContainsKey("files"))
            {
                var infoFiles = _metaInfoDict["files"];

            }

        }

        private void loadString(string metaInfoData, string field)
        {
            var benString = $"{field.Length}:{field}";
            var position = metaInfoData.IndexOf(benString);
            if (position != -1)
            {
                position += benString.Length;
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
                if (_metaInfoData[position] != 'l')
                {
                    throw new Exception("Error : missing string list.");
                }
                position++;
                List<string> announceList = new List<string>();
                while (_metaInfoData[position]!='e')
                {
                    if (_metaInfoData[position] == 'l') position++;
                    announceList.Add(decodeBenString(metaInfoData, ref position));
                    if (_metaInfoData[position] == 'e') position++;
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

        private void loadTorrentDictionary()
        {

            loadString(_metaInfoData, "announce");
            loadListString(_metaInfoData,"announce-list");
            loadString(_metaInfoData, "comment");
            loadString(_metaInfoData, "created by");
            loadInteger(_metaInfoData, "creation date");

            loadCollection(_metaInfoData, "info");

            parseInfo(_metaInfoData);
            parseFiles(_metaInfoData);

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
