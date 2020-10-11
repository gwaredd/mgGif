using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class AnimatedTextures : MonoBehaviour
{
    public string Filename;

    private List<Texture2D> mFrames     = new List<Texture2D>();
    private List<float>     mFrameDelay = new List<float>();

    private int   mCurFrame = 0;
    private float mTime     = 0.0f;

    void Start()
    {
        if( string.IsNullOrWhiteSpace( Filename ) )
        {
            return;
        }

        var path = Path.Combine( Application.streamingAssetsPath, Filename );

        using( var decoder = new MG.GIF.Decoder( File.ReadAllBytes( path ) ) )
        {
            var img = decoder.NextImage();

            while( img != null )
            {
                mFrames.Add( img.CreateTexture() );
                mFrameDelay.Add( img.Delay / 1000.0f );
                img = decoder.NextImage();
            }
        }

        GetComponent<Renderer>().material.mainTexture = mFrames[0];
    }

    void Update()
    {
        if( mFrames == null )
        {
            return;
        }

        mTime += Time.deltaTime;

        if( mTime >= mFrameDelay[ mCurFrame ] )
        {
            mCurFrame = ( mCurFrame + 1 ) % mFrames.Count;
            mTime = 0.0f;

            GetComponent<Renderer>().material.mainTexture = mFrames[mCurFrame];
        }
    }
}

