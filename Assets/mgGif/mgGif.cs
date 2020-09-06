using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

using BufferType = System.UInt64;

namespace MG.GIF
{
    public enum Disposal
    {
        None              = 0x00,
        DoNotDispose      = 0x04,
        RestoreBackground = 0x08,
        ReturnToPrevious  = 0x0C
    }

    public class Image
    {
        public int       Width;
        public int       Height;
        public Color32[] RawImage;
        public int       Delay; // ms
        public Disposal  DisposalMethod = Disposal.None;

        public Texture2D CreateTexture()
        {
            var tex = new Texture2D( Width, Height, TextureFormat.ARGB32, false );

            tex.filterMode = FilterMode.Point;
            tex.wrapMode   = TextureWrapMode.Clamp;

            tex.SetPixels32( RawImage );
            tex.Apply();

            return tex;
        }
    }

    public class ImageList
    {
        public string Version;
        public ushort Width;
        public ushort Height;

        public List<Image> Images = new List<Image>();

        public void Add( Image img )
        {
            Images.Add( img );
        }

        public Image GetImage( int index )
        {
            return index < Images.Count ? Images[index] : null;
        }

        public int NumFrames
        {
            get
            {
                int count = 0;

                foreach( var img in Images )
                {
                    if( img.Delay > 0 )
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public Image GetFrame( int index )
        {
            if( Images.Count == 0 )
            {
                return null;
            }

            foreach( var img in Images )
            {
                if( img.Delay > 0 )
                {
                    if( index == 0 )
                    {
                        return img;
                    }

                    index--;
                }
            }

            return Images[Images.Count - 1];
        }
    }

    public class Decoder
    {
        [Flags]
        private enum ImageFlag
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

        private ImageList   Images;

        // colour
        private Color32[]   GlobalColourTable = new Color32[ 4096 ];
        private Color32[]   LocalColourTable  = new Color32[ 4096 ];
        private Color32[]   ActiveColourTable = null;
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

        private void ReadColourTable( Color32[] colourTable, ImageFlag flags, BinaryReader r )
        {
            var tableSize = Pow2[ (int)( flags & ImageFlag.TableSizeMask ) + 1 ];

            for( var i = 0; i < tableSize; i++ )
            {
                colourTable[ i ] = new Color32(
                    r.ReadByte(),
                    r.ReadByte(),
                    r.ReadByte(),
                    0xFF
                );
            }
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

            Images.Width  = r.ReadUInt16();
            Images.Height = r.ReadUInt16();

            var flags     = (ImageFlag) r.ReadByte();
            var bgIndex   = r.ReadByte();

            r.ReadByte(); // aspect ratio

            if( flags.HasFlag( ImageFlag.ColourTable ) )
            {
                ReadColourTable( GlobalColourTable, flags, r );

                if( bgIndex < GlobalColourTable.Length )
                {
                    BackgroundColour = GlobalColourTable[ bgIndex ];
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

            ControlDispose = (Disposal) ( flags & 0x1C );
            ControlDelay   = r.ReadUInt16();

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

            ImageLeft       = r.ReadUInt16();
            ImageTop        = r.ReadUInt16();
            ImageWidth      = r.ReadUInt16();
            ImageHeight     = r.ReadUInt16();

            var flags       = (ImageFlag) r.ReadByte();
            var interlaced  = flags.HasFlag( ImageFlag.Interlaced );

            if( ImageWidth == 0 || ImageHeight == 0 )
            {
                return;
            }

            if( flags.HasFlag( ImageFlag.ColourTable ) )
            {
                ReadColourTable( LocalColourTable, flags, r );
                ActiveColourTable = LocalColourTable;
            }
            else
            {
                ActiveColourTable = GlobalColourTable;
            }

            LzwMinimumCodeSize = r.ReadByte();

            if( LzwMinimumCodeSize > 11 )
            {
                LzwMinimumCodeSize = 11;
            }


            // compressed image data

            var (lzwData, totalBytes) = ReadImageBlocks( r );


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

            var img = new Image();

            img.Width          = Images.Width;
            img.Height         = Images.Height;
            img.Delay          = ControlDelay * 10; // (gif are in 1/100th second) convert to ms
            img.DisposalMethod = ControlDispose;
            img.RawImage       = DecompressLZW( lzwData, totalBytes );

            if( interlaced )
            {
                img.RawImage = Deinterlace( img.RawImage, ImageWidth );
            }

            Images.Add( img );
        }


        //------------------------------------------------------------------------------

        private Tuple<BufferType[],int> ReadImageBlocks( BinaryReader r )
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

            var buffer = new BufferType[ ( totalBytes + sizeof(BufferType) - 1 ) / sizeof(BufferType) ];
            r.BaseStream.Seek( startPos, SeekOrigin.Begin );

            var offset = 0;
            blockSize = r.ReadByte();

            while( blockSize != 0x00 )
            {
                Buffer.BlockCopy( r.ReadBytes( blockSize ), 0, buffer, offset, blockSize );
                offset += blockSize;
                blockSize = r.ReadByte();
            }

            return Tuple.Create( buffer, totalBytes );
        }

        //------------------------------------------------------------------------------
        // LZW
        // the code spends 95% of the time here so optimised for performance using 
        // pre-allocated buffers (cut down on allocation overhead)

        int         LzwClearCode;
        int         LzwEndCode;
        int         LzwCodeSize;
        int         LzwNextSize;
        int         LzwMaximumCodeSize;

        int         LzwNumCodes      = 0;
        int[]       LzwCodeIndices   = new int[ 4098 ];             // codes can be upto 12 bytes long, this is the maximum number of possible codes (2^12 + 2 for clear and end code)
        ushort[]    LzwCodeBuffer    = new ushort[ 64 * 1024 ];     // 64k buffer for codes - should be plenty but we dynamically resize if required
        int         LzwCodeBufferLen = 0;                           // end of data (next write position)
        int[]       Pow2             = { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096 };

        Color32[]   OutputBuffer;


        //------------------------------------------------------------------------------
        // decompress LZW data and write colours to OutputBuffer
        // Optimsed for performance
        // LzwCodeSize setup before call
        // OutputBuffer should be initialised before hand with default values (so despose and transparency works correctly)

        private Color32[] DecompressLZW( BufferType[] lzwData, int totalBytes )
        {
            // setup codes

            LzwCodeSize        = LzwMinimumCodeSize + 1;
            LzwNextSize        = Pow2[ LzwCodeSize ];
            LzwMaximumCodeSize = Pow2[ LzwMinimumCodeSize ];
            LzwClearCode       = LzwMaximumCodeSize;
            LzwEndCode         = LzwClearCode + 1;

            // initialise buffers

            LzwCodeBufferLen   = 0;
            LzwNumCodes        = LzwMaximumCodeSize + 2;

            // write initial code sequences

            for( ushort i = 0; i < LzwNumCodes; i++ )
            {
                LzwCodeIndices[ i ] = LzwCodeBufferLen;
                LzwCodeBuffer[ LzwCodeBufferLen++ ] = 1; // length
                LzwCodeBuffer[ LzwCodeBufferLen++ ] = i; // code
            }


            // LZW decode loop

            int rowBase   = ( Images.Height - ImageTop - 1 ) * Images.Width;
            int curCol    = ImageLeft;
            int rightEdge = ImageLeft + ImageWidth;

            const uint  NoCode            = 0xFFFF;
            uint        previousCode      = NoCode;    // last code processed
            int         bitsAvailable     = 0;         // number of bits available to read in the shift register
            int         lzwDataPos        = 0;         // next read position from the input stream
            uint        mask              = (uint) ( LzwNextSize - 1 );
            BufferType  shiftRegister     = 0;         // shift register holds the bytes coming in from the input stream, we shift down by the number of bits

            while( lzwDataPos != lzwData.Length || bitsAvailable > 0 )
            {
                // get next code

                uint curCode = (uint)( shiftRegister & mask );
                bitsAvailable -= LzwCodeSize;
                shiftRegister >>= LzwCodeSize;

                if( bitsAvailable <= 0 && lzwDataPos < lzwData.Length )
                {
                    shiftRegister = lzwData[ lzwDataPos++ ];
                    var numBits = 8 * ( lzwDataPos < lzwData.Length || totalBytes % sizeof(BufferType) == 0 ? sizeof(BufferType) : ( totalBytes % sizeof(BufferType) ) );

                    if( bitsAvailable < 0 )
                    {
                        var bitsRead = LzwCodeSize + bitsAvailable;
                        curCode |= ( (uint) shiftRegister << bitsRead ) & mask;

                        shiftRegister >>= -bitsAvailable;
                        bitsAvailable = numBits + bitsAvailable;
                    }
                    else
                    {
                        bitsAvailable = numBits;
                    }
                }


                // process code

                bool plusOne   = false;
                int  bufferPos = 0;

                if( curCode == LzwClearCode )
                {
                    // reset codes
                    LzwCodeSize = LzwMinimumCodeSize + 1;
                    LzwNextSize = Pow2[ LzwCodeSize ];
                    LzwNumCodes = LzwMaximumCodeSize + 2;

                    // reset buffer write pos
                    LzwCodeBufferLen = LzwNumCodes * 2;

                    // clear previous code
                    previousCode = NoCode;
                    mask         = (uint) ( LzwNextSize - 1 );

                    continue;
                }
                else if( curCode == LzwEndCode )
                {
                    // stop
                    break;
                }
                else if( curCode < LzwNumCodes )
                {
                    // write existing code
                    bufferPos = LzwCodeIndices[ curCode ];
                }
                else if( previousCode != NoCode )
                {
                    // write previous code
                    bufferPos = LzwCodeIndices[ previousCode ];
                    plusOne = true;
                }
                else
                {
                    continue;
                }


                // output colours

                var codeLength = LzwCodeBuffer[ bufferPos++ ];
                var newCode    = LzwCodeBuffer[ bufferPos ];

                for( int i = 0; i < codeLength; i++ )
                {
                    var code = LzwCodeBuffer[ bufferPos++ ];

                    if( code != TransparentIndex && curCol < Images.Width )
                    {
                        OutputBuffer[ rowBase + curCol ] = ActiveColourTable[ code ];
                    }

                    curCol = ++curCol % rightEdge;

                    if( curCol == 0 )
                    {
                        curCol = ImageLeft;
                        rowBase -= Images.Width;

                        if( rowBase < 0 )
                        {
                            return OutputBuffer;
                        }
                    }
                }

                if( plusOne )
                {
                    if( newCode != TransparentIndex && curCol < Images.Width )
                    {
                        OutputBuffer[ rowBase + curCol ] = ActiveColourTable[ newCode ];
                    }

                    curCol = ++curCol % rightEdge;

                    if( curCol == 0 )
                    {
                        curCol = ImageLeft;
                        rowBase -= Images.Width;

                        if( rowBase < 0 )
                        {
                            return OutputBuffer;
                        }
                    }
                }


                // create new code

                if( previousCode != NoCode && LzwNumCodes != LzwCodeIndices.Length )
                {
                    // get previous code from buffer

                    bufferPos  = LzwCodeIndices[ previousCode ];
                    codeLength = LzwCodeBuffer[ bufferPos++ ];

                    // resize buffer if required (should be rare)

                    if( LzwCodeBufferLen + codeLength + 1 >= LzwCodeBuffer.Length )
                    {
                        Array.Resize( ref LzwCodeBuffer, LzwCodeBuffer.Length * 2 );
                    }

                    // add new code

                    LzwCodeIndices[ LzwNumCodes++ ]     = LzwCodeBufferLen;
                    LzwCodeBuffer[ LzwCodeBufferLen++ ] = (ushort) ( codeLength + 1 );

                    // write previous code sequence

                    for( int i=0; i < codeLength; i++ )
                    {
                        LzwCodeBuffer[ LzwCodeBufferLen++ ] = LzwCodeBuffer[ bufferPos + i ];
                    }

                    // append new code

                    LzwCodeBuffer[ LzwCodeBufferLen++ ] = newCode;
                }

                // increase code size?

                if( LzwNumCodes >= LzwNextSize && LzwCodeSize < 12 )
                {
                    LzwNextSize = Pow2[ ++LzwCodeSize ];
                    mask = (uint) ( LzwNextSize - 1 );
                }

                // remeber last code processed
                previousCode = curCode;
            }

            return OutputBuffer;
        }
    }
}
