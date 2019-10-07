using System;
using System.IO;
using System.Collections.Generic;

namespace BitTorrent
{
    public class MetaInfoFile
    {
        private string _fileName = string.Empty;
        private Dictionary<string, string> _metaInfoDict;
        private Dictionary<string, string> _infoDict;
        private string _metaInfoData;

        public string FileName { get => _fileName; set => _fileName = value; }

        private string fromBenString(string metaInfoData, ref int position)
        {
            int end = position;
            while(metaInfoData[end]!=':')
            {
                end++;
            }

            if (end > position)
            {
                var length = int.Parse(metaInfoData.Substring(position, end - position));

                //if (end + length > metaInfoData.Length)
                //{
                //    length = metaInfoData.Length - end - 1;
                //}

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
            var position = 0;
            var end = 0;
            var fileDict = _metaInfoDict["info"];
            if (fileDict[position] !='d')
            {
                throw new Exception("Error : Missing Info dictionary.");
            }
            position++;
            while(position != fileDict.Length)
            {
                var key = fromBenString(fileDict, ref position);
                if (key=="")
                {
                    break;
                }
      //          Console.WriteLine(key);
                switch(fileDict[position])
                {
                    case 'i':
                        end = position;
                        while (fileDict[end]!='e')
                        {
                            end++;
                        }
                        _infoDict[key] = fileDict.Substring(position+1, end-position-1);
                        position = end + 1;
                        break;
                    case 'd':
                    case 'l':
                        throw new Exception("Error : Unkown Info format.");
                    default:
                        _infoDict[key] = fromBenString(fileDict, ref position);
                        break;

                }
            }
        }

        private void loadString(string metaInfoData, string field)
        {
            var benString = $"{field.Length}:{field}";
            var position = metaInfoData.IndexOf(benString);
            if (position != -1)
            {
                position += benString.Length;
                _metaInfoDict[field] = fromBenString(metaInfoData, ref position);
            }
        }

        private void loadInteger(string metaInfoData, string field)
        {
            var benString = $"{field.Length}:{field}";
            var position = metaInfoData.IndexOf(benString);
            if (position != -1)
            {
                position += benString.Length+1;
                _metaInfoDict[field] = metaInfoData.Substring(position, metaInfoData.IndexOf('e'));

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
                while (_metaInfoData[position]!='e')
                {
                    Console.WriteLine(fromBenString(metaInfoData, ref position));
                }
            }
        }

        private void loadDictionary(string metaInfoData, string field)
        {
            var benString = $"{field.Length}:{field}";
            var position = metaInfoData.IndexOf(benString);
            if (position != -1)
            {
                position += benString.Length;
                _metaInfoDict[field] = metaInfoData.Substring(position, metaInfoData.LastIndexOf('e') - position);
            }
            parseInfo(_metaInfoData);
        }

        private void loadTorrentDictionary()
        {

            loadString(_metaInfoData, "announce");
           // loadListString(_metaInfoData,"announce-list");
            loadString(_metaInfoData, "comment");
            loadString(_metaInfoData, "created by");
            loadInteger(_metaInfoData, "creation date");
            loadDictionary(_metaInfoData, "info");

            foreach (var key in _metaInfoDict.Keys)
            {
                if (key != "info")
                {
                    Console.WriteLine($"{key}={_metaInfoDict[key]}");
                }
            }

            foreach (var key in _infoDict.Keys)
            {
                if (key != "pieces")
                {
                    Console.WriteLine($"{key}={_infoDict[key]}");
                }
            }

        }

        public MetaInfoFile(string fileName)
        {
            FileName = fileName;
            _metaInfoDict = new Dictionary<string, string>();
            _infoDict = new Dictionary<string, string>();
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
