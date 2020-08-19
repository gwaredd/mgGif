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
            Interlaced      = 0x40,
            ColourTable     = 0x80,
            TableSizeMask   = 0x07,
            BitDepthMask    = 0x70,
        }

        private enum Block
        {
            Image       = 0x2C,
            Extension   = 0x21,
            End         = 0x3B
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
            None                = 0x00,
            DoNotDispose        = 0x04,
            RestoreBackground   = 0x08,
            ReturnToPrevious    = 0x0C
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

            public Color[] ColourTable;
            public Color[] RawImage;
        }

        public string   Version         { get; private set; }
        public ushort   Width           { get; private set; }
        public ushort   Height          { get; private set; }
        public int      BitDepth        { get; private set; }

        public List<Image>  Images  = new List<Image>();

        public  Color[]     ColourTable;
        public  Color       Background          = Color.black;
        private ushort      Delay               = 0;
        public  ushort      TransparentColour   = 0xFFFF;
        public  Disposal    DisposalMethod      = Disposal.None;


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
            var tableSize = (int) Math.Pow( 2, (int)( flags & Flag.TableSizeMask ) + 1 );
            var colourTable = new Color[ tableSize ];

            for( var i = 0; i < tableSize; i++ )
            {
                colourTable[i] = new Color(
                    ( (float) r.ReadByte() ) / 255.0f,
                    ( (float) r.ReadByte() ) / 255.0f,
                    ( (float) r.ReadByte() ) / 255.0f
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

            Width       = r.ReadUInt16();
            Height      = r.ReadUInt16();
            var flags   = (Flag) r.ReadByte();
            var bgIndex = r.ReadByte();
            r.ReadByte(); // aspect ratio
            BitDepth    = (int)( flags & Flag.BitDepthMask ) >> 4 + 1;

            if( flags.HasFlag( Flag.ColourTable ) )
            {
                ColourTable = ReadColourTable( flags, r );
                Background  = bgIndex < ColourTable.Length ? ColourTable[bgIndex] : Color.black;
            }
            else
            {
                Background = Color.black;
            }
        }

        //------------------------------------------------------------------------------

        protected void ReadBlocks( BinaryReader r )
        {
            while( true )
            {
                switch( (Block) r.ReadByte() )
                {
                    case Block.Image:
                        ReadImageBlock( r );
                        break;

                    case Block.Extension:

                        var ext = (Extension) r.ReadByte();

                        if( ext == Extension.GraphicControl )
                        {
                            ReadControlBlock( r );
                        }
                        else
                        {
                            SkipBlock( r );
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
                r.ReadBytes( blockSize );
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

            var delay = r.ReadUInt16();

            if( delay != 0 )
            {
                Delay = delay;
            }

            var hasTransparentColour = ( flags & 0x01 ) == 0x01;
            var transparentColour = r.ReadByte();
            TransparentColour = hasTransparentColour ? transparentColour : (ushort) 0xFFFF;

            r.ReadByte(); // terminator
        }


        //------------------------------------------------------------------------------

        protected void ReadImageBlock( BinaryReader r )
        {
            var img = new Image();

            img.Delay = Delay;

            img.Left   = r.ReadUInt16();
            img.Top    = r.ReadUInt16();
            img.Width  = r.ReadUInt16();
            img.Height = r.ReadUInt16();

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

            // TODO: decode!

            if( img.Interlaced )
            {
                Debug.LogWarning( "image is interlaced!" );
            }

            var data = ReadImageBlocks( r );

            img.RawImage = new DecompressLZW().Decompress( this, data, img );

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
