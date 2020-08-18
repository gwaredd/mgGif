using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MG.GIF;
using System;

namespace Tests
{
    //////////////////////////////////////////////////////////////////////////////////

    public class TestConfig
    {
        public string Dir;
        public Dictionary<string,string> Config = new Dictionary<string, string>();

        public bool Has( string key )
        {
            return Config.ContainsKey( key );
        }

        public string Get( string key )
        {
            return Config.ContainsKey( key ) ? Config[key] : null;
        }

        public TestConfig( string dir, string file )
        {
            Dir = dir;

            string line;
            string section = "";

            var r = new StreamReader( $"{dir}\\{file}" );

            while( ( line = r.ReadLine() ) != null )
            {
                line = line.Trim();

                if( line.Length == 0 || line[0] == '#' )
                {
                    continue;
                }

                if( line[0] == '[' )
                {
                    if( line == "[config]" )
                    {
                        section = "";
                    }
                    else
                    {
                        section = $"{line}.";
                    }

                    continue;
                }

                var kv = line.Split( new char[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries );

                if( kv.Length == 1 )
                {
                    Config[$"{section}{kv[0].Trim()}"] = "";
                }
                else if( kv.Length != 2 )
                {
                    Debug.LogWarning( $"Unknown config - {line}" );
                }
                else
                {
                    Config[$"{section}{kv[0].Trim()}"] = kv[1].Trim();
                }
            }

            r.Close();
        }

        //--------------------------------------------------------------------------------

        private void ValidatePixels( int frameIndex, string referenceFile, GifData data )
        {
            Debug.LogWarning( $"ValidatePixels for frame {frameIndex} with {referenceFile}" );

            // read reference file

            var bytes = File.ReadAllBytes( $"{Dir}\\{referenceFile}" );

            Assert.IsTrue( bytes.Length % 4 == 0 );

            var colours = new Color[ bytes.Length / 4 ];

            for( int i=0; i < bytes.Length; i += 4 )
            {
                colours[ i / 4 ] = new Color(
                    bytes[i + 0] / 255.0f,
                    bytes[i + 1] / 255.0f,
                    bytes[i + 2] / 255.0f,
                    bytes[i + 3] / 255.0f
                );
            }

            // validate

            Assert.AreEqual( colours.Length, data.Width * data.Height );
        }

        //--------------------------------------------------------------------------------

        private void ValidateFrame( int frameIndex, string frameName, GifData data )
        {
            var handle = $"[{frameName}].";

            foreach( var key in Config.Keys )
            {
                if( !key.StartsWith( handle) )
                {
                    continue;
                }

                var kv = key.Split( new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries );

                if( kv[1] == "pixels" )
                {
                    ValidatePixels( frameIndex, Get( key ), data );
                }
                else if( kv[1] == "delay" )
                {
                    Assert.IsNotNull( data.Controls );
                    Assert.IsTrue( frameIndex < data.Controls.Count );

                    var delay = data.Controls[ frameIndex ].Delay;
                    var expected = Get(key);

                    Assert.AreEqual( expected, delay.ToString() );
                }
                else
                {
                    Debug.LogError( $"Unknown frame attribute {kv[1]}" );
                }
            }
        }

        //--------------------------------------------------------------------------------

        public void Apply( GifData data )
        {
            foreach( var key in Config.Keys )
            {
                if( key[0] == '[' )
                {
                    continue;
                }

                switch( key )
                {
                    case "input": // test gif for input
                        break;

                    case "comment": // plain text extension
                    case "xmp-data": // XMP data extension
                    case "color-profile": // ICC colour profile extension
                        break;

                    // Netscape or Animation extension
                    case "loop-count": // default to infinite
                    case "buffer-size": // size of buffer before playing
                    case "force-animation": // default to true
                        // ignore
                        break;

                    case "version":
                        Assert.AreEqual( Get( "version" ), data.Version );
                        break;

                    case "width":
                        Assert.AreEqual( Get( "width" ), data.Width.ToString() );
                        break;

                    case "height":
                        Assert.AreEqual( Get( "height" ), data.Height.ToString() );
                        break;

                    case "background":

                        Color col;
                        var v = Get( "background" );

                        if( ColorUtility.TryParseHtmlString( v, out col ) )
                        {
                            Assert.AreEqual( col, data.Background );
                        }
                        else
                        {
                            Debug.LogError( $"Failed to parse background colour {v}" );
                        }

                        break;

                    case "frames":

                        var frames = Get( "frames" ).Split( new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries );

                        for( int i = 0; i < frames.Length; i++ )
                        {
                            ValidateFrame( i, frames[i], data );
                        }

                        break;

                    default:
                        Debug.LogWarning( $"Unhandled config {key}" );
                        break;
                }
            }
        }
    }

    //////////////////////////////////////////////////////////////////////////////////


    public class MyTests
    {
        private string DirData = @"Assets\Tests\~Data";

        //--------------------------------------------------------------------------------

        [Test]
        public void CanReadTestData()
        {
            var d = new DirectoryInfo( DirData );
            var files = d.GetFiles( "*.conf" );

            Assert.AreEqual( 81, files.Length );
        }

        //--------------------------------------------------------------------------------

        [Test]
        public void CanReadTestConfig()
        {
            var config = new TestConfig( DirData, "255-codes.conf" );

            Assert.AreEqual( 9, config.Config.Count );

            Assert.IsTrue( config.Has( "input" ) );
            Assert.IsTrue( config.Has( "loop-count" ) );
            Assert.IsTrue( config.Has( "[frame0].pixels" ) );
            Assert.IsFalse( config.Has( "doesnotexist" ) );

            Assert.AreEqual( "255-codes.gif", config.Get( "input" ) );
            Assert.AreEqual( "#000000", config.Get( "background" ) );
            Assert.AreEqual( "random-image.rgba", config.Get( "[frame0].pixels" ) );
        }

        //--------------------------------------------------------------------------------

        private void TestGif( string file )
        {
            var config  = new TestConfig( DirData, $"{file}.conf" );
            var data    = File.ReadAllBytes( $"{DirData}\\{config.Get( "input" )}" );
            var format  = GifData.Create( data );

            config.Apply( format );
        }

        //--------------------------------------------------------------------------------

        [Test]
        public void TestOne()
        {
            //TestGif( "animation-multi-image-explicit-zero-delay" );
            TestGif( "255-codes" );
        }

        //--------------------------------------------------------------------------------

        private List<string> mSkip = new List<string>()
        {
            //"animation-multi-image-explicit-zero-delay"
        };

        [Test]
        public void TestAll()
        {
            var d = new DirectoryInfo( DirData );

            foreach( var file in d.GetFiles( "*.conf" ) )
            {
                var filename = Path.GetFileNameWithoutExtension( file.Name );

                if( mSkip.Contains( filename ) )
                {
                    Debug.LogWarning( $"Skip '{filename}'" );
                    continue;
                }

                Debug.Log( filename );
                TestGif( filename );
            }
        }
    }
}
