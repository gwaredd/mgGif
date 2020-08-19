using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

namespace MG.GIF
{
    public class GifData
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

        public enum Disposal
        {
            None              = 0x00,
            DoNotDispose      = 0x04,
            RestoreBackground = 0x08,
            ReturnToPrevious  = 0x0C
        }

        public class Image
        {
            public ushort   Left;
            public ushort   Top;
            public ushort   Width;
            public ushort   Height;
            public bool     Interlaced;
            public byte     LzwMinimumCodeSize;
            public ushort   Delay;
            public Color[]  ColourTable;
            public Color[]  RawImage;
            public Disposal DisposalMethod = Disposal.None;
        }

        public string Version  { get; private set; }
        public ushort Width    { get; private set; }
        public ushort Height   { get; private set; }
        public int    BitDepth { get; private set; }

        public List<Image>  Images = new List<Image>();

        public  Color[]     ColourTable;
        public  Color       Background          = Color.black;
        public  byte        BackgroundIndex     = 0xFF;
        private ushort      Delay               = 0;
        public  ushort      TransparentColour   = 0xFFFF;
        public  Disposal    DisposalMethod      = Disposal.None;
        public  ushort      LoopCount           = 0;

        public Image GetImage( int index )
        {
            return index < Images.Count ? Images[index] : null;
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


        //------------------------------------------------------------------------------

        public static GifData Create( byte[] data )
        {
            try
            {
                return new GifData().Decode( data );
            }
            catch( Exception e )
            {
                Debug.Log( e.Message );
                return null;
            }
        }

        //------------------------------------------------------------------------------

        public GifData Decode( byte[] data )
        {
            if( data == null || data.Length <= 12 )
            {
                throw new Exception( "Invalid data" );
            }

            using( var r = new BinaryReader( new MemoryStream( data ) ) )
            {
                ReadHeader( r );
                ReadBlocks( r );
            }

            return this;
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

            Version = new string( r.ReadChars( 6 ) );

            if( Version != "GIF87a" && Version != "GIF89a" )
            {
                throw new Exception( "Unsupported GIF version" );
            }

            // read header

            Width = r.ReadUInt16();
            Height = r.ReadUInt16();
            var f = r.ReadByte();
            var flags       = (Flag) f;
            BitDepth = (int) ( flags & Flag.BitDepthMask ) >> 4 + 1;
            BackgroundIndex = r.ReadByte();
            r.ReadByte(); // aspect ratio

            if( flags.HasFlag( Flag.ColourTable ) )
            {
                ColourTable = ReadColourTable( flags, r );

                if( BackgroundIndex < ColourTable.Length )
                {
                    Background = ColourTable[BackgroundIndex];
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

                            case Extension.ApplicationData:
                                ReadApplicationData( r );
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
                    DisposalMethod = Disposal.DoNotDispose;
                    break;
                case 0x08:
                    DisposalMethod = Disposal.RestoreBackground;
                    break;
                case 0x0C:
                    DisposalMethod = Disposal.ReturnToPrevious;
                    break;
                default:
                    DisposalMethod = Disposal.None;
                    break;
            }

            Delay = r.ReadUInt16();

            var hasTransparentColour = ( flags & 0x01 ) == 0x01;
            var transparentColour = r.ReadByte();

            if( hasTransparentColour )
            {
                TransparentColour = transparentColour;
            }
            else
            {
                TransparentColour = 0xFFFF;
            }

            r.ReadByte(); // terminator
        }

        //------------------------------------------------------------------------------

        private void ReadAnimExt( BinaryReader r )
        {
            var blockSize = r.ReadByte();

            while( blockSize != 0x00 )
            {
                if( blockSize == 3 )
                {
                    var id  = r.ReadByte();
                    var val = r.ReadUInt16();

                    if( id == 1 )
                    {
                        LoopCount = val == 0 ? (ushort) 0xFFFF : val;
                    }
                }
                else
                {
                    r.BaseStream.Seek( blockSize, SeekOrigin.Current );
                }

                blockSize = r.ReadByte();
            }
        }

        private void ReadApplicationData( BinaryReader r )
        {
            var blockSize = r.ReadByte();

            if( blockSize == 0x0b )
            {
                var appId = System.Text.Encoding.Default.GetString( r.ReadBytes( 11 ) );

                if( appId == "NETSCAPE2.0" || appId == "ANIMEXTS1.0" )
                {
                    ReadAnimExt( r );
                    return;
                }

                blockSize = r.ReadByte();
            }

            // blocks

            while( blockSize != 0x00 )
            {
                r.BaseStream.Seek( blockSize, SeekOrigin.Current );
                blockSize = r.ReadByte();
            }
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

            img.Left           = r.ReadUInt16();
            img.Top            = r.ReadUInt16();
            img.Width          = r.ReadUInt16();
            img.Height         = r.ReadUInt16();
            img.Delay          = Delay;
            img.DisposalMethod = DisposalMethod;

            var flags = (Flag) r.ReadByte();
            img.Interlaced = flags.HasFlag( Flag.Interlaced );

            if( img.Width == 0 || img.Height == 0 )
            {
                return;
            }

            if( flags.HasFlag( Flag.ColourTable ) )
            {
                img.ColourTable = ReadColourTable( flags, r );
            }

            img.LzwMinimumCodeSize = r.ReadByte();

            var data = ReadImageBlocks( r );

            // copy background colour?
            Color[] prevImg = null;

            switch( DisposalMethod )
            {
                case Disposal.None:
                case Disposal.DoNotDispose:

                    {
                        var prev = Images.Count > 0 ? Images[ Images.Count - 1 ] : null;

                        if( prev?.RawImage != null )
                        {
                            prevImg = prev.RawImage;
                        }
                    }

                    break;


                case Disposal.ReturnToPrevious:

                    for( int i=Images.Count - 1; i >= 0; i-- )
                    {
                        var prev = Images[ i ];

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

            img.RawImage = new DecompressLZW().Decompress( this, data, img, prevImg );

            if( img.Interlaced )
            {
                img.RawImage = Deinterlace( img.RawImage, img.Width );
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
    }
}
