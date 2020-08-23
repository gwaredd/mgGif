using System.IO;
using UnityEngine;

public class AnimatedTextures : MonoBehaviour
{
    public string Filename;

    private Texture2D[] mFrames;
    private float[]     mFrameDelay;

    private int         mCurFrame = 0;
    private float       mTime     = 0.0f;

    void Start()
    {
        if( string.IsNullOrWhiteSpace( Filename ) )
        {
            return;
        }

        var path  = Path.Combine( Application.streamingAssetsPath, Filename );
        var gif   = MG.GIF.Decoder.Parse( File.ReadAllBytes( path ) );

        var numFrames = gif.NumFrames;

        if( numFrames == 0 )
        {
            return;
        }

        mFrames       = new Texture2D[ numFrames ];
        mFrameDelay   = new float[ numFrames ];

        for( int i=0; i < numFrames; i++ )
        {
            var frame       = gif.GetFrame( i );
            mFrames[i]      = frame.CreateTexture();
            mFrameDelay[i]  = frame.Delay / 1000.0f; // ms -> s
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
            mCurFrame = ( mCurFrame + 1 ) % mFrames.Length;
            mTime = 0.0f;

            GetComponent<Renderer>().material.mainTexture = mFrames[mCurFrame];
        }
    }
}

