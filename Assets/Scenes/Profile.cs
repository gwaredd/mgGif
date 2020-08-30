using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

public class Profile : MonoBehaviour
{
    private int     mCount;
    private float   mTimer;
    private long    mTotal;

    void Start()
    {
        mCount = 0;
        mTimer = 2.0f;
        mTotal = 0;
    }

    void Update()
    {
        mTimer -= Time.deltaTime;

        Throttle();

        if( mTimer < 0.0f )
        {
            Run( ++mCount );
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
        mTotal += sw.ElapsedMilliseconds;
        var avg = (float) mTotal / (float) mCount;
        UnityEngine.Debug.Log( $"Sample {sample}, {sw.ElapsedMilliseconds}ms, avg {avg}ms, {sw.ElapsedTicks} ticks" );

        Profiler.EndSample();
    }
}
