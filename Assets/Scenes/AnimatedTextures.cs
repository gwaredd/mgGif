using System.IO;
using UnityEngine;

public class AnimatedTextures : MonoBehaviour
{
    public string Filename;
    public Texture Tex;

    void Start()
    {
        if( string.IsNullOrWhiteSpace( Filename ) )
        {
            return;
        }

        var path  = Path.Combine( Application.streamingAssetsPath, Filename );
        var gif   = MG.GIF.Decoder.Parse( File.ReadAllBytes( path ) );

        var tex = new Texture2D( gif.Width, gif.Height, TextureFormat.ARGB32, false );
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        var img = gif.GetFrame( 0 );

        tex.SetPixels32( img.RawImage );

        //var p = new Color32[ S * S ];

        //for( int i=0; i < S * S; i++ )
        //{
        //    byte r = (byte) Random.Range( 0, 255 );
        //    byte g = (byte) Random.Range( 0, 255 );
        //    byte b = (byte) Random.Range( 0, 255 );
        //    p[i] = new Color32( r, g, b, 0xFF );
        //}
        tex.Apply();



        //Debug.Log( $"{gif.Width}x{gif.Height} #{gif.Images.Count}" );

        //var texture = new Texture2D( 128, 128, TextureFormat.ARGB32, false );

        //for(int y=0; y > 128; y++ )
        //{
        //    for( int x=0; x < 128; x++ )
        //    {
        //        texture.SetPixel( x, y, Color.red );
        //    }
        //}

        //texture.Apply( false );

        GetComponent<Renderer>().material.mainTexture = tex;
        //GetComponent<Renderer>().material.SetTexture( "_MainTex", Tex );

    }

    void Update()
    {
    }
}

