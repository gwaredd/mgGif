using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

public class Profile : MonoBehaviour
{
    public string SampleFile = "cat.gif";
    public int NumSamples = 1;

    void Update()
    {
        if( !Input.GetKeyDown( KeyCode.P ) )
        {
            var sw = new Stopwatch();
            sw.Start();
            while( sw.ElapsedMilliseconds < 100 )
            {
            }
            sw.Stop();
            return;
        }

        UnityEngine.Debug.Log( "running" );

        var path  = Path.Combine( Application.streamingAssetsPath, SampleFile );
        var bytes = File.ReadAllBytes( path );

        for( int i=0; i < NumSamples; i++ )
        {
            var gif   = MG.GIF.Decoder.Parse( bytes );
        }

        UnityEngine.Debug.Log( "done" );
    }
}
