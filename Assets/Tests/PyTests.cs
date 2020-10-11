/*
    Test data from
        https://github.com/robert-ancell/pygif

    LICENSE
        LGPL v3.0
        https://github.com/robert-ancell/pygif/blob/master/LICENSE
*/

using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;
using UnityEngine.Networking;
using System.Text;

namespace MG.GIF
{
    //////////////////////////////////////////////////////////////////////////////////
    // Execute test config file

    public class TestConfig
    {
        string mTestDirectory;
        Dictionary<string,string> mConfig = new Dictionary<string, string>();


        //--------------------------------------------------------------------------------

        public int Count
        {
            get
            {
                return mConfig.Count;
            }
        }

        public bool Has( string key )
        {
            return mConfig.ContainsKey( key );
        }

        public string Get( string key )
        {
            return mConfig.ContainsKey( key ) ? mConfig[key] : null;
        }

        //--------------------------------------------------------------------------------
        // read config file

        public TestConfig( string dir, string file )
        {
            mTestDirectory = dir;

            var section  = "";
            var contents = Encoding.Default.GetString( ReadFile( $"{dir}\\{file}" ) );

            foreach( var curLine in contents.Split( new char[] { '\r', '\n' } ) )
            {
                var line = curLine.Trim();

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
                    mConfig[$"{section}{kv[0].Trim()}"] = "";
                }
                else if( kv.Length != 2 )
                {
                    TestContext.WriteLine( $"Unknown config - {line}" );
                }
                else
                {
                    mConfig[$"{section}{kv[0].Trim()}"] = kv[1].Trim();
                }
            }
        }


        //--------------------------------------------------------------------------------
        // read file shim

        byte[] ReadFile( string path )
        {
            return ReadFileWWW( path );
        }

        byte[] ReadFileDefault( string path )
        {
            return File.ReadAllBytes( path );
        }

        byte[] ReadFileWWW( string path )
        {
            using( var req = UnityWebRequest.Get( path ) )
            {
                var op = req.SendWebRequest();

                while( !op.isDone )
                {
                }

                if( req.isNetworkError || req.isHttpError )
                {
                    throw new Exception( req.error );
                }

                return req.downloadHandler.data;
            }
        }


        //--------------------------------------------------------------------------------
        // compare output against reference image

        private void ValidatePixels( List<Image> images, Image frame, string referenceFile )
        {
            if( frame == null && referenceFile == "transparent-dot.rgba" )
            {
                return;
            }

            // read reference file

            var bytes = ReadFile( $"{mTestDirectory}\\{referenceFile}" );

            Assert.IsTrue( bytes.Length % 4 == 0 );

            var colours = new Color32[ bytes.Length / 4 ];

            for( int i=0; i < bytes.Length; i += 4 )
            {
                colours[ i / 4 ] = new Color32(
                    bytes[i + 0],
                    bytes[i + 1],
                    bytes[i + 2],
                    bytes[i + 3]
                );
            }

            // validate

            Assert.IsNotNull( frame );
            Assert.AreEqual( colours.Length, frame.RawImage.Length );

            var width  = images[ 0 ].Width;
            var height = images[ 0 ].Height;

            for( var y = 0; y < height; y++ )
            {
                for( var x = 0; x < width; x++ )
                {
                    // note that colours are flipped in Y as gif texture coordinates are top-left (unity is bottom-left)

                    var i = y * width + x;
                    var j = ( height - y - 1 ) * width + x;

                    Assert.AreEqual( colours[ i ].r, frame.RawImage[ j ].r );
                    Assert.AreEqual( colours[ i ].g, frame.RawImage[ j ].g );
                    Assert.AreEqual( colours[ i ].b, frame.RawImage[ j ].b );
                    Assert.AreEqual( colours[ i ].a, frame.RawImage[ j ].a );
                }
            }
        }

        //--------------------------------------------------------------------------------
        // check frame against config values

        private void ValidateFrame( int frameIndex, string frameName, List<Image> images )
        {
            var handle = $"[{frameName}].";

            var frame = null as Image;

            if( Get("force-animation") == "no" )
            {
                frame = images.GetFrame( frameIndex );
            }
            else
            {
                frame = images[ frameIndex ];
            }


            foreach( var key in mConfig.Keys )
            {
                if( !key.StartsWith( handle) )
                {
                    continue;
                }

                var kv = key.Split( new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries );

                if( kv[1] == "pixels" )
                {
                    ValidatePixels( images, frame, Get( key ) );
                }
                else if( kv[1] == "delay" )
                {
                    Assert.IsNotNull( images );
                    Assert.IsTrue( frameIndex < images.Count );

                    var expected = Get(key);

                    // tests are in 1/100s whereas we store the delay in ms
                    Assert.AreEqual( expected, ( frame.Delay / 10 ).ToString() );
                }
                else
                {
                    TestContext.WriteLine( $"Unknown frame attribute {kv[1]}" );
                }
            }
        }

        //--------------------------------------------------------------------------------
        // test config values

        public void Run()
        {
            // read input gif

            var bytes   = ReadFile( $"{mTestDirectory}\\{Get( "input" )}" );
            var decoder = new Decoder( bytes );

            var images = new List<Image>();
            var img = decoder.NextImage();

            while( img != null )
            {
                images.Add( (Image) img.Clone() );
                img = decoder.NextImage();
            }

            // compare results

            foreach( var key in mConfig.Keys )
            {
                if( key[0] == '[' )
                {
                    continue;
                }

                switch( key )
                {
                    case "input":
                        // test gif for input
                        break;

                    case "comment":         // plain text extension
                    case "xmp-data":        // XMP data extension
                    case "color-profile":   // ICC colour profile extension
                    case "buffer-size":     // size of buffer before playing
                    case "force-animation": // default to true
                    case "loop-count":
                        // ignore
                        break;

                    case "version":
                        Assert.AreEqual( Get( "version" ), decoder.Version );
                        break;

                    case "width":
                        Assert.AreEqual( Get( "width" ), decoder.Width.ToString() );
                        break;

                    case "height":
                        Assert.AreEqual( Get( "height" ), decoder.Height.ToString() );
                        break;

                    case "background":

                        var v   = Get( "background" );

                        //var col = (Color) ColorConverter.ConvertFromString( v );
                        //Assert.AreEqual( col.R, decoder.BackgroundColour.r );
                        //Assert.AreEqual( col.G, decoder.BackgroundColour.g );
                        //Assert.AreEqual( col.B, decoder.BackgroundColour.b );
                        //Assert.AreEqual( col.A, decoder.BackgroundColour.a );

                        Color c;

                        if( ColorUtility.TryParseHtmlString( v, out c ) )
                        {
                            Color32 col = c;
                            Assert.AreEqual( col.r, decoder.BackgroundColour.r );
                            Assert.AreEqual( col.g, decoder.BackgroundColour.g );
                            Assert.AreEqual( col.b, decoder.BackgroundColour.b );
                            Assert.AreEqual( col.a, decoder.BackgroundColour.a );
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
                            ValidateFrame( i, frames[i], images );
                        }

                        break;

                    default:
                        TestContext.WriteLine( $"Unhandled config {key}" );
                        break;
                }
            }
        }
    }


    //////////////////////////////////////////////////////////////////////////////////
    // Unit Tests

    public class PyTests
    {
        private string DirData = Application.streamingAssetsPath + "/Data~";

        //--------------------------------------------------------------------------------
        // config tests

        [Test]
        public void CanReadTestData()
        {
            var d = new DirectoryInfo( DirData );
            var files = d.GetFiles( "*.conf" );

            Assert.AreEqual( 81, files.Length );
        }

        [Test]
        public void CanReadTestConfig()
        {
            var config = new TestConfig( DirData, "255-codes.conf" );

            Assert.AreEqual( 9, config.Count );

            Assert.IsTrue( config.Has( "input" ) );
            Assert.IsTrue( config.Has( "loop-count" ) );
            Assert.IsTrue( config.Has( "[frame0].pixels" ) );
            Assert.IsFalse( config.Has( "doesnotexist" ) );

            Assert.AreEqual( "255-codes.gif", config.Get( "input" ) );
            Assert.AreEqual( "#000000", config.Get( "background" ) );
            Assert.AreEqual( "random-image.rgba", config.Get( "[frame0].pixels" ) );
        }


        //--------------------------------------------------------------------------------
        // pygif tests

        private void C( string file )
        {
            new TestConfig( DirData, $"{file}.conf" ).Run();
        }

        [Test] public void Test_255_codes()                                 { C( "255-codes" ); }
        [Test] public void Test_4095_codes_clear()                          { C( "4095-codes-clear" ); }
        [Test] public void Test_4095_codes()                                { C( "4095-codes" ); }
        [Test] public void Test_all_blues()                                 { C( "all-blues" ); }
        [Test] public void Test_all_greens()                                { C( "all-greens" ); }
        [Test] public void Test_all_reds()                                  { C( "all-reds" ); }
        [Test] public void Test_animation_multi_image_explicit_zero_delay() { C( "animation-multi-image-explicit-zero-delay" ); }
        //[Test] public void Test_animation_multi_image()                     { ValidateConfig( "animation-multi-image" ); }
        [Test] public void Test_animation_no_delays()                       { C( "animation-no-delays" ); }
        [Test] public void Test_animation_speed()                           { C( "animation-speed" ); }
        [Test] public void Test_animation_zero_delays()                     { C( "animation-zero-delays" ); }
        [Test] public void Test_animation()                                 { C( "animation" ); }
        [Test] public void Test_comment()                                   { C( "comment" ); }
        [Test] public void Test_depth1()                                    { C( "depth1" ); }
        [Test] public void Test_depth2()                                    { C( "depth2" ); }
        [Test] public void Test_depth3()                                    { C( "depth3" ); }
        [Test] public void Test_depth4()                                    { C( "depth4" ); }
        [Test] public void Test_depth5()                                    { C( "depth5" ); }
        [Test] public void Test_depth6()                                    { C( "depth6" ); }
        [Test] public void Test_depth7()                                    { C( "depth7" ); }
        [Test] public void Test_depth8()                                    { C( "depth8" ); }
        [Test] public void Test_disabled_transparent()                      { C( "disabled-transparent" ); }
        [Test] public void Test_dispose_keep()                              { C( "dispose-keep" ); }
        [Test] public void Test_dispose_none()                              { C( "dispose-none" ); }
        [Test] public void Test_dispose_restore_background()                { C( "dispose-restore-background" ); }
        [Test] public void Test_dispose_restore_previous()                  { C( "dispose-restore-previous" ); }
        [Test] public void Test_double_clears()                             { C( "double-clears" ); }
        [Test] public void Test_extra_data()                                { C( "extra-data" ); }
        [Test] public void Test_extra_pixels()                              { C( "extra-pixels" ); }
        [Test] public void Test_four_colors()                               { C( "four-colors" ); }
        [Test] public void Test_gif87a_animation()                          { C( "gif87a-animation" ); }
        [Test] public void Test_gif87a()                                    { C( "gif87a" ); }
        [Test] public void Test_high_color()                                { C( "high-color" ); }
        [Test] public void Test_icc_color_profile_empty()                   { C( "icc-color-profile-empty" ); }
        [Test] public void Test_icc_color_profile()                         { C( "icc-color-profile" ); }
        [Test] public void Test_image_inside_bg()                           { C( "image-inside-bg" ); }
        [Test] public void Test_image_outside_bg()                          { C( "image-outside-bg" ); }
        [Test] public void Test_image_overlap_bg()                          { C( "image-overlap-bg" ); }
        [Test] public void Test_image_zero_height()                         { C( "image-zero-height" ); }
        [Test] public void Test_image_zero_size()                           { C( "image-zero-size" ); }
        [Test] public void Test_image_zero_width()                          { C( "image-zero-width" ); }
        [Test] public void Test_images_combine()                            { C( "images-combine" ); }
        [Test] public void Test_images_overlap()                            { C( "images-overlap" ); }
        [Test] public void Test_interlace()                                 { C( "interlace" ); }
        [Test] public void Test_invalid_ascii_comment()                     { C( "invalid-ascii-comment" ); }
        [Test] public void Test_invalid_background()                        { C( "invalid-background" ); }
        [Test] public void Test_invalid_code()                              { C( "invalid-code" ); }
        [Test] public void Test_invalid_colors()                            { C( "invalid-colors" ); }
        [Test] public void Test_invalid_transparent()                       { C( "invalid-transparent" ); }
        [Test] public void Test_invalid_utf8_comment()                      { C( "invalid-utf8-comment" ); }
        [Test] public void Test_large_codes()                               { C( "large-codes" ); }
        [Test] public void Test_large_comment()                             { C( "large-comment" ); }
        [Test] public void Test_local_color_table()                         { C( "local-color-table" ); }
        [Test] public void Test_loop_animexts()                             { C( "loop-animexts" ); }
        [Test] public void Test_loop_buffer()                               { C( "loop-buffer" ); }
        [Test] public void Test_loop_buffer_max()                           { C( "loop-buffer_max" ); }
        [Test] public void Test_loop_infinite()                             { C( "loop-infinite" ); }
        [Test] public void Test_loop_max()                                  { C( "loop-max" ); }
        [Test] public void Test_loop_once()                                 { C( "loop-once" ); }
        [Test] public void Test_many_clears()                               { C( "many-clears" ); }
        [Test] public void Test_max_codes()                                 { C( "max-codes" ); }
        [Test] public void Test_max_height()                                { C( "max-height" ); }
        [Test] public void Test_max_size()                                  { C( "max-size" ); }
        [Test] public void Test_max_width()                                 { C( "max-width" ); }
        [Test] public void Test_missing_pixels()                            { C( "missing-pixels" ); }
        [Test] public void Test_no_clear_and_eoi()                          { C( "no-clear-and-eoi" ); }
        [Test] public void Test_no_clear()                                  { C( "no-clear" ); }
        [Test] public void Test_no_data()                                   { C( "no-data" ); }
        [Test] public void Test_no_eoi()                                    { C( "no-eoi" ); }
        [Test] public void Test_no_global_color_table()                     { C( "no-global-color-table" ); }
        [Test] public void Test_nul_application_extension()                 { C( "nul-application-extension" ); }
        [Test] public void Test_nul_comment()                               { C( "nul-comment" ); }
        [Test] public void Test_plain_text()                                { C( "plain-text" ); }
        [Test] public void Test_transparent()                               { C( "transparent" ); }
        [Test] public void Test_unknown_application_extension()             { C( "unknown-application-extension" ); }
        [Test] public void Test_unknown_extension()                         { C( "unknown-extension" ); }
        [Test] public void Test_xmp_data_empty()                            { C( "xmp-data-empty" ); }
        [Test] public void Test_xmp_data()                                  { C( "xmp-data" ); }
        [Test] public void Test_zero_height()                               { C( "zero-height" ); }
        [Test] public void Test_zero_size()                                 { C( "zero-size" ); }
        [Test] public void Test_zero_width()                                { C( "zero-width" ); }
    }
}


////////////////////////////////////////////////////////////////////////////////
// utility functions

public static class MgGifImageArrayExtension
{
    public static int GetNumFrames( this List<MG.GIF.Image> images )
    {
        int count = 0;

        foreach( var img in images )
        {
            if( img.Delay > 0 )
            {
                count++;
            }
        }

        return count;
    }

    public static MG.GIF.Image GetFrame( this List<MG.GIF.Image> images, int index )
    {
        if( images.Count == 0 )
        {
            return null;
        }

        foreach( var img in images )
        {
            if( img.Delay > 0 )
            {
                if( index == 0 )
                {
                    return img;
                }

                index--;
            }
        }

        return images[ images.Count - 1 ];
    }
}
