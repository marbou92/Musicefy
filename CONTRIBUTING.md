# Contributing to Musicefy

Thank you for your interest in contributing to Musicefy!

## Development Setup

### Prerequisites
- Visual Studio 2019+ (MSVC v142)
- Qt 5.15.2 (MSVC 2019 64-bit)
- CMake 3.16+
- Ninja (recommended) or Visual Studio generator

### Getting Started
1. Fork and clone the repository
2. Install Qt 5.15.2 and set `CMAKE_PREFIX_PATH` to your Qt installation
3. Build using the instructions in README.md
4. Run tests with `ctest --output-on-failure`

### Code Style
- C++17 standard
- Use namespaces: `mf::core::*` for core, `mf::app::*` for application
- Header guards via `#pragma once`
- Qt coding conventions for signals/slots naming
- All code must compile with `/WX` (warnings as errors)

### Testing
- Add tests for new features in the `tests/` directory
- Tests use Qt Test framework
- Run the full suite before submitting a PR

### Pull Requests
1. Create a feature branch from `main`
2. Make your changes with clear commit messages
3. Ensure all tests pass
4. Submit a PR with a description of the changes
