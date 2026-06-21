# CardCreator

A desktop application for designing and exporting trading cards, built with Avalonia UI and C#.

Cards are rendered at 1500x2100px (600 DPI print-ready) with swappable color-themed frame overlays rendered in Blender using Cycles.

---

## Features

- Create, edit, save, and delete cards stored in a local SQLite database
- Live card preview with layered zone overlays
- Swappable frame color themes (20 colors)
- Load custom card art from disk
- Export card canvas as a high-resolution PNG
- Cross-platform (Linux and Windows)

---

## Tech Stack

- [Avalonia UI](https://avaloniaui.net/) — cross-platform .NET UI framework
- C# / .NET 8
- SQLite via [Dapper](https://github.com/DapperLib/Dapper) and Microsoft.Data.Sqlite
- Frame overlays rendered in Blender (Cycles)

---

## Requirements

- .NET 8 SDK
- Linux or Windows desktop environment

---

## Getting Started

```bash
git clone https://github.com/yourname/CardCreator.git
cd CardCreator/CardCreator
dotnet run
```

The database is created automatically on first run at:

- **Linux:** `~/.config/CardCreator/cards.db`
- **Windows:** `%APPDATA%\CardCreator\cards.db`

---

## Asset Structure

Frame overlays are loaded from disk at runtime and are not embedded in the binary. Each color theme lives in its own folder:

```
Assets/
  art_placeholder.png
  avalonia-logo.ico
  frames/
    silver/
      silver_base.png
      cost.png
      title.png
      top_right.png
      name_bar.png
      bottom_left.png
      bottom_center.png
      bottom_right.png
    gold/
      ...
    blue/
      ...
```

When publishing, the `Assets/frames/` directory is copied alongside the binary automatically.

---

## Available Frame Colors

red, blue, green, gold, purple, black, white, silver, bronze, crimson, teal, orange, pink, navy, forest, copper, ice, obsidian, bone, toxic

---

## Publishing

**Linux:**
```bash
dotnet publish -r linux-x64 --self-contained -c Release
```

**Windows:**
```bash
dotnet publish -r win-x64 --self-contained -c Release
```

---

## License

MIT
