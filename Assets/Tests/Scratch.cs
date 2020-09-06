using NUnit.Framework;
using System;
using UnityEngine;

namespace Tests
{
    public class Scratch
    {
        //[Test]
        public void ScratchTest()
        {
            var data = new byte[]{ 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };

            var ints = new int[ data.Length / sizeof( int ) ];

            Buffer.BlockCopy( data, 0, ints, 0, data.Length );

            Debug.Log( ( ints[0] & 0x000000FF ) >> 0 );
            Debug.Log( ( ints[0] & 0x0000FF00 ) >> 8 );
            Debug.Log( ( ints[0] & 0x00FF0000 ) >> 16 );
            Debug.Log( ( ints[0] & 0xFF000000 ) >> 24 );
        }
    }
}
