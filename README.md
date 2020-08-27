# mgGIF
> Parses a GIF file and extracts the images

![Butterfly](https://i.giphy.com/media/7OM8aqLAr2xQ4/giphy.webp)

## Installation

There is only one file, copy `Assets\mgGif\mgGif.cs` to your project.

## Usage

```
byte[] bytes = File.ReadAllBytes( filename );

var gif = MG.GIF.Decoder.Parse( bytes );

vr tex = gif.GetFrame( 0 ).CreateTexture();
```

See ` Assets\Scenes\AnimatedTextures.cs` for an example

