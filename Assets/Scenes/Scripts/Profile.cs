using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Profile : MonoBehaviour
{
    public Text Text;
    public Text Version;

    private int      mCount;
    private long     mSum;
    private long     mSumSquares;
    private byte[][] mFiles;

    void Start()
    {
        Version.text = MG.GIF.Decoder.Ident();

        mCount      = 0;
        mSum        = 0;
        mSumSquares = 0;

        StartCoroutine( ReadFiles() );
    }

    IEnumerator ReadFiles()
    {
        mFiles = new byte[1][];

        var files = new string[]{ "butterfly.gif", "cat.gif", "jellyfish.gif" };

        var data = new List<byte[]>();

        foreach( var filename in files )
        {
            var path = Application.streamingAssetsPath + "/" + filename;

            using( var req = UnityWebRequest.Get( path ) )
            {
                yield return req.SendWebRequest();
                data.Add( req.downloadHandler.data );
            }
        }

        mFiles = data.ToArray();
    }

    void Update()
    {
        if( mFiles.Length != 3 )
        {
            return;
        }

        Run( ++mCount );
    }

    void Run( int sample )
    {
        var decoder = new MG.GIF.Decoder();

        var sw = new Stopwatch();

        sw.Start();

        foreach( var file in mFiles )
        {
            decoder.Load( file );

            var img = decoder.NextImage();

            while( img != null )
            {
                img = decoder.NextImage();
            }
        }

        sw.Stop();

        mSum += sw.ElapsedMilliseconds;
        mSumSquares += sw.ElapsedMilliseconds * sw.ElapsedMilliseconds;

        var average  = (float) mSum / mCount;
        var variance = mSumSquares / mCount - average * average;

        UnityEngine.Debug.Log( $"[{sample:00}]: av <color=yellow>{average:0.0}</color>ms, sd <color=yellow>{Mathf.Sqrt( variance ):0.0}</color> - {sw.ElapsedMilliseconds}ms" );

        Text.text = $"[{sample:00}] {average:0.0}";
    }

    public void OnNext()
    {
        SceneManager.LoadScene( "Tests" );
    }
}
