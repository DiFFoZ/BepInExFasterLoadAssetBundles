# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.6.2] 2024-06-17
### Fixed
- Finding assetbundle metadata by hash failing.

## [0.6.1] 2024-06-16
### Changed
- Algorithm of hashing assetbundle from SHA1 to Hash128 (spookyhash).

## [0.6.0] 2024-06-16
### Added
- Big size assetbundles (larger than 300MB) are recompressed with LZ4 instead of uncompressed. This should fix crashes with very unoptimized mod assets.
- Deleting temp files on game start up.
- Deleting assetbundle metadata if uncompressed bundle was deleted.
- Pre-check to not recompress, if original assetbundle is already uncompressed or compressed with LZ4.
### Removed
- Deleting of the old cache that was introduced in v0.4.0.

## [0.5.0] 2024-04-24
### Changed
- All bundle loading by stream are now recompressed.
### Fixed
- Array leaking from the pool.

## [0.4.0] 2024-04-04
### Added
- Check of drive space before trying to decompress.
### Changed
- Moved cache folder to the game installation. 
    - The old cache folder will be deleted.
- Switching to main thread when decompress the bundle.
- Loading of uncompressed bundle to make them load faster.
### Fixed
- Exception that happens if mod trying to load non exists bundle.

## [0.3.1] 2024-03-28
### Fixed
- Exception that prevents to decompress bundle.

## [0.3.0] 2024-03-28
### Changed
- Cache folder is now global (`%userprofile%\AppData\LocalLow\<companyname>\<productname>`).

## [0.2.0] 2024-03-28
### Changed
- Decompression is now happens in background.
- Decompression thread priority is set to `Normal` instead of `High`.
- AssetBundle loaded via `FileStream` will be now cached.

## [0.1.0] 2024-03-25
### Added
- Debug log when decompressed assetbundle is loaded instead.
- `LastAccessTime` to the metadata.json file.
- Cached assetbundle will be deleted after 3 days of inactive usage.
### Changed
- Lock the metadata file when updating it.
### Fixed
- No logs are printed.

## [0.0.2] 2024-03-22
### Changed
- Catch any exception when trying to load decompressed assetbundle.
- Move logs to `ManualLogSource` instead of Console.

## [0.0.1] 2024-03-22
### Added
- Project files
