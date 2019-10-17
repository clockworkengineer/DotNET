﻿using System;
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


        static public byte[] extractString(byte[] buffer, ref int position)
        {

            int end = position;

            while (buffer[end] != ':')
            {
                end++;
            }

            byte[] lengthBytes = new byte[end - position];
            Buffer.BlockCopy(buffer, position, lengthBytes, 0, end - position);

            int lengthOfString = int.Parse(Encoding.ASCII.GetString(lengthBytes));

            if (end + lengthOfString > buffer.Length)
            {
                lengthOfString = buffer.Length - end - 1;
            }

            position = end + lengthOfString + 1;

            byte[] retBuffer = new byte[lengthOfString];
            Buffer.BlockCopy(buffer, end + 1, retBuffer, 0, lengthOfString);

            return (retBuffer);

        }

        static public BNodeBase decodeNumber(byte[] buffer, ref int position)
        {
            BNodeNumber bNode = new BNodeNumber();

            position++;

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

        static public BNodeBase decodeString(byte[] buffer, ref int position)
        {
            BNodeString bNode = new BNodeString();

            bNode.str = extractString(buffer, ref position);

            return (bNode);

        }

        static public string decodeKey(byte[] buffer, ref int position)
        {
            string key = Encoding.ASCII.GetString(extractString(buffer, ref position));

            return (key);

        }

        static public BNodeBase decodeList(byte[] buffer, ref int position)
        {

            BNodeList bNode = new BNodeList();

            position++;

            while (buffer[position] != (byte)'e')
            {

                switch ((char)buffer[position])
                {
                    case 'd':
                        bNode.list.Add(decodeDictionary(buffer, ref position));
                        break;

                    case 'l':
                        bNode.list.Add(decodeList(buffer, ref position));
                        break;

                    case 'i':
                        bNode.list.Add(decodeNumber(buffer, ref position));
                        break;

                    default:
                        bNode.list.Add(decodeString(buffer, ref position));
                        break;

                }

            }

            return (bNode);

        }

        static public BNodeBase decodeDictionary(byte[] buffer, ref int position)
        {

            BNodeDictionary bNode = new BNodeDictionary();

            position++;

            while (buffer[position] != (byte)'e')
            {
                string key = decodeKey(buffer, ref position);

                switch ((char)buffer[position])
                {
                    case 'd':
                        bNode.dict[key] = decodeDictionary(buffer, ref position);
                        break;

                    case 'l':
                        bNode.dict[key] = decodeList(buffer, ref position);
                        break;

                    case 'i':
                        bNode.dict[key] = decodeNumber(buffer, ref position);
                        break;

                    default:
                        bNode.dict[key] = decodeString(buffer, ref position);
                        break;
                }

            }
    
            return (bNode);

        }
        
        public static BNodeBase DecodeBNodes(byte[] buffer, ref int position)
        {
            BNodeBase bNode=null;

            while (buffer[position]!=(byte) 'e')
            {
                switch((char) buffer[position])
                {
                    case 'd':
                        bNode = decodeDictionary(buffer, ref position);
                        break;

                    case 'l':
                        bNode = decodeList(buffer, ref position);
                        break;

                    case 'i':
                        bNode = decodeNumber(buffer, ref position);
                        break;

                    default:
                        bNode = decodeString(buffer, ref position);
                        break;

                }
            }

            return (bNode);

        }

        public static BNodeBase decode(byte[] buffer)
        {
            int position = 0;

            BNodeBase bNodeRoot = DecodeBNodes(buffer, ref position);

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
                foreach (var node in ((BNodeList)bNode).list)
                {
                    result += "l";
                    result += decode(node);
                    result += "e";
                }
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