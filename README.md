# D3DShared

A shared C# library providing reusable Direct3D 12 rendering infrastructure and utilities for building 3D graphics applications on Windows.

## Overview

D3DShared abstracts away the complex boilerplate of Direct3D 12 initialization and resource management, providing a clean foundation for D3D12-based applications. It includes a full rendering pipeline, 3D model loading, and common UI helpers.

## Features

- **D3D12 Renderer** - Complete Direct3D 12 initialization, swap chain management, frame synchronization with fences, and a built-in standard shader with 3-light Phong lighting
- **OBJ Model Loader** - Wavefront OBJ file parser with mesh deduplication, bounding box calculation, model centering, and triangle mesh extraction
- **Data Structures** - Vertex and constant buffer types for shader communication (matrices, lighting parameters)
- **Settings Management** - JSON-based configuration persistence with adapter selection and generic serialization
- **UI Helpers** - Standard form creation, FPS counter, and status label components

## Tech Stack

- **Language:** C# (with unsafe code)
- **Framework:** .NET 10.0 (Windows)
- **Graphics API:** Direct3D 12 via [Vortice](https://github.com/amerkoleci/Vortice.Windows) bindings
- **UI:** Windows Forms

## Dependencies

- Vortice.Direct3D12 v3.8.1
- Vortice.DXGI v3.8.1
- Vortice.D3DCompiler v3.8.1

## Project Structure

```
D3DShared/
├── D3DShared.sln
└── D3DShared/
    ├── D3DShared.csproj
    ├── D3D12Renderer.cs    # Core D3D12 rendering pipeline
    ├── ObjLoader.cs         # Wavefront OBJ model loader
    ├── DataStructures.cs    # Vertex and constant buffer structs
    ├── Settings.cs          # JSON settings persistence
    └── UIHelpers.cs         # Common UI components
```

## Usage

This library is referenced as a shared dependency by other projects in the D3DDotNet solution (e.g., HairSim).

---

*Created with [Claude Code](https://claude.ai/claude-code)*
