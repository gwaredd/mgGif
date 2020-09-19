# mgGIF
> A unity library to parse a GIF file and extracts the images, just for fun

![Butterfly](https://gwaredd.github.io/mgGif/butterfly.gif)

## Installation

There is only one file, copy [Assets\mgGif\mgGif.cs](https://github.com/gwaredd/mgGif/blob/master/Assets/mgGif/mgGif.cs) to your project.

Alternatively, the [upm](https://github.com/gwaredd/mgGif/tree/upm) branch can be pulled directly into the `Packages` directory, e.g.

```
git clone -b upm git@github.com:gwaredd/mgGif.git
```

## Usage

Pretty straight forward, pass a `byte[]` and receive an array of raw decompressed images in return.

See ` Assets\Scenes\AnimatedTextures.cs` for an example

```cs
byte[] bytes = File.ReadAllBytes( filename );

var images = MG.GIF.Decoder.Parse( bytes );
var tex = images[0].CreateTexture();
```

Alternatively, whilst the code is reasonably fast, you can also spread the cost across several frames inside a coroutine if required.

```cs
IEnumerator TimeSliceDecoding() 
{
  var decoder = new MG.GIF.Decoder()

  decoder.Load( bytes );

  var img = NextImage();

  while( img != null )
  {
      img.CreateTexture();
      yield return null;
      img = NextImage();
  }
}
```
Note, for convenience with animations there are also `GetFrame()` and `GetNumFrames()` extensions on the `MG.GIF.Image[]` type that skip instant frames (0 delay).

