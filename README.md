# mgGIF
> A unity library to parse a GIF file and extracts the images, just for fun

![Butterfly](https://gwaredd.github.io/mgGif/butterfly.gif)

## Installation

Copy [Assets\mgGif\mgGif.cs](https://github.com/gwaredd/mgGif/blob/master/Assets/mgGif/mgGif.cs) to your project.

Alternatively, the [upm](https://github.com/gwaredd/mgGif/tree/upm) branch can be pulled directly into the `Packages` directory, e.g.

```
git clone -b upm git@github.com:gwaredd/mgGif.git
```

## Usage

Pass a `byte[]` of the GIF file and loop through results.

```cs

byte[] data = File.ReadAllBytes( "some.gif" );

using( var decoder = new MG.GIF.Decoder( data ) )
{
    var img = decoder.NextImage();

    while( img != null )
    {
        Texture2D tex = img.CreateTexture();
        img = decoder.NextImage();
    }
}
```

For speed, the decoder will reuse the memory between each `NextImage()` call. If you need to keep the image data then you must `Clone()` it.

See [Assets\Scenes\AnimatedTextures.cs](https://github.com/gwaredd/mgGif/blob/master/Assets/Scenes/AnimatedTextures.cs) for an example

