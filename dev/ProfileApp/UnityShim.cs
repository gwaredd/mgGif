/**/
namespace UnityEngine
{
    public struct Color32
    {
        public byte r;
        public byte g;
        public byte b;
        public byte a;

        public Color32( byte _r, byte _g, byte _b, byte _a )
        {
            r = _r;
            g = _g;
            b = _b;
            a = _a;
        }
    }

    public enum TextureFormat
    {
        ARGB32
    }

    public enum FilterMode
    {
        Point
    }

    public enum TextureWrapMode
    {
        Clamp
    }

    public class Texture2D
    {
        public FilterMode filterMode;
        public TextureWrapMode wrapMode;

        public Texture2D( int Width, int Height, TextureFormat fmt, bool b ) { }
        public void SetPixels32( Color32[] pixels ) { }
        public void Apply() { }
    }
}
/**/
