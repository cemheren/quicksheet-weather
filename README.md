# QuickSheet Weather Extension

A demo extension for [QuickSheet](https://github.com/cemheren/QuickSheet) that displays a 7-day weather forecast in your spreadsheet grid.

## Installation

In any QuickSheet cell, type:
```
ext: github:Deskworks/quicksheet-weather
```

## Usage

Once loaded, type in another cell:
```
wthr: Seattle,2,7
```

This displays a 2-column × 7-row weather forecast below the cell.

Parameters: `wthr: <location>,<cols>,<rows>`

## Development

Requires .NET 9 SDK.

```bash
dotnet build WeatherExtension.csproj
```

### QuickSheet Extension Protocol

Extensions communicate with QuickSheet via **JSON-lines** (one JSON object per line) over **stdin/stdout**.

#### Manifest (`quicksheet-extension.json`)

Every extension ships a manifest that tells QuickSheet how to launch it:

```json
{
  "name": "weather",
  "version": "1.0.0",
  "prefix": "wthr",
  "description": "7-day weather forecast widget for QuickSheet",
  "entry": "dotnet run --project WeatherExtension.csproj",
  "minProtocolVersion": 1
}
```

| Field | Description |
|-------|-------------|
| `name` | Unique extension identifier |
| `version` | Semver version string |
| `prefix` | The cell prefix that triggers this extension (e.g. `wthr:`) |
| `description` | Human-readable summary |
| `entry` | Shell command QuickSheet uses to start the extension process |
| `minProtocolVersion` | Minimum protocol version the extension supports |

#### Message Flow

```
QuickSheet                          Extension
   │                                    │
   │──── {"type":"init"} ──────────────▶│
   │◀─── {"type":"register",...} ───────│
   │                                    │
   │──── {"type":"activate",...} ──────▶│
   │◀─── {"type":"write",...} ──────────│
   │     (or {"type":"error",...})      │
```

#### Messages from QuickSheet → Extension

**`init`** — Sent once when the extension process starts.

```json
{"type": "init"}
```

**`activate`** — Sent when a user enters a cell matching the extension prefix.

```json
{
  "type": "activate",
  "id": "cell-abc123",
  "gridCols": 2,
  "gridRows": 7,
  "params": ["London"]
}
```

| Field | Description |
|-------|-------------|
| `id` | Unique cell/request identifier (must be echoed back) |
| `gridCols` | Number of columns available for output |
| `gridRows` | Number of rows available for output |
| `params` | Array of string parameters from the cell (e.g. `wthr: London` → `["London"]`) |

#### Messages from Extension → QuickSheet

**`register`** — Sent in response to `init`. Declares the extension's prefix.

```json
{
  "type": "register",
  "prefix": "wthr",
  "name": "Weather Forecast",
  "version": "1.0.0"
}
```

**`write`** — Sends cell data back to the grid.

```json
{
  "type": "write",
  "id": "cell-abc123",
  "cells": [
    {"r": 0, "c": 0, "v": "Mon"},
    {"r": 0, "c": 1, "v": "☀️ 75°/60°F"}
  ]
}
```

Each cell object has:
| Field | Description |
|-------|-------------|
| `r` | Row index (0-based, relative to the trigger cell) |
| `c` | Column index (0-based) |
| `v` | String value to display |

**`error`** — Reports an error for a specific request.

```json
{"type": "error", "id": "cell-abc123", "message": "Location not found"}
```

**`log`** — Optional diagnostic logging (not shown to users).

```json
{"type": "log", "level": "info", "message": "Fetching forecast..."}
```

`level` can be `"info"`, `"warn"`, or `"error"`.
