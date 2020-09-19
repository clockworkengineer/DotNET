using System;
using System.Text;
using Xunit;
using BitTorrent;

namespace BitTorrentLibraryTests
{
    public class TrackerTests
    {
        [Fact]
        public void TestExceptionWhenNullFileAgentPassedIn()
        {
            Assert.Throws<NullReferenceException>( () => new Tracker(null));
        }
    }
}