using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MG.GIF
{
    public class DecompressLZW
    {
        int ClearCode;
        int EndCode;
        int CodeSize;
        int NextSize;
        int MaximumCodeSize;
        int MinimumCodeSize;

        Dictionary<int, List<byte>> CodeTable;

        private static int ReadNextCode( BitArray array, int offset, int codeSize )
        {
            // NB: do we need to account for endianess?

            int v = 0;

            for( int i = 0; i < codeSize; i++ )
            {
                if( array.Get( offset + i ) )
                {
                    v |= 1 << i;
                }
            }

            return v;
        }

        private void ClearCodeTable()
        {
            CodeSize  = MinimumCodeSize + 1;
            NextSize = (int) Math.Pow( 2, CodeSize );
            CodeTable = Enumerable.Range( 0, MaximumCodeSize ).ToDictionary(
                i => i,
                i => new List<byte>() { (byte)i }
            );
        }

        public byte[] Decompress( GifData.Image img )
        {
            MinimumCodeSize = img.LzwMinimumCodeSize;
            MaximumCodeSize = (int) Math.Pow( 2, MinimumCodeSize );
            ClearCode       = MaximumCodeSize;
            EndCode         = ClearCode + 1;

            ClearCodeTable();

            var input  = new BitArray( img.Data );
            var output = new byte[ img.Width * img.Height ];
            var writePos = 0;

            // LZW decode loop

            var position = 0;
            var lastCode = -1;

            while( position < input.Length )
            {
                int code = ReadNextCode( input, position, CodeSize );
                position += CodeSize;

                if( code == ClearCode )
                {
                    ClearCodeTable();
                    lastCode = -1;
                    continue;
                }
                else if( code == EndCode )
                {
                    break;
                }
                else if( CodeTable.ContainsKey( code ) )
                {
                    foreach( var v in CodeTable[ code ] )
                    {
                        output[writePos++] = v;
                    }
                }
                else if( code > CodeTable.Count )
                {
                    if( lastCode >= 0 && CodeTable.ContainsKey( lastCode ) )
                    {
                        foreach( var v in CodeTable[ lastCode ] )
                        {
                            output[ writePos++ ] = v;
                        }

                        output[writePos++] = CodeTable[lastCode][0];
                    }
                    else
                    {
                        // TODO: error
                        continue;
                    }
                }
                else
                {
                    // TODO: error
                    continue;
                }

                if( writePos >= output.Length )
                {
                    break;
                }

                if( lastCode >= 0 && CodeTable.ContainsKey( lastCode ) )
                {
                    var entry = new List<byte>( CodeTable[ lastCode ] );
                    entry.Add( CodeTable[code][0] );
                    CodeTable[CodeTable.Count] = entry;
                }

                lastCode = code;

                if( CodeSize < 12 && CodeTable.Count >= NextSize )
                {
                    CodeSize++;
                    NextSize = (int) Math.Pow( 2, CodeSize );
                }
            }

            return output;
        }
    }
}
