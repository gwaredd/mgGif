using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.Profiling;
using System.Diagnostics;

namespace MG.GIF
{
    public class Decoder
    {
        [Flags]
        private enum ImageFlag
        {
            Interlaced = 0x40,
            ColourTable = 0x80,
            TableSizeMask = 0x07,
            BitDepthMask = 0x70,
        }

        private enum Block
        {
            Image = 0x2C,
            Extension = 0x21,
            End = 0x3B
        }

        private enum Extension
        {
            GraphicControl = 0xF9,
            Comments = 0xFE,
            PlainText = 0x01,
            ApplicationData = 0xFF
        }

        private ImageList   Images;

        // colour
        private Color32[]   GlobalColourTable = null;
        private Color32[]   ActiveColourTable;
        private Color32     BackgroundColour  = new Color32(0x00,0x00,0x00,0xFF);
        private Color32     ClearColour       = new Color32(0x00,0x00,0x00,0x00);
        private ushort      TransparentIndex  = 0xFFFF;

        // current controls
        private ushort      ControlDelay      = 0;
        private Disposal    ControlDispose    = Disposal.None;

        // current image
        private ushort      ImageLeft;
        private ushort      ImageTop;
        private ushort      ImageWidth;
        private ushort      ImageHeight;
        private ImageFlag   ImageFlags;
        private bool        ImageInterlaced;
        private byte        LzwMinimumCodeSize;


        //------------------------------------------------------------------------------

        public static ImageList Parse( byte[] data )
        {
            try
            {
                return new Decoder().Decode( data );
            }
            catch( Exception e )
            {
                UnityEngine.Debug.Log( e.Message );
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

        private Color32[] ReadColourTable( ImageFlag flags, BinaryReader r )
        {
            var tableSize   = (int) Math.Pow( 2, (int)( flags & ImageFlag.TableSizeMask ) + 1 );
            var colourTable = new Color32[ tableSize ];

            for( var i = 0; i < tableSize; i++ )
            {
                colourTable[i] = new Color32(
                    r.ReadByte(),
                    r.ReadByte(),
                    r.ReadByte(),
                    0xFF
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

            Images.Width = r.ReadUInt16();
            Images.Height = r.ReadUInt16();
            ImageFlags = (ImageFlag) r.ReadByte();
            var bgIndex     = r.ReadByte();
            r.ReadByte(); // aspect ratio

            Images.BitDepth = (int) ( ImageFlags & ImageFlag.BitDepthMask ) >> 4 + 1;

            if( ImageFlags.HasFlag( ImageFlag.ColourTable ) )
            {
                GlobalColourTable = ReadColourTable( ImageFlags, r );

                if( bgIndex < GlobalColourTable.Length )
                {
                    BackgroundColour = GlobalColourTable[bgIndex];
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

            // has transparent colour?

            var transparentColour = r.ReadByte();

            if( ( flags & 0x01 ) == 0x01 )
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

        protected Color32[] Deinterlace( Color32[] input, int width )
        {
            var output   = new Color32[ input.Length ];
            var numRows  = input.Length / width;
            var writePos = input.Length - width; // NB: work backwards due to Y-coord flip

            for( var row = 0; row < numRows; row++ )
            {
                var copyRow = 0;

                // every 8th row starting at 0
                if( row % 8 == 0 )
                {
                    copyRow = row / 8;
                }
                // every 8th row starting at 4
                else if( ( row + 4 ) % 8 == 0 )
                {
                    var o = numRows / 8;
                    copyRow = o + ( row - 4 ) / 8;
                }
                // every 4th row starting at 2
                else if( ( row + 2 ) % 4 == 0 )
                {
                    var o = numRows / 4;
                    copyRow = o + ( row - 2 ) / 4;
                }
                // every 2nd row starting at 1
                else // if( ( r + 1 ) % 2 == 0 )
                {
                    var o = numRows / 2;
                    copyRow = o + ( row - 1 ) / 2;
                }

                Array.Copy( input, ( numRows - copyRow - 1 ) * width, output, writePos, width );
                writePos -= width;
            }

            return output;
        }

        //------------------------------------------------------------------------------

        protected void ReadImageBlock( BinaryReader r )
        {
            // read image block header

            ImageLeft = r.ReadUInt16();
            ImageTop = r.ReadUInt16();
            ImageWidth = r.ReadUInt16();
            ImageHeight = r.ReadUInt16();
            ImageFlags = (ImageFlag) r.ReadByte();
            ImageInterlaced = ImageFlags.HasFlag( ImageFlag.Interlaced );

            if( ImageWidth == 0 || ImageHeight == 0 )
            {
                return;
            }

            if( ImageFlags.HasFlag( ImageFlag.ColourTable ) )
            {
                ActiveColourTable = ReadColourTable( ImageFlags, r );
            }
            else
            {
                ActiveColourTable = GlobalColourTable;
            }

            LzwMinimumCodeSize = r.ReadByte();


            // compressed image data

            var lzwData = ReadImageBlocks( r );


            // this disposal method determines whether we start with a previous image

            OutputBuffer = null;

            switch( ControlDispose )
            {
                case Disposal.None:
                case Disposal.DoNotDispose:
                    {
                        var prev = Images.Images.Count > 0 ? Images.Images[ Images.Images.Count - 1 ] : null;

                        if( prev?.RawImage != null )
                        {
                            OutputBuffer = prev.RawImage.Clone() as Color32[];
                        }
                    }
                    break;


                case Disposal.ReturnToPrevious:

                    for( int i = Images.Images.Count - 1; i >= 0; i-- )
                    {
                        var prev = Images.Images[ i ];

                        if( prev.DisposalMethod == Disposal.None || prev.DisposalMethod == Disposal.DoNotDispose )
                        {
                            OutputBuffer = prev.RawImage.Clone() as Color32[];
                            break;
                        }
                    }

                    break;

                case Disposal.RestoreBackground:
                default:
                    break;
            }

            if( OutputBuffer == null )
            {
                var size = Images.Width * Images.Height;

                OutputBuffer = new Color32[size];

                for( int i = 0; i < size; i++ )
                {
                    OutputBuffer[i] = ClearColour;
                }
            }

            // create image

            var img = new Image( Images );

            img.Delay = ControlDelay;
            img.DisposalMethod = ControlDispose;



            //var s = Sampler.Get( "DecompressLZW" );

            var sw = new Stopwatch();
            sw.Start();
            img.RawImage = DecompressLZW( lzwData );
            sw.Stop();
            UnityEngine.Debug.Log( sw.ElapsedTicks );

            if( ImageInterlaced )
            {
                img.RawImage = Deinterlace( img.RawImage, ImageWidth );
            }

            Images.Add( img );
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
        // LZW

        int LzwClearCode;
        int LzwEndCode;
        int LzwCodeSize;
        int LzwNextSize;
        int LzwMaximumCodeSize;
        Dictionary<int, List<ushort>> LzwCodeTable;

        //List<ushort>[] LzwCodeTable = new List<ushort>[ 4096 ]; // max code size = 12, 2^12 = 4096

        int         PixelNum;
        Color32[]   OutputBuffer;


        private int ReadNextCode( BitArray array, int offset, int codeSize )
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

        private void WritePixel( ushort code )
        {
            var row = ImageTop  + PixelNum / ImageWidth;
            var col = ImageLeft + PixelNum % ImageWidth;

            if( row < Images.Height && col < Images.Width )
            {
                // reverse row (flip in Y) because gif coordinates start at the top-left (unity is bottom-left)

                var index = ( Images.Height - row - 1 ) * Images.Width + col;

                if( code != TransparentIndex )
                {
                    OutputBuffer[index] = code < ActiveColourTable.Length ? ActiveColourTable[code] : BackgroundColour;
                }
            }

            PixelNum++;
        }

        private void ClearCodeTable()
        {
            LzwCodeSize  = LzwMinimumCodeSize + 1;
            LzwNextSize  = (int) Math.Pow( 2, LzwCodeSize );
            LzwCodeTable = new Dictionary<int, List<ushort>>();

            for( ushort i=0; i < LzwMaximumCodeSize + 2; i++ )
            {
                LzwCodeTable[i] = new List<ushort>() { i };
            }
        }

        private Color32[] DecompressLZW( byte[] lzwData )
        {
            LzwMaximumCodeSize = (int) Math.Pow( 2, LzwMinimumCodeSize );
            LzwClearCode       = LzwMaximumCodeSize;
            LzwEndCode         = LzwClearCode + 1;

            ClearCodeTable();

            // LZW decode loop

            PixelNum = 0;

            int previousCode = -1;

            int bitsAvailable  = 0;
            int readPos        = 0;
            uint shiftRegister = 0;

            while( readPos != lzwData.Length || bitsAvailable > 0 )
            {
                // get next code

                int bitsToRead = LzwCodeSize;

                // consume any existing bits

                if( bitsAvailable > 0 )
                {
                    var numBits = Mathf.Min( bitsToRead, bitsAvailable );
                    shiftRegister = shiftRegister >> numBits;
                    bitsToRead -= numBits;
                    bitsAvailable -= numBits;
                }

                // load up new bits

                if( bitsAvailable == 0 )
                {
                    if( readPos < lzwData.Length - 1 )
                    {
                        shiftRegister |= ( (uint) lzwData[readPos++] << 16 ) | ( (uint) lzwData[readPos++] << 24 );
                        bitsAvailable = 16;
                    }
                    else if( readPos < lzwData.Length )
                    {
                        shiftRegister |= (uint) lzwData[readPos++] << 16;
                        bitsAvailable = 8;
                    }
                }

                // consume any remaining bits

                if( bitsToRead > 0 )
                {
                    shiftRegister = shiftRegister >> bitsToRead;
                    bitsAvailable -= bitsToRead;
                }

                int curCode = (int)( ( shiftRegister & 0x0000FFFF ) >> ( 16 - LzwCodeSize ) );

                //

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
                        WritePixel( code );
                    }

                    if( previousCode >= 0 )
                    {
                        var newCodes = new List<ushort>( LzwCodeTable[ previousCode ] );
                        newCodes.Add( codes[0] );
                        LzwCodeTable[ LzwCodeTable.Count ] = newCodes;
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
                        WritePixel( code );
                    }

                    WritePixel( codes[0] );

                    var newCodes = new List<ushort>( LzwCodeTable[ previousCode ] );
                    newCodes.Add( codes[0] );
                    LzwCodeTable[ LzwCodeTable.Count ] = newCodes;
                }
                else
                {
                    UnityEngine.Debug.LogWarning( $"Unexpected code {curCode}" );
                    continue;
                }

                previousCode = curCode;

                // increase code size?

                if( LzwCodeTable.Count >= LzwNextSize && LzwCodeSize < 12 )
                {
                    LzwCodeSize++;
                    LzwNextSize = (int) Math.Pow( 2, LzwCodeSize );
                }
            }

            return OutputBuffer;
        }
    }
}
