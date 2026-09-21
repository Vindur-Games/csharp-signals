# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-09-21

### Added
- Initial release of Vindur Signals Unity package (`com.vindur.signals`).
- Basic reactive primitives: `Signal.State`, `Signal.Computed`, `Signal.Linked`, and `Signal.Effect`.
- Dependency tracking with cycle detection and glitch-free updates.
- Untracked reads via `Signal.Untracked`.
- Dedicated assembly definition `Vindur.Signals`.
- Project structure prepared for upcoming Unity-specific extensions (`Runtime/Unity/` and `Editor/`).
