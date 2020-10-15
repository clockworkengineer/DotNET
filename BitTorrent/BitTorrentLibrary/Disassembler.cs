// //
// // Author: Robert Tizzard
// //
// // Library: C# class library to implement the BitTorrent protocol.
// //
// // Description: WORK IN PROGRESS.
// //
// // Copyright 2020.
// //

// using System;
// using System.Threading;
// using System.Linq;
// using System.Collections.Generic;

// namespace BitTorrentLibrary
// {
//     public class Disassembler
//     {
//         private readonly Random _pieceGenerator = new Random();
//         public UInt32 ActiveDisassemblerTasks { get; set; }
//         private UInt32[] PieceSuggestions(Peer remotePeer, UInt32 numberOfSuggestions)
//         {
//             HashSet<UInt32> suggestions = new HashSet<uint>();

//             while (numberOfSuggestions-- > 0)
//             {
//                 while (true)
//                 {
//                     UInt32 suggestion = (UInt32)_pieceGenerator.Next(0, (int)remotePeer.Dc.NumberOfPieces);
//                     if (remotePeer.Dc.IsPieceLocal(suggestion) && !suggestions.Contains(suggestion))
//                     {
//                         suggestions.Add(suggestion);
//                         break;
//                     }
//                 }
//             }

//             return (suggestions.ToArray());

//         }

//         public Disassembler(Downloader downloader)
//         {

//         }

//         public void DisassemlePieces(Peer remotePeer)
//         {

//             ActiveDisassemblerTasks++;

//             PWP.Bitfield(remotePeer, remotePeer.Dc.BuildPieceBitfield());
//             PWP.Uninterested(remotePeer);

//             foreach (var suggestion in PieceSuggestions(remotePeer, 10)) {
//                 PWP.Have(remotePeer, suggestion);
//             }

//             PWP.Unchoke(remotePeer);
            
//             while (true)
//             {
//                 Thread.Sleep(1000);
//             }
            
//             ActiveDisassemblerTasks--;

//         }
//     }
// }
