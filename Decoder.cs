using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace MG.GIF
{
    public class Decoder
    {
        [Flags]
        private enum Flag
        {
            Interlaced    = 0x40,
            ColourTable   = 0x80,
            TableSizeMask = 0x07,
            BitDepthMask  = 0x70,
        }

        private enum Block
        {
            Image     = 0x2C,
            Extension = 0x21,
            End       = 0x3B
        }

        private enum Extension
        {
            GraphicControl  = 0xF9,
            Comments        = 0xFE,
            PlainText       = 0x01,
            ApplicationData = 0xFF
        }

        ImageList Images;

        public  Color[]     GlobalColourTable = null;
        public  Color       BackgroundColour  = Color.black;
        public  byte        BackgroundIndex   = 0xFF;
        public  ushort      TransparentIndex  = 0xFFFF;

        private ushort      ControlDelay      = 0;
        public  Disposal    ControlDispose    = Disposal.None;

        public ushort   ImageLeft;
        public ushort   ImageTop;
        public ushort   ImageWidth;
        public ushort   ImageHeight;
        public bool     ImageInterlaced;
        public byte     LzwMinimumCodeSize;


        //------------------------------------------------------------------------------

        public static ImageList Parse( byte[] data )
        {
            try
            {
                return new Decoder().Decode( data );
            }
            catch( Exception e )
            {
                Debug.Log( e.Message );
                return null;
            }
        }

        //------------------------------------------------------------------------------

        public ImageList Decode( byte[] data )
        {
            if( data == null || data.Length <= 12 )
            {
                throw new Exception( "Invalid data" );
            }

            Images = new ImageList();

            using( var r = new BinaryReader( new MemoryStream( data ) ) )
            {
                ReadHeader( r );
                ReadBlocks( r );
            }

            return Images;
        }

        //------------------------------------------------------------------------------

        private Color[] ReadColourTable( Flag flags, BinaryReader r )
        {
            var tableSize   = (int) Math.Pow( 2, (int)( flags & Flag.TableSizeMask ) + 1 );
            var colourTable = new Color[ tableSize ];

            for( var i = 0; i < tableSize; i++ )
            {
                colourTable[i] = new Color(
                      r.ReadByte() / 255.0f,
                      r.ReadByte() / 255.0f,
                      r.ReadByte() / 255.0f
                );
            }

            return colourTable;
        }

        //------------------------------------------------------------------------------

        protected void ReadHeader( BinaryReader r )
        {
            // signature

            Images.Version = new string( r.ReadChars( 6 ) );

            if( Images.Version != "GIF87a" && Images.Version != "GIF89a" )
            {
                throw new Exception( "Unsupported GIF version" );
            }

            // read header

            Images.Width    = r.ReadUInt16();
            Images.Height   = r.ReadUInt16();
            var flags       = (Flag) r.ReadByte();
            BackgroundIndex = r.ReadByte();
            r.ReadByte(); // aspect ratio

            Images.BitDepth = (int) ( flags & Flag.BitDepthMask ) >> 4 + 1;

            if( flags.HasFlag( Flag.ColourTable ) )
            {
                GlobalColourTable = ReadColourTable( flags, r );

                if( BackgroundIndex < GlobalColourTable.Length )
                {
                    BackgroundColour = GlobalColourTable[BackgroundIndex];
                }
            }
        }

        //------------------------------------------------------------------------------

        protected void ReadBlocks( BinaryReader r )
        {
            while( true )
            {
                var block = (Block) r.ReadByte();

                switch( block )
                {
                    case Block.Image:
                        ReadImageBlock( r );
                        break;

                    case Block.Extension:

                        var ext = (Extension) r.ReadByte();

                        switch( ext )
                        {
                            case Extension.GraphicControl:
                                ReadControlBlock( r );
                                break;

                            default:
                                SkipBlock( r );
                                break;
                        }

                        break;

                    case Block.End:
                        return;

                    default:
                        throw new Exception( "Unexpected block type" );
                }
            }
        }

        //------------------------------------------------------------------------------

        private void SkipBlock( BinaryReader r )
        {
            var blockSize = r.ReadByte();

            while( blockSize != 0x00 )
            {
                r.BaseStream.Seek( blockSize, SeekOrigin.Current );
                blockSize = r.ReadByte();
            }
        }


        //------------------------------------------------------------------------------

        private void ReadControlBlock( BinaryReader r )
        {
            var blockSize = r.ReadByte();

            var flags = r.ReadByte();

            switch( flags & 0x1C )
            {
                case 0x04:
                    ControlDispose = Disposal.DoNotDispose;
                    break;
                case 0x08:
                    ControlDispose = Disposal.RestoreBackground;
                    break;
                case 0x0C:
                    ControlDispose = Disposal.ReturnToPrevious;
                    break;
                default:
                    ControlDispose = Disposal.None;
                    break;
            }

            ControlDelay = r.ReadUInt16();

            var hasTransparentColour = ( flags & 0x01 ) == 0x01;
            var transparentColour = r.ReadByte();

            if( hasTransparentColour )
            {
                TransparentIndex = transparentColour;
            }
            else
            {
                TransparentIndex = 0xFFFF;
            }

            r.ReadByte(); // terminator
        }

        //------------------------------------------------------------------------------

        protected Color[] Deinterlace( Color[] input, int width )
        {
            var output = new Color[ input.Length ];

            var numRows = input.Length / width;

            var rows = new int[ numRows ];

            for( var r = 0; r < numRows; r++ )
            {
                // every 8th row starting at 0
                if( r % 8 == 0 )
                {
                    rows[r] = r / 8;
                }
                // every 8th row starting at 4
                else if( ( r + 4 ) % 8 == 0 )
                {
                    var o = numRows / 8;
                    rows[r] = o + ( r - 4 ) / 8;
                }
                // every 4th row starting at 2
                else if( ( r + 2 ) % 4 == 0 )
                {
                    var o = numRows / 4;
                    rows[r] = o + ( r - 2 ) / 4;
                }
                // every 2nd row starting at 1
                else // if( ( r + 1 ) % 2 == 0 )
                {
                    var o = numRows / 2;
                    rows[r] = o + ( r - 1 ) / 2;
                }
            }

            int writePos=0;

            foreach( var row in rows )
            {
                Array.Copy( input, row * width, output, writePos, width );
                writePos += width;
            }

            return output;
        }

        protected void ReadImageBlock( BinaryReader r )
        {
            var img = new Image();

            ImageLeft          = r.ReadUInt16();
            ImageTop           = r.ReadUInt16();
            ImageWidth         = r.ReadUInt16();
            ImageHeight        = r.ReadUInt16();
            img.Delay          = ControlDelay;
            img.DisposalMethod = ControlDispose;

            var flags = (Flag) r.ReadByte();
            ImageInterlaced = flags.HasFlag( Flag.Interlaced );

            if( ImageWidth == 0 || ImageHeight == 0 )
            {
                return;
            }

            Color[] colourTable = null;

            if( flags.HasFlag( Flag.ColourTable ) )
            {
                colourTable = ReadColourTable( flags, r );
            }

            ColourTable = colourTable ?? GlobalColourTable;

            LzwMinimumCodeSize = r.ReadByte();

            var data = ReadImageBlocks( r );

            // copy background colour?
            Color[] prevImg = null;

            switch( ControlDispose )
            {
                case Disposal.None:
                case Disposal.DoNotDispose:

                    {
                        var prev = Images.Images.Count > 0 ? Images.Images[ Images.Images.Count - 1 ] : null;

                        if( prev?.RawImage != null )
                        {
                            prevImg = prev.RawImage;
                        }
                    }

                    break;


                case Disposal.ReturnToPrevious:

                    for( int i= Images.Images.Count - 1; i >= 0; i-- )
                    {
                        var prev = Images.Images[ i ];

                        if( prev.DisposalMethod == Disposal.None || prev.DisposalMethod == Disposal.DoNotDispose )
                        {
                            prevImg = prev.RawImage;
                            break;
                        }
                    }

                    break;

                case Disposal.RestoreBackground:
                default:
                    break;
            }

            img.RawImage = Decompress( data, img, prevImg );

            if( ImageInterlaced )
            {
                img.RawImage = Deinterlace( img.RawImage, ImageWidth );
            }

            Images.Images.Add( img );
        }


        //------------------------------------------------------------------------------

        private byte[] ReadImageBlocks( BinaryReader r )
        {
            var startPos = r.BaseStream.Position;

            // get total size

            var totalBytes = 0;
            var blockSize = r.ReadByte();

            while( blockSize != 0x00 )
            {
                totalBytes += blockSize;
                r.BaseStream.Seek( blockSize, SeekOrigin.Current );

                blockSize = r.ReadByte();
            }

            if( totalBytes == 0 )
            {
                return null;
            }

            // read bytes

            var buffer = new byte[ totalBytes ];
            r.BaseStream.Seek( startPos, SeekOrigin.Begin );

            var offset = 0;
            blockSize = r.ReadByte();

            while( blockSize != 0x00 )
            {
                r.Read( buffer, offset, blockSize );
                offset += blockSize;

                blockSize = r.ReadByte();
            }

            return buffer;
        }

        //------------------------------------------------------------------------------

        int LzwClearCode;
        int LzwEndCode;
        int LzwCodeSize;
        int LzwNextSize;
        int LzwMaximumCodeSize;
        ushort TransparentColour;

        Color Background;
        Color[] ColourTable;
        Color[] Output;
        int PixelNum;

        Dictionary<int, List<ushort>> LzwCodeTable;

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

        public Color GetColour( ushort code )
        {
            if( code == TransparentColour )
            {
                return Color.clear;
            }

            return code < ColourTable.Length ? ColourTable[code] : Background;
        }

        public void Write( ushort code )
        {
            var row = ImageTop + PixelNum / ImageWidth;
            var col = ImageLeft + PixelNum % ImageWidth;

            if( row < Images.Height && col < Images.Width )
            {
                var index = row * Images.Width + col;
                Output[index] = GetColour( code );
            }

            PixelNum++;
        }


        private void ClearCodeTable()
        {
            LzwCodeSize = LzwMinimumCodeSize + 1;
            LzwNextSize = (int) Math.Pow( 2, LzwCodeSize );
            LzwCodeTable = Enumerable.Range( 0, LzwMaximumCodeSize + 2 ).ToDictionary(
                    i => i,
                    i => new List<ushort>() { (ushort) i }
                );
        }

        public Color[] Decompress( byte[] data, Image img, Color[] prevImg )
        {
            LzwMaximumCodeSize = (int) Math.Pow( 2, LzwMinimumCodeSize );
            LzwClearCode       = LzwMaximumCodeSize;
            LzwEndCode         = LzwClearCode + 1;

            TransparentColour = TransparentIndex;
            Background = BackgroundColour;

            ClearCodeTable();

            var input = new BitArray( data );

            // copy background colour?

            if( prevImg != null )
            {
                Output = prevImg.Clone() as Color[];
            }
            else
            {
                Output = Enumerable.Repeat( Color.clear, Images.Width * Images.Height ).ToArray();
            }

            PixelNum = 0;

            // LZW decode loop

            var position = 0;
            var previousCode = -1;

            while( position < input.Length )
            {
                int curCode = ReadNextCode( input, position, LzwCodeSize );
                position += LzwCodeSize;

                if( curCode == LzwClearCode )
                {
                    ClearCodeTable();
                    previousCode = -1;
                    continue;
                }
                else if( curCode == LzwEndCode )
                {
                    break;
                }
                else if( LzwCodeTable.ContainsKey( curCode ) )
                {
                    var codes = LzwCodeTable[ curCode ];

                    foreach( var code in codes )
                    {
                        Write( code );
                    }

                    if( previousCode >= 0 )
                    {
                        var newCodes = new List<ushort>( LzwCodeTable[ previousCode ] );
                        newCodes.Add( codes[0] );
                        LzwCodeTable[LzwCodeTable.Count] = newCodes;
                    }
                }
                else if( curCode >= LzwCodeTable.Count )
                {
                    if( previousCode < 0 )
                    {
                        continue;
                    }

                    var codes = LzwCodeTable[ previousCode ];

                    foreach( var code in codes )
                    {
                        Write( code );
                    }

                    Write( codes[0] );

                    var newCodes = new List<ushort>( LzwCodeTable[ previousCode ] );
                    newCodes.Add( codes[0] );
                    LzwCodeTable[LzwCodeTable.Count] = newCodes;
                }
                else
                {
                    Debug.LogWarning( $"Unexpected code {curCode}" );
                    continue;
                }

                if( PixelNum >= Output.Length )
                {
                    break;
                }

                previousCode = curCode;

                if( LzwCodeTable.Count >= LzwNextSize && LzwCodeSize < 12 )
                {
                    LzwCodeSize++;
                    LzwNextSize = (int) Math.Pow( 2, LzwCodeSize );
                }
            }

            return Output;
        }

    }
}
