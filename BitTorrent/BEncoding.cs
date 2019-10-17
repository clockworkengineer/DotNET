using System;
using System.Collections.Generic;

namespace BitTorrent
{
    public class Bencoding
    {
        public class BNodeBase
        {
            
        }

        public class BNodeDictionary : BNodeBase
        {
            Dictionary<string, BNodeBase> dict;
        }

        public class BNodeList : BNodeBase
        {

            List<BNodeBase> list;
        }

        public class BNodeNumber : BNodeBase
        {
            byte[] number;
        }

        public class BNodeString: BNodeBase
        {
            byte[] str;
        }

      
        static public BNodeBase encode(byte[] buffer)
        {
            return (null);
        }

        static public byte[] decode(BNodeBase BNode)
        {
            return (null);
        }

    }
}
