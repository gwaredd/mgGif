using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

public class Profile : MonoBehaviour
{
    private int      mCount;
    private long     mSum;
    private long     mSumSquares;
    private byte[][] mFiles;

    void Start()
    {
        mCount      = 0;
        mSum        = 0;
        mSumSquares = 0;

        var files = Directory.GetFiles( Application.streamingAssetsPath, "*.gif" );
        mFiles = (from file in files select File.ReadAllBytes( file )).ToArray();
    }

    void Update()
    {
        Run( ++mCount );
    }

    void Run( int sample )
    {
        Profiler.BeginSample( $"Sample {sample}" );

        var decoder = new MG.GIF.Decoder();

        var sw = new Stopwatch();

        sw.Start();

        foreach( var file in mFiles )
        {
            decoder.Decode( file ).GetImages();
        }

        sw.Stop();

        mSum += sw.ElapsedMilliseconds;
        mSumSquares += sw.ElapsedMilliseconds * sw.ElapsedMilliseconds;

        var average  = (float) mSum / mCount;
        var variance = mSumSquares / mCount - average * average;

        UnityEngine.Debug.Log( $"[{sample:00}]: av <color=yellow>{average:0.0}</color>ms, sd <color=yellow>{Mathf.Sqrt( variance ):0.0}</color> - {sw.ElapsedMilliseconds}ms" );

        Profiler.EndSample();
    }
}
