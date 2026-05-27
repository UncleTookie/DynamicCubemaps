# Dynamic Cubemaps - Cities: Skylines Mod

Dynamic Cubemaps is a simple Cities: Skylines mod for assigning different environment cubemaps to the main day/night periods:

- Sunrise
- Day
- Sunset
- Night

The mod uses the game's current time period to swap the active cubemap automatically. Cubemaps can be selected from the Options menu.

## Using Dynamic Cubemaps

Open the game options for **Dynamic Cubemaps** and choose a cubemap for each slot:

- Sunrise Cubemap
- Day Cubemap
- Sunset Cubemap
- Night Cubemap

Click **Apply Settings** after changing selections.

Click **Reload Cubemaps** if you add or edit cubemap files while the game is running.

## Alternate Night Modes

Dynamic Cubemaps has three night behavior modes:

- **Default**: Uses the selected night cubemap with the map theme's normal nighttime horizon haze and fog colors.
- **Disable horizon haze at night**: Uses the selected night cubemap, but replaces the map theme's nighttime haze and fog colors with darker neutral tones. This can help when the night cubemap and map theme colors do not match.
- **Use vanilla mode at night**: Restores the vanilla night sky and disables custom night cubemap styling.

## Legacy HDRI Cubemap Packs

Existing Workshop cubemap packs using `CubemapReplacements.xml` can be detected and used by the mod.

The mod scans enabled mods and Workshop folders for `CubemapReplacements.xml`. HDRI, cubemap, and skybox names are also used as discovery hints for legacy packs. Any compatible legacy cubemaps found there are added to the dropdowns in the options menu.

## Making Your Own Cubemap Pack

Create a `CubemapReplacements.xml` file and one or more combined cubemap images. This repo includes a small example pack in `Examples`.

Example:

```xml
<?xml version="1.0" encoding="utf-8"?>
<CubemapReplacementsConfig>
  <CubemapReplacements>
    <CubemapReplacement code="clear_day" description="Clear Day" file_prefix="Textures/clear_day_" />
    <CubemapReplacement code="clear_night" description="Clear Night" file_prefix="Textures/clear_night_" />
  </CubemapReplacements>
</CubemapReplacementsConfig>
```

With this XML, the mod expects these files:

```text
Textures/clear_day_cubemap.png
Textures/clear_night_cubemap.png
```

Each image should be a combined cubemap in a 4x3 cross layout. The faces must be square. For example, a 4096x3072 image contains 1024x1024 cubemap faces.

There is a free online HDRI-to-cubemap converter available [here](https://hdri-to-cubemap-converter.vercel.app/). A 2048px face size is a good starting point. Higher resolutions can look sharper, but they will use more memory.

Each entry needs:

- `code`: unique internal ID
- `description`: name shown in the options dropdown
- `file_prefix`: path and filename prefix before `cubemap.png`

## Importing Custom Cubemaps

The simplest way to include custom cubemaps is to place `CubemapReplacements.xml` and the texture files in the mod folder with the DLL.

You can also package cubemaps in a separate local or Workshop mod folder using the same legacy XML format. Once the game sees the folder and the XML is valid, the cubemaps should appear in the Dynamic Cubemaps options menu.

## Limitations

- This mod has been tested with Lumina and Render It!, but compatibility with other visual mods may vary, especially when using the 'disable haze at night' mode. Report any bugs in the comments.
- Cubemap changes happen at the mod's day period boundaries.
- Cubemaps are loaded at their authored brightness. Night cubemaps may still behave more like color overlays depending on the map, lighting, and other visual mods. Night-focused cubemaps such as cloud-layer textures usually work best.
- Split cubemap formats are not supported; use a single combined `*_cubemap.png` image.
