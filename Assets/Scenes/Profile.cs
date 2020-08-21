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
            return;
        }

        Debug.Log( "running" );

        var path  = Path.Combine( Application.streamingAssetsPath, SampleFile );
        var bytes = File.ReadAllBytes( path );

        Profiler.BeginSample( "Decompress" );

        for( int i=0; i < NumSamples; i++ )
        {
            var gif   = MG.GIF.Decoder.Parse( bytes );
        }

        Profiler.EndSample();
        Debug.Log( "done" );
    }
}
