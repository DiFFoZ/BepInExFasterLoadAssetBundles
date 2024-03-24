# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
