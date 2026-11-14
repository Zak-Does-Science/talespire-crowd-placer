# Crowd Placer

A small BepInEx plugin for [TaleSpire](https://talespire.com) that places a whole crowd of named minis onto a board with one keystroke.

## Why it exists

I run a weekly tabletop campaign in TaleSpire. One arc is set in a tournament arena, and an arena with empty stands looks like a fire drill. TaleSpire lets you copy and paste one mini at a time, or lasso up to 50 that are already on the board. Nothing could take a list of positions and names and put them down for you. I checked the existing mods; the ones that came close are no longer maintained or don't work with the current build.

So I built one. It went from research to a working plugin in a day, written with Claude Code.

## How it works

1. A script (mine are Python map generators) writes a `crowd.json` file: a marker position, a set of creature blueprints, and a list of creatures with positions, facing, names and a hidden flag.
2. In GM mode, you stand an "anchor" mini on the marker spot in your board and select it.
3. Press **Ctrl+Shift+K**. Crowd Placer decodes each blueprint, places every creature relative to the anchor, names it, and reports how many landed.

Placement is spread across frames so the game doesn't stall, and each spawn retries while a model is still loading the first time.

## The crowd file

```json
{
  "marker": {"x": 7.5, "y": 1.0, "z": 27.5},
  "blueprints": {"commoner": "talespire://creature-blueprint/..."},
  "creatures": [
    {"bp": "commoner", "x": 9.0, "y": 1.0, "z": 27.5, "rot": 270, "name": "Odile Fenn", "hidden": false}
  ]
}
```

- **Blueprints** are what TaleSpire puts on your clipboard when you copy a mini (Ctrl+C).
- **Positions** are in the same units as the slab you built the map from. The plugin only uses the offset from `marker`, so a crowd lands correctly wherever you paste the slab.
- **`rot`** is in degrees. One gotcha took real debugging to find: creatures turn the opposite way to slab props, and a copied blueprint carries the original mini's own rotation, which the game prefers over the one you pass in. The plugin overwrites it so `rot` means what you wrote.

`examples/crowd_facing_test.json` is the test I used to sort out the facing problem: minis in a ring, each told to face the middle.

## Install

You need TaleSpire with BepInEx 5 (for example through the Thunderstore mod manager).

```
dotnet build -c Release -p:TaleSpireDir="<your TaleSpire folder>" -p:BepInExCore="<your profile>\BepInEx\core"
```

Copy `bin\Release\net472\CrowdPlacer.dll` into that profile's `BepInEx\plugins` folder. The crowd file defaults to `BepInEx\config\crowd.json`; you can change the path and the hotkey in the plugin's config file.

## Status

It does what my game needs, and my players see the results at the table. It's a personal tool, not a polished release. TaleSpire updates may break the internal calls it relies on.
