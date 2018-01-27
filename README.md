# Colony Survival Construction Mod by Scarabol

## Installation

**This mod must be installed on server side!**

* download a (compatible) [release](https://github.com/Scarabol/ColonyConstructionMod/releases) or build from source code (see below)
* place the unzipped *Scarabol* folder inside your *ColonySurvival/gamedata/mods/* directory, like *ColonySurvival/gamedata/mods/Scarabol/*

## Blueprints

After installation *gamedata/mods/Scarabol/Construction/blueprints* **(on server side)** contains all available blueprints. Feel free to extent the offer, e. g. with the Save Tool.

The mod supports two types of blueprint formats. The first one is just the format the Save Tool generates. The second one is basically the Save Tool format with extra features (see examples in blueprints directory).

**You can convert your MC schematics into CS blueprints with [this tool](https://github.com/Log234/Schematic-Converter).** (not my own project)

## Capsules

Since **version 1.4** the mod provides *Capsules*. These are auto created for each blueprint and can be obtained by command. They're used to auto-build structures, without a constructor colonist, no resources needed and very fast. To gain a capsule use **/capsule [blueprintname]** e.g. */capsule gate*. To gain or use capsules the permission **mods.scarabol.construction.usecapsules** is mandatory.

## Build

* install Linux
* download source code
```Shell
git clone https://github.com/Scarabol/ColonyConstructionMod
```
* use make
```Shell
cd ColonyConstructionMod
make
```

**Pull requests welcome!**

