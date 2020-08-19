using System.Collections.Generic;
using UnityEngine;

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
        public Color[]  RawImage;
        public ushort   Delay;
        public Disposal DisposalMethod = Disposal.None;
    }

    public class ImageList
    {
        public string   Version;
        public ushort   Width;
        public ushort   Height;
        public int      BitDepth;

        public List<Image> Images = new List<Image>();

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
    }
}
