using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

public class Profile : MonoBehaviour
{
    private int      mCount;
    private long     mTotal;
    private string[] mFiles;

    void Start()
    {
        mCount = 0;
        mTotal = 0;
        mFiles = Directory.GetFiles( Application.streamingAssetsPath, "*.gif", SearchOption.TopDirectoryOnly );
    }

    void Update()
    {
        Run( ++mCount );
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

        UnityEngine.Debug.Log( $"[{sample}]: avg <color=yellow>{avg:0.0}</color>ms ({sw.ElapsedMilliseconds}ms)" );

        Profiler.EndSample();
    }
}
