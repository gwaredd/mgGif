using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
        ushort TransparentColour;
        Color Background;
        Color[] ColourTable;

        Dictionary<int, List<ushort>> CodeTable;

        private static int ReadNextCode( BitArray array, int offset, int codeSize )
        {
            // NB: do we need to account for endianess?

            int v = 0;

            if( offset + codeSize > array.Count )
            {
                return 0;
            }

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
            NextSize  = (int) Math.Pow( 2, CodeSize );
            CodeTable = Enumerable.Range( 0, MaximumCodeSize + 2 ).ToDictionary(
                    i => i,
                    i => new List<ushort>() { (ushort) i }
                );
        }

        public Color GetColour( ushort code )
        {
            if( code == TransparentColour )
            {
                return Color.clear;
            }

            return code < ColourTable.Length ? ColourTable[code] : Background;
        }

        public Color[] Decompress( GifData gif, byte[] data, GifData.Image img )
        {
            ColourTable = img.ColourTable != null ? img.ColourTable : gif.ColourTable;

            MinimumCodeSize = img.LzwMinimumCodeSize;
            MaximumCodeSize = (int) Math.Pow( 2, MinimumCodeSize );
            ClearCode       = MaximumCodeSize;
            EndCode         = ClearCode + 1;

            TransparentColour = gif.TransparentColour;
            Background = gif.Background;

            ClearCodeTable();

            var input  = new BitArray( data );
            var output = new Color[ gif.Width * gif.Height ];
            var writePos = 0;

            // LZW decode loop

            var position = 0;
            var previousCode = -1;

            while( position < input.Length )
            {
                int curCode = ReadNextCode( input, position, CodeSize );
                position += CodeSize;

                if( curCode == ClearCode )
                {
                    ClearCodeTable();
                    previousCode = -1;
                    continue;
                }
                else if( curCode == EndCode )
                {
                    break;
                }
                else if( CodeTable.ContainsKey( curCode ) )
                {
                    var codes = CodeTable[ curCode ];

                    foreach( var code in codes )
                    {
                        output[ writePos++ ] = GetColour( code );
                    }

                    if( previousCode >= 0 )
                    {
                        var newCodes = new List<ushort>( CodeTable[ previousCode ] );
                        newCodes.Add( codes[0] );
                        CodeTable[ CodeTable.Count ] = newCodes;
                    }
                }
                else if( curCode >= CodeTable.Count )
                {
                    if( previousCode < 0 )
                    {
                        continue;
                    }

                    var codes = CodeTable[ previousCode ];

                    foreach( var code in codes )
                    {
                        output[writePos++] = GetColour( code );
                    }

                    output[writePos++] = GetColour( codes[0] );

                    var newCodes = new List<ushort>( CodeTable[ previousCode ] );
                    newCodes.Add( codes[0] );
                    CodeTable[ CodeTable.Count ] = newCodes;
                }
                else
                {
                    Debug.LogWarning( $"Unexpected code {curCode}" );
                    continue;
                }

                if( writePos >= output.Length )
                {
                    break;
                }

                previousCode = curCode;

                if( CodeTable.Count >= NextSize && CodeSize < 12 )
                {
                    CodeSize++;
                    NextSize = (int) Math.Pow( 2, CodeSize );
                }
            }

            while( writePos < output.Length )
            {
                output[writePos++] = Color.clear;
            }

            return output;
        }
    }
}
