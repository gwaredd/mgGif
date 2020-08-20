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

        //var path  = Path.Combine( Application.streamingAssetsPath, Filename );
        //var gif   = MG.GIF.Decoder.Parse( File.ReadAllBytes( path ) );

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

        GetComponent<Renderer>().material.mainTexture = Tex;
        //GetComponent<Renderer>().material.SetTexture( "_MainTex", Tex );

    }

    void Update()
    {
    }
}

