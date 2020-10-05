using UnityEngine;
using UnityEngine.UI;
using MG.GIF;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.SceneManagement;


//////////////////////////////////////////////////////////////////////////////////

public class TestRunner : MonoBehaviour
{
    public Text       Title;
    public Text       Count;
    public Transform  Contents;
    public GameObject TestCase;

    int mCurrentTest;
    int mPassed;
    int mFailed;
    int mSkipped;
    int mTotal;

    PyTests mTestObject = new PyTests();
    List<TestCase> mTestCases = new List<TestCase>();

    //------------------------------------------------------------------------------

    void Start()
    {
        mCurrentTest = 0;
        mPassed      = 0;
        mFailed      = 0;
        mSkipped     = 0;
        mTotal       = 0;

        foreach( var method in typeof( PyTests ).GetMethods() )
        {
            if( method.GetCustomAttributes( typeof( TestAttribute ), true ).Length == 1 )
            {
                var go = Instantiate( TestCase );
                go.transform.SetParent( Contents );

                var testCase = go.GetComponent<TestCase>();

                testCase.Create( method );
                mTestCases.Add( testCase );
            }
        }

        mTotal = mTestCases.Count;
        UpdateCount();
    }

    //------------------------------------------------------------------------------

    void Update()
    {
        if( mCurrentTest == mTotal )
        {
            return;
        }

        var test = mTestCases[ mCurrentTest ];

        if( test.Run( mTestObject ) )
        {
            mPassed++;
        }
        else
        {
            mFailed++;
        }

        mCurrentTest++;
        UpdateCount();
    }

    void UpdateCount()
    {
        Count.text = $"<size=32><color=green>{mPassed}</color> / <color=red>{mFailed}</color> / {mSkipped}</size> <color=white>{mTotal}</color>";

        var mFinished = mPassed + mSkipped + mFailed;

        if( mFinished != mTotal )
        {
            Title.text = "Running ...";
        }
        else if( mFailed > 0 )
        {
            Title.text = "<color=red>FAIL</color>";
        }
        else
        {
            Title.text = "<color=green>PASS</color>";
        }
    }

    public void OnNext()
    {
        SceneManager.LoadScene( "Profile" );
    }
}

