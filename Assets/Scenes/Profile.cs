using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

public class Profile : MonoBehaviour
{
    public float SampleWaitTime = 0.25f;
    public long  ThrottleWait   = 50;
    public int   NumSamples     = 20;

    private int     mCount;
    private float   mTimer;
    private long    mTotal;
    private string[] mFiles;

    void Start()
    {
        mCount = 0;
        mTimer = SampleWaitTime;
        mTotal = 0;
        mFiles = Directory.GetFiles( Application.streamingAssetsPath, "*.gif", SearchOption.TopDirectoryOnly );
    }

    void Update()
    {
        if( mCount == NumSamples )
        {
            return;
        }

        mTimer -= Time.deltaTime;

        Throttle();

        if( mTimer < 0.0f )
        {
            Run( ++mCount );
            mTimer = SampleWaitTime;
        }
    }

    void Throttle()
    {
        var sw = new Stopwatch();
        sw.Start();

        while( sw.ElapsedMilliseconds < ThrottleWait )
        {
        }

        sw.Stop();
    }

    void Run( int sample )
    {
        Profiler.BeginSample( $"Sample {sample}" );

        var sw = new Stopwatch();
        sw.Start();

        foreach( var file in mFiles )
        {
            var bytes = File.ReadAllBytes( file );
            new MG.GIF.Decoder( bytes ).DecodeArray();
        }

        sw.Stop();
        mTotal += sw.ElapsedMilliseconds;
        var avg = (float) mTotal / (float) mCount;
        UnityEngine.Debug.Log( $"Sample {sample}, {sw.ElapsedMilliseconds}ms, avg {avg}ms, {sw.ElapsedTicks} ticks" );

        Profiler.EndSample();
    }
}
