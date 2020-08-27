# mgGIF
> A unity library to parse a GIF file and extracts the images

![Butterfly](https://gwaredd.github.io/mgGif/butterfly.gif)

## Installation

There is only one file, copy `Assets\mgGif\mgGif.cs` to your project.

Alternatively, the [upm](https://github.com/gwaredd/mgGif/tree/upm) branch can be pulled directly into the `Packages` directory, e.g.

```
git clone -b upm git@github.com:gwaredd/mgGif.git
```

Or added to your `Packages/manifest.json` file

```
{
  "dependencies": {
    "com.gwaredd.mggif": "https://github.com/gwaredd/mgGif.git#upm",
    ...
  }
}
```

## Usage

```
byte[] bytes = File.ReadAllBytes( filename );

var gif = MG.GIF.Decoder.Parse( bytes );
var tex = gif.GetFrame( 0 ).CreateTexture();
```

See ` Assets\Scenes\AnimatedTextures.cs` for an example

