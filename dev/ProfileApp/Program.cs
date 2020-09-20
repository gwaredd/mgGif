using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ProfileApp
{
    class Program
    {
        static void Main( string[] args )
        {
            var dir       = @"C:\dev\mgGIF\Assets\StreamingAssets";
            var filenames = Directory.GetFiles( dir, "*.gif" );
            var filedata  = ( from file in filenames select File.ReadAllBytes( file ) ).ToArray();

            int  count      = 0;
            long sum        = 0;
            long sumSquares = 0;

            var decoder = new MG.GIF.Decoder();

            while( true )
            {
                var sw = new Stopwatch();

                sw.Start();

                foreach( var file in filedata )
                {
                    decoder.Load( file ).GetImages();
                }

                sw.Stop();

                count++;

                sum += sw.ElapsedMilliseconds;
                sumSquares += sw.ElapsedMilliseconds * sw.ElapsedMilliseconds;

                var average  = (float) sum / count;
                var variance = sumSquares / count - average * average;

                Console.WriteLine( $"[{count:00}]: av {average:0.0}ms, sd {Math.Sqrt( variance ):0.0} - {sw.ElapsedMilliseconds}ms" );
            }
        }
    }
}
