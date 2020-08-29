using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

public class Profile : MonoBehaviour
{
    public int NumSamples = 3;

    private int     mSample;
    private float   mTimer;

    void Start()
    {
        mSample = 0;
        mTimer  = 2.0f;
    }

    void Update()
    {
        mTimer -= Time.deltaTime;

        Throttle();

        if( mTimer < 0.0f && mSample < NumSamples )
        {
            Run( ++mSample );
            mTimer = 1.0f;
        }
    }

    void Throttle()
    {
        var sw = new Stopwatch();
        sw.Start();

        while( sw.ElapsedMilliseconds < 100 )
        {
        }

        sw.Stop();
    }

    void Run( int sample )
    {
        var files = new string[]{ "butterfly.gif", "cat.gif", "jellyfish.gif" };

        Profiler.BeginSample( $"Sample {sample}" );

        var sw = new Stopwatch();
        sw.Start();

        foreach( var file in files )
        {
            var path  = Path.Combine( Application.streamingAssetsPath, file );
            MG.GIF.Decoder.Parse( File.ReadAllBytes( path ) );
        }

        sw.Stop();
        UnityEngine.Debug.Log( $"Sample {sample}, {sw.ElapsedMilliseconds}ms, {sw.ElapsedTicks} ticks" );

        Profiler.EndSample();
    }
}
