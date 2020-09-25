using System;

namespace BitTorrentLibrary
{
  public interface IAnnouncer
    {
        AnnounceResponse Announce(Tracker tracker);
    }
}
