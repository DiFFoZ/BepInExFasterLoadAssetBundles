# BepInExFasterLoadAssetBundles
Makes startup loading time faster by **60%**.

## What it does
Before loading asset bundles, they will be decompressed into `Lethal Company Game/Cache/AssetBundles`. Decompressing can help with slow loading of asset bundles or high RAM usage.

## Where decompressed asset bundles are stored
They are stored in `Lethal Company Game/Cache/AssetBundles`.

## Incompatibilities
Currently only 2 mods are not compatiable with this mod:
- [XUnity_AutoTranslator](https://thunderstore.io/c/lethal-company/p/Hayrizan/XUnity_AutoTranslator/). Tracked by [issue #9](https://github.com/DiFFoZ/BepInExFasterLoadAssetBundles/issues/9).
- [IntroTweaks]. No fix will be made.

## Links
- [Thunderstore](https://thunderstore.io/c/lethal-company/p/DiFFoZ/BepInEx_Faster_Load_AssetBundles_Patcher/)
