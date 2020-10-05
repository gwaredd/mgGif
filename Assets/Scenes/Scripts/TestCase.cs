using System;
using System.Reflection;
using MG.GIF;
using UnityEngine;
using UnityEngine.UI;

public class TestCase : MonoBehaviour
{
    public Text NameLabel;

    string      mName;
    MethodInfo  mMethod;

    public bool Run( PyTests mTestObject )
    {
        var img = GetComponent<UnityEngine.UI.Image>();

        try
        {
            mMethod.Invoke( mTestObject, null );
            img.color = new Color32( 226, 240, 217, 0xFF );
        }
        catch( Exception e )
        {
            NameLabel.text = $"<color=red>{mName}</color> {e.Message}";
            img.color = new Color32( 246, 183, 157, 0xFF );
            return false;
        }

        return true;
    }

    public void Create( MethodInfo method )
    {
        mName   = method.Name;
        mMethod = method;

        name = mName;
        NameLabel.text = mName;
    }
}
