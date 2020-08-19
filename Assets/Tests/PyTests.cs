// test suite from https://github.com/robert-ancell/pygif

using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System;

// TODO: other tests
//  http://www.schaik.com/pngsuite/
//  https://code.google.com/archive/p/imagetestsuite/downloads


namespace MG.GIF
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

        private void ValidatePixels( Image frame, string referenceFile )
        {
            if( frame == null && referenceFile == "transparent-dot.rgba" )
            {
                return;
            }

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

            Assert.IsNotNull( frame );
            Assert.AreEqual( colours.Length, frame.RawImage.Length );

            for( var i = 0; i < colours.Length; i++ )
            {
                //Debug.Log( i );
                Assert.AreEqual( colours[i], frame.RawImage[i] );
            }
        }

        //--------------------------------------------------------------------------------

        private void ValidateFrame( int frameIndex, string frameName, ImageList data )
        {
            var handle = $"[{frameName}].";

            var frame = null as Image;

            if( Get("force-animation") == "no" )
            {
                frame = data.GetFrame( frameIndex );
            }
            else
            {
                frame = data.GetImage( frameIndex );
            }


            foreach( var key in Config.Keys )
            {
                if( !key.StartsWith( handle) )
                {
                    continue;
                }

                var kv = key.Split( new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries );

                if( kv[1] == "pixels" )
                {
                    ValidatePixels( frame, Get( key ) );
                }
                else if( kv[1] == "delay" )
                {
                    Assert.IsNotNull( data.Images );
                    Assert.IsTrue( frameIndex < data.Images.Count );

                    var expected = Get(key);

                    Assert.AreEqual( expected, frame.Delay.ToString() );
                }
                else
                {
                    Debug.LogError( $"Unknown frame attribute {kv[1]}" );
                }
            }
        }

        //--------------------------------------------------------------------------------

        public void Run()
        {
            var bytes = File.ReadAllBytes( $"{Dir}\\{Get( "input" )}" );
            var gif   = Decoder.Parse( bytes );

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
                    case "buffer-size": // size of buffer before playing
                    case "force-animation": // default to true
                        // ignore
                        break;

                    case "version":
                        Assert.AreEqual( Get( "version" ), gif.Version );
                        break;

                    case "width":
                        Assert.AreEqual( Get( "width" ), gif.Width.ToString() );
                        break;

                    case "height":
                        Assert.AreEqual( Get( "height" ), gif.Height.ToString() );
                        break;

                    case "loop-count":

                        //var loop_count = Get( "loop-count" );

                        //if( loop_count == "infinite" )
                        //{
                        //    Assert.AreEqual( 0xFFFF.ToString(), gif.LoopCount.ToString() );
                        //}
                        //else
                        //{
                        //    Assert.AreEqual( loop_count, gif.LoopCount.ToString() );
                        //}

                        break;

                    case "background":

                        Color col;
                        var v = Get( "background" );

                        if( ColorUtility.TryParseHtmlString( v, out col ) )
                        {
                            //Assert.AreEqual( col, gif.BackgroundColour );
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
                            ValidateFrame( i, frames[i], gif );
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

    public class PyTests
    {
        private string DirData = @"Assets\Tests\~Data";

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
        // all tests

        private void ValidateConfig( string file )
        {
            new TestConfig( DirData, $"{file}.conf" ).Run();
        }

        [Test] public void Test_255_codes()                                 { ValidateConfig( "255-codes" ); }
        [Test] public void Test_4095_codes_clear()                          { ValidateConfig( "4095-codes-clear" ); }
        [Test] public void Test_4095_codes()                                { ValidateConfig( "4095-codes" ); }
        [Test] public void Test_all_blues()                                 { ValidateConfig( "all-blues" ); }
        [Test] public void Test_all_greens()                                { ValidateConfig( "all-greens" ); }
        [Test] public void Test_all_reds()                                  { ValidateConfig( "all-reds" ); }
        [Test] public void Test_animation_multi_image_explicit_zero_delay() { ValidateConfig( "animation-multi-image-explicit-zero-delay" ); }
        //[Test] public void Test_animation_multi_image()                     { ValidateConfig( "animation-multi-image" ); }
        [Test] public void Test_animation_no_delays()                       { ValidateConfig( "animation-no-delays" ); }
        [Test] public void Test_animation_speed()                           { ValidateConfig( "animation-speed" ); }
        [Test] public void Test_animation_zero_delays()                     { ValidateConfig( "animation-zero-delays" ); }
        [Test] public void Test_animation()                                 { ValidateConfig( "animation" ); }
        [Test] public void Test_comment()                                   { ValidateConfig( "comment" ); }
        [Test] public void Test_depth1()                                    { ValidateConfig( "depth1" ); }
        [Test] public void Test_depth2()                                    { ValidateConfig( "depth2" ); }
        [Test] public void Test_depth3()                                    { ValidateConfig( "depth3" ); }
        [Test] public void Test_depth4()                                    { ValidateConfig( "depth4" ); }
        [Test] public void Test_depth5()                                    { ValidateConfig( "depth5" ); }
        [Test] public void Test_depth6()                                    { ValidateConfig( "depth6" ); }
        [Test] public void Test_depth7()                                    { ValidateConfig( "depth7" ); }
        [Test] public void Test_depth8()                                    { ValidateConfig( "depth8" ); }
        [Test] public void Test_disabled_transparent()                      { ValidateConfig( "disabled-transparent" ); }
        [Test] public void Test_dispose_keep()                              { ValidateConfig( "dispose-keep" ); }
        [Test] public void Test_dispose_none()                              { ValidateConfig( "dispose-none" ); }
        [Test] public void Test_dispose_restore_background()                { ValidateConfig( "dispose-restore-background" ); }
        [Test] public void Test_dispose_restore_previous()                  { ValidateConfig( "dispose-restore-previous" ); }
        [Test] public void Test_double_clears()                             { ValidateConfig( "double-clears" ); }
        [Test] public void Test_extra_data()                                { ValidateConfig( "extra-data" ); }
        [Test] public void Test_extra_pixels()                              { ValidateConfig( "extra-pixels" ); }
        [Test] public void Test_four_colors()                               { ValidateConfig( "four-colors" ); }
        [Test] public void Test_gif87a_animation()                          { ValidateConfig( "gif87a-animation" ); }
        [Test] public void Test_gif87a()                                    { ValidateConfig( "gif87a" ); }
        [Test] public void Test_high_color()                                { ValidateConfig( "high-color" ); }
        [Test] public void Test_icc_color_profile_empty()                   { ValidateConfig( "icc-color-profile-empty" ); }
        [Test] public void Test_icc_color_profile()                         { ValidateConfig( "icc-color-profile" ); }
        [Test] public void Test_image_inside_bg()                           { ValidateConfig( "image-inside-bg" ); }
        [Test] public void Test_image_outside_bg()                          { ValidateConfig( "image-outside-bg" ); }
        [Test] public void Test_image_overlap_bg()                          { ValidateConfig( "image-overlap-bg" ); }
        [Test] public void Test_image_zero_height()                         { ValidateConfig( "image-zero-height" ); }
        [Test] public void Test_image_zero_size()                           { ValidateConfig( "image-zero-size" ); }
        [Test] public void Test_image_zero_width()                          { ValidateConfig( "image-zero-width" ); }
        [Test] public void Test_images_combine()                            { ValidateConfig( "images-combine" ); }
        [Test] public void Test_images_overlap()                            { ValidateConfig( "images-overlap" ); }
        [Test] public void Test_interlace()                                 { ValidateConfig( "interlace" ); }
        [Test] public void Test_invalid_ascii_comment()                     { ValidateConfig( "invalid-ascii-comment" ); }
        [Test] public void Test_invalid_background()                        { ValidateConfig( "invalid-background" ); }
        [Test] public void Test_invalid_code()                              { ValidateConfig( "invalid-code" ); }
        [Test] public void Test_invalid_colors()                            { ValidateConfig( "invalid-colors" ); }
        [Test] public void Test_invalid_transparent()                       { ValidateConfig( "invalid-transparent" ); }
        [Test] public void Test_invalid_utf8_comment()                      { ValidateConfig( "invalid-utf8-comment" ); }
        [Test] public void Test_large_codes()                               { ValidateConfig( "large-codes" ); }
        [Test] public void Test_large_comment()                             { ValidateConfig( "large-comment" ); }
        [Test] public void Test_local_color_table()                         { ValidateConfig( "local-color-table" ); }
        [Test] public void Test_loop_animexts()                             { ValidateConfig( "loop-animexts" ); }
        [Test] public void Test_loop_buffer()                               { ValidateConfig( "loop-buffer" ); }
        [Test] public void Test_loop_buffer_max()                           { ValidateConfig( "loop-buffer_max" ); }
        [Test] public void Test_loop_infinite()                             { ValidateConfig( "loop-infinite" ); }
        [Test] public void Test_loop_max()                                  { ValidateConfig( "loop-max" ); }
        [Test] public void Test_loop_once()                                 { ValidateConfig( "loop-once" ); }
        [Test] public void Test_many_clears()                               { ValidateConfig( "many-clears" ); }
        [Test] public void Test_max_codes()                                 { ValidateConfig( "max-codes" ); }
        [Test] public void Test_max_height()                                { ValidateConfig( "max-height" ); }
        [Test] public void Test_max_size()                                  { ValidateConfig( "max-size" ); }
        [Test] public void Test_max_width()                                 { ValidateConfig( "max-width" ); }
        [Test] public void Test_missing_pixels()                            { ValidateConfig( "missing-pixels" ); }
        [Test] public void Test_no_clear_and_eoi()                          { ValidateConfig( "no-clear-and-eoi" ); }
        [Test] public void Test_no_clear()                                  { ValidateConfig( "no-clear" ); }
        [Test] public void Test_no_data()                                   { ValidateConfig( "no-data" ); }
        [Test] public void Test_no_eoi()                                    { ValidateConfig( "no-eoi" ); }
        [Test] public void Test_no_global_color_table()                     { ValidateConfig( "no-global-color-table" ); }
        [Test] public void Test_nul_application_extension()                 { ValidateConfig( "nul-application-extension" ); }
        [Test] public void Test_nul_comment()                               { ValidateConfig( "nul-comment" ); }
        [Test] public void Test_plain_text()                                { ValidateConfig( "plain-text" ); }
        [Test] public void Test_transparent()                               { ValidateConfig( "transparent" ); }
        [Test] public void Test_unknown_application_extension()             { ValidateConfig( "unknown-application-extension" ); }
        [Test] public void Test_unknown_extension()                         { ValidateConfig( "unknown-extension" ); }
        [Test] public void Test_xmp_data_empty()                            { ValidateConfig( "xmp-data-empty" ); }
        [Test] public void Test_xmp_data()                                  { ValidateConfig( "xmp-data" ); }
        [Test] public void Test_zero_height()                               { ValidateConfig( "zero-height" ); }
        [Test] public void Test_zero_size()                                 { ValidateConfig( "zero-size" ); }
        [Test] public void Test_zero_width()                                { ValidateConfig( "zero-width" ); }
    }
}
