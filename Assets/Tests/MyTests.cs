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

            if( data.Images != null )
            {
                Assert.Less( frameIndex, data.Images.Count );
                var frame = data.Images[ frameIndex ];

                for( var i = 0; i < colours.Length; i++ )
                {
                    Assert.AreEqual( colours[i], frame.RawImage[i] );
                }
            }
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

        [Test] public void Test_255_codes() { TestGif( "255-codes" ); }
        [Test] public void Test_4095_codes_clear() { TestGif( "4095-codes-clear" ); }
        [Test] public void Test_4095_codes() { TestGif( "4095-codes" ); }
        [Test] public void Test_all_blues() { TestGif( "all-blues" ); }
        [Test] public void Test_all_greens() { TestGif( "all-greens" ); }
        [Test] public void Test_all_reds() { TestGif( "all-reds" ); }
        [Test] public void Test_animation_multi_image_explicit_zero_delay() { TestGif( "animation-multi-image-explicit-zero-delay" ); }
        [Test] public void Test_animation_multi_image() { TestGif( "animation-multi-image" ); }
        [Test] public void Test_animation_no_delays() { TestGif( "animation-no-delays" ); }
        [Test] public void Test_animation_speed() { TestGif( "animation-speed" ); }
        [Test] public void Test_animation_zero_delays() { TestGif( "animation-zero-delays" ); }
        [Test] public void Test_animation() { TestGif( "animation" ); }
        [Test] public void Test_comment() { TestGif( "comment" ); }
        [Test] public void Test_depth1() { TestGif( "depth1" ); }
        [Test] public void Test_depth2() { TestGif( "depth2" ); }
        [Test] public void Test_depth3() { TestGif( "depth3" ); }
        [Test] public void Test_depth4() { TestGif( "depth4" ); }
        [Test] public void Test_depth5() { TestGif( "depth5" ); }
        [Test] public void Test_depth6() { TestGif( "depth6" ); }
        [Test] public void Test_depth7() { TestGif( "depth7" ); }
        [Test] public void Test_depth8() { TestGif( "depth8" ); }
        [Test] public void Test_disabled_transparent() { TestGif( "disabled-transparent" ); }
        [Test] public void Test_dispose_keep() { TestGif( "dispose-keep" ); }
        [Test] public void Test_dispose_none() { TestGif( "dispose-none" ); }
        [Test] public void Test_dispose_restore_background() { TestGif( "dispose-restore-background" ); }
        [Test] public void Test_dispose_restore_previous() { TestGif( "dispose-restore-previous" ); }
        [Test] public void Test_double_clears() { TestGif( "double-clears" ); }
        [Test] public void Test_extra_data() { TestGif( "extra-data" ); }
        [Test] public void Test_extra_pixels() { TestGif( "extra-pixels" ); }
        [Test] public void Test_four_colors() { TestGif( "four-colors" ); }
        [Test] public void Test_gif87a_animation() { TestGif( "gif87a-animation" ); }
        [Test] public void Test_gif87a() { TestGif( "gif87a" ); }
        [Test] public void Test_high_color() { TestGif( "high-color" ); }
        [Test] public void Test_icc_color_profile_empty() { TestGif( "icc-color-profile-empty" ); }
        [Test] public void Test_icc_color_profile() { TestGif( "icc-color-profile" ); }
        [Test] public void Test_image_inside_bg() { TestGif( "image-inside-bg" ); }
        [Test] public void Test_image_outside_bg() { TestGif( "image-outside-bg" ); }
        [Test] public void Test_image_overlap_bg() { TestGif( "image-overlap-bg" ); }
        [Test] public void Test_image_zero_height() { TestGif( "image-zero-height" ); }
        [Test] public void Test_image_zero_size() { TestGif( "image-zero-size" ); }
        [Test] public void Test_image_zero_width() { TestGif( "image-zero-width" ); }
        [Test] public void Test_images_combine() { TestGif( "images-combine" ); }
        [Test] public void Test_images_overlap() { TestGif( "images-overlap" ); }
        [Test] public void Test_interlace() { TestGif( "interlace" ); }
        [Test] public void Test_invalid_ascii_comment() { TestGif( "invalid-ascii-comment" ); }
        [Test] public void Test_invalid_background() { TestGif( "invalid-background" ); }
        [Test] public void Test_invalid_code() { TestGif( "invalid-code" ); }
        [Test] public void Test_invalid_colors() { TestGif( "invalid-colors" ); }
        [Test] public void Test_invalid_transparent() { TestGif( "invalid-transparent" ); }
        [Test] public void Test_invalid_utf8_comment() { TestGif( "invalid-utf8-comment" ); }
        [Test] public void Test_large_codes() { TestGif( "large-codes" ); }
        [Test] public void Test_large_comment() { TestGif( "large-comment" ); }
        [Test] public void Test_local_color_table() { TestGif( "local-color-table" ); }
        [Test] public void Test_loop_animexts() { TestGif( "loop-animexts" ); }
        [Test] public void Test_loop_buffer() { TestGif( "loop-buffer" ); }
        [Test] public void Test_loop_buffer_max() { TestGif( "loop-buffer_max" ); }
        [Test] public void Test_loop_infinite() { TestGif( "loop-infinite" ); }
        [Test] public void Test_loop_max() { TestGif( "loop-max" ); }
        [Test] public void Test_loop_once() { TestGif( "loop-once" ); }
        [Test] public void Test_many_clears() { TestGif( "many-clears" ); }
        [Test] public void Test_max_codes() { TestGif( "max-codes" ); }
        [Test] public void Test_max_height() { TestGif( "max-height" ); }
        [Test] public void Test_max_size() { TestGif( "max-size" ); }
        [Test] public void Test_max_width() { TestGif( "max-width" ); }
        [Test] public void Test_missing_pixels() { TestGif( "missing-pixels" ); }
        [Test] public void Test_no_clear_and_eoi() { TestGif( "no-clear-and-eoi" ); }
        [Test] public void Test_no_clear() { TestGif( "no-clear" ); }
        [Test] public void Test_no_data() { TestGif( "no-data" ); }
        [Test] public void Test_no_eoi() { TestGif( "no-eoi" ); }
        [Test] public void Test_no_global_color_table() { TestGif( "no-global-color-table" ); }
        [Test] public void Test_nul_application_extension() { TestGif( "nul-application-extension" ); }
        [Test] public void Test_nul_comment() { TestGif( "nul-comment" ); }
        [Test] public void Test_plain_text() { TestGif( "plain-text" ); }
        [Test] public void Test_transparent() { TestGif( "transparent" ); }
        [Test] public void Test_unknown_application_extension() { TestGif( "unknown-application-extension" ); }
        [Test] public void Test_unknown_extension() { TestGif( "unknown-extension" ); }
        [Test] public void Test_xmp_data_empty() { TestGif( "xmp-data-empty" ); }
        [Test] public void Test_xmp_data() { TestGif( "xmp-data" ); }
        [Test] public void Test_zero_height() { TestGif( "zero-height" ); }
        [Test] public void Test_zero_size() { TestGif( "zero-size" ); }
        [Test] public void Test_zero_width() { TestGif( "zero-width" ); }
    }
}
