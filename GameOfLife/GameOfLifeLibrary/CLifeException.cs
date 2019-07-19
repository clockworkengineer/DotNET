//
// Author: Robert Tizzard
// 
// Class: CLife
//
// Description: Exception class for Clife base and derived classes.
// 
// Copyright 2019.
//

using System;
namespace GameOfLifeLibrary
{
    public class CLifeException : Exception
    {

        public CLifeException() : base() { }
        public CLifeException(string message) : base(message) { }
        public CLifeException(string message, System.Exception inner) : base(message, inner) { }
        protected CLifeException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }


    }
}
