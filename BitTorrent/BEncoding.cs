using System;
using System.Collections.Generic;
using System.Text;

namespace BitTorrent
{
    public class Bencoding
    {
        public class BNodeBase
        {
            
        }

        public class BNodeDictionary : BNodeBase
        {
            public Dictionary<string, BNodeBase> dict;

            public  BNodeDictionary()
            {
                dict = new Dictionary<string, BNodeBase>();
            }
        }

        public class BNodeList : BNodeBase
        {

            public List<BNodeBase> list;

            public BNodeList()
            {
                list = new List<BNodeBase>();
            }
        }

        public class BNodeNumber : BNodeBase
        {
            public byte[] number;
        }

        public class BNodeString: BNodeBase
        {
            public byte[] str;
        }


        static public byte[] extractString(byte[] buffer, ref int position, int length)
        {

            int end = position;

            while (buffer[end] != ':')
            {
                end++;
            }

            byte[] lengthBytes = new byte[end - position];
            Buffer.BlockCopy(buffer, position, lengthBytes, 0, end - position);

            int lengthOfString = int.Parse(Encoding.ASCII.GetString(lengthBytes));

            if (end + lengthOfString > length)
            {
                lengthOfString = length - end - 1;
            }

            position = end + lengthOfString + 1;

            byte[] retBuffer = new byte[lengthOfString];
            Buffer.BlockCopy(buffer, end + 1, retBuffer, 0, lengthOfString);

            return (retBuffer);

        }

        static public BNodeBase decodeNumber(byte[] buffer, ref int position, int length)
        {
            BNodeNumber bNode = new BNodeNumber();

            int end = position;
            while (buffer[end] != (byte) 'e')
            {
                end++;
            }
            bNode.number = new byte[end - position];
            Buffer.BlockCopy(buffer, position, bNode.number, 0, end - position);

            position = end + 1;

            return (bNode);

        }

        static public BNodeBase decodeString(byte[] buffer, ref int position, int length)
        {
            BNodeString bNode = new BNodeString();

            bNode.str = extractString(buffer, ref position, length);

            return (bNode);

        }

        static public string decodeKey(byte[] buffer, ref int position, int length)
        {
            string key = Encoding.ASCII.GetString(extractString(buffer, ref position, length));

            return (key);

        }

        static public BNodeBase decodeList(byte[] buffer, ref int position, int length)
        {

            BNodeList bNode = new BNodeList();

            while (buffer[position] != (byte)'e')
            {

                switch ((char)buffer[position])
                {
                    case 'd':
                        position++;
                        bNode.list.Add(decodeDictionary(buffer, ref position, length));
                        break;

                    case 'l':
                        position++;
                        bNode.list.Add(decodeList(buffer, ref position, length));
                        break;

                    case 'i':
                        position++;
                        bNode.list.Add(decodeNumber(buffer, ref position, length));
                        break;

                    default:
                        bNode.list.Add(decodeString(buffer, ref position, length));
                        break;

                }

            }

            return (bNode);

        }

        static public BNodeBase decodeDictionary(byte[] buffer, ref int position, int length)
        {

            BNodeDictionary bNode = new BNodeDictionary();

            while (buffer[position] != (byte)'e')
            {
                string key = decodeKey(buffer, ref position, length);

                switch ((char)buffer[position])
                {
                    case 'd':
                        position++;
                        bNode.dict[key] = decodeDictionary(buffer, ref position, length);
                        break;

                    case 'l':
                        position++;
                        bNode.dict[key] = decodeList(buffer, ref position, length);
                        break;

                    case 'i':
                        position++;
                        bNode.dict[key] = decodeNumber(buffer, ref position, length);
                        break;

                    default:
                        bNode.dict[key] = decodeString(buffer, ref position, length);
                        break;
                }

            }
    
            return (bNode);

        }
        
        public static BNodeBase decodeBNodes(byte[] buffer, ref int position, int length)
        {
            BNodeBase bNode=null;

            while (buffer[position]!=(byte) 'e')
            {
                switch((char) buffer[position])
                {
                    case 'd':
                        position++;
                        bNode = decodeDictionary(buffer, ref position, length);
                        break;

                    case 'l':
                        position++;
                        bNode = decodeList(buffer, ref position, length);
                        break;

                    case 'i':
                        position++;
                        bNode = decodeNumber(buffer, ref position, length);
                        break;

                    default:
                        bNode = decodeString(buffer, ref position, length);
                        break;

                }
            }

            return (bNode);

        }

        public static BNodeBase decode(byte[] buffer)
        {
            int position = 0;

            BNodeBase bNodeRoot = decodeBNodes(buffer, ref position, buffer.Length);

            return (bNodeRoot);

        }

        static public string decode (BNodeBase bNode)
        {
            string result = string.Empty;

            if (bNode is BNodeDictionary)
            {
                result += "d";
                foreach (var key in ((BNodeDictionary) bNode).dict.Keys)
                {
                    result += $"{key.Length}:{key}";
                    result += decode(((BNodeDictionary)bNode).dict[key]);
                }
                result += "e";
            } else if (bNode is BNodeList) {
                result += "l";
                foreach (var node in ((BNodeList)bNode).list)
                {
                    result += decode(node);
                }
                result += "e";
            }
            else if (bNode is BNodeNumber)
            {
                result += "i";
                result += Encoding.ASCII.GetString(((BNodeNumber)bNode).number);
                result += "e";
            }
            else if(bNode is BNodeString)
            {
                result  += $"{((BNodeString)bNode).str.Length}:{Encoding.ASCII.GetString(((BNodeString)bNode).str)}";
            }

            return (result);
        }

    }
}
