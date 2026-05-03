# QuickSheet Weather Extension

A demo extension for [QuickSheet](https://github.com/cemheren/QuickSheet) that displays a 7-day weather forecast in your spreadsheet grid.

## Installation

In any QuickSheet cell, type:
```
ext: github:cemheren/quicksheet-weather
```

## Usage

Once loaded, type in another cell:
```
wthr: 98112,2,7
```

This displays a 2-column × 7-row weather forecast below the cell.

Parameters: `wthr: <location>,<cols>,<rows>`

## Development

Requires .NET 9 SDK.

```bash
dotnet build WeatherExtension.csproj
```

### Protocol

This extension communicates with QuickSheet via JSON-lines over stdin/stdout:
- Receives `init` → responds with `register` (prefix: `wthr`)
- Receives `activate` → responds with `write` (cell data)
