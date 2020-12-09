//
// Author: Rob Tizzard
//
// Description: XUnit tests for BiTorrent Assembler class.
//
// Copyright 2020.
//
using System;
using Xunit;
using BitTorrentLibrary;
namespace BitTorrentLibraryTests
{
    public class AsssemblerTests
    {
        [Fact]
        public void TestCreateAssembler()
        {
            try
            {
                Assembler aasembler = new Assembler();
            }
            catch (Exception ex)
            {
                Assert.True(false, "Should not throw execption here but it did. " + ex.Message);
            }
            Assert.True(true);
        }
        [Fact]
        public void TestPassNullConextToAssemblePieces()
        {
            Assembler assembler = new Assembler();
            Assert.Throws<ArgumentNullException>(() => assembler.AssemblePieces(null));
        }
    }
}