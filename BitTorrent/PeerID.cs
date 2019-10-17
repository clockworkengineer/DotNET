using System;
namespace BitTorrent
{
    public class PeerID
    {

        static public string get()
        {
            return("-AZ1000-"+ Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Substring(0, 12));
        }
    }
}
