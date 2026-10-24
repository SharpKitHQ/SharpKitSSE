[![Matrix](https://img.shields.io/matrix/sharpkitsse-dev%3Amatrix.org)](https://matrix.to/#/#sharpkitsse-dev:matrix.org)
[![GitHub Issues](https://img.shields.io/github/issues/SharpKitHQ/SharpKitSSE)](https://github.com/SharpKitHQ/SharpKitSSE/issues)
[![GitHub Pull Requests](https://img.shields.io/github/issues-pr/SharpKitHQ/SharpKitSSE)](https://github.com/SharpKitHQ/SharpKitSSE/pulls)
[![Download On NexusMods](https://img.shields.io/badge/Download%20On-NexusMods-orange)](https://www.nexusmods.com/skyrimspecialedition/mods/196677)
[![Latest Release](https://img.shields.io/github/v/release/SharpKitHQ/SharpKitSSE)](https://github.com/SharpKitHQ/SharpKitSSE/releases)

# SharpKit

SharpKit is an experimental open-source reimplementation of the [Bethesda CreationKit](https://en.uesp.net/wiki/Skyrim_Mod:Creation_Kit).

The goal of the project is to implement most of the functionality of vanilla CK, but faster and more stable, and then
start adding quality of life features missing from CK.

SharpKit is designed to be faster (where possible) and more stable (everywhere) than CreationKit.

Based on the excellent [Mutagen](https://github.com/Mutagen-Modding/Mutagen) library for plugin file handling and
[NiflySharp](https://github.com/ousnius/NiflySharp) for NIF file handling.

> [!WARNING]
> This project is in a very early stage of development, most features are not implemented yet, and those that are
> are likely catastrophically broken. Use at your own risk. The author(s) claim no responsibility for damage to your
> existing plugin files, save files, or anything else.

## Documentation

There is no documentation yet. The project is designed to closely mimic the look and behavior of Creation Kit, so until
documentation exists, use the [Creation Kit Wiki](https://ck.uesp.net/wiki/Main_Page) most of the concepts should be
applicable to SharpKit as well.

## Roadmap

This is a non-exhaustive list of features that are implemented or are planned to be implemented.

Feel free to submit a PR with additions to this list.

**General:**
 - [ ] Object Window (partial, not all types are added yet)
 - [ ] Cell View (partial, worldspaces and cells are shown, cell object list is not)
 - [ ] Script Editor (partial, property assignment not fully working yet)
 - [ ] Render Window (partial, display-only)

**QOL Functionality:**
 - [ ] Undo/Redo Functionality
 - [ ] Keyboard Shortcuts
 - [ ] 3D preview in model picker
 - [ ] "All" listing in object window

### Form Editors

(Editors that are in *italic* are considered low-priority because the base game has very few records of that type, 
~~crossed-out~~ editors are considered very low priority because the base game has no records of that type)

**Actors**
 - [ ] Actor
 - [ ] *Body Part Data*
 - [ ] Leveled Character
 - [ ] *Talking Activator*

**Audio**
 - [ ] *Accoustic Space*
 - [ ] Music Track
 - [ ] *Music Type*
 - [ ] *Reverb Parameters*
 - [ ] *Sound Category*
 - [ ] Sound Description
 - [ ] Sound Marker
 - [ ] Sound Output Model

**Character**
 - [ ] *Association Type*
 - [ ] Class
 - [ ] *Equip Slot*
 - [ ] Faction
 - [ ] Head Part
 - [ ] Movement Type
 - [ ] Package
 - [ ] Quest
 - [ ] Race
 - [ ] Relationship
 - [ ] *SM Event Node*
 - [ ] Voice Type


**Items**
 - [X] Ammo
 - [X] Armor
 - [ ] Armor Addon
 - [ ] Book
 - [ ] Constructible Object
 - [ ] Ingredient
 - [X] Key
 - [ ] Leveled Item
 - [ ] Misc Item
 - [ ] Outfit
 - [ ] *Soul Gem*
 - [ ] Weapon
 
**Magic**
 - [ ] *Dual Cast Data*
 - [ ] Enchantment
 - [ ] *Leveled Spell*
 - [ ] Magic Effect
 - [ ] Potion
 - [ ] Scroll
 - [ ] Shout
 - [ ] Spell
 - [ ] Word of Power

**Miscellaneous**
 - [ ] Anim Object
 - [ ] Art Object
 - [ ] *Collision Layer*
 - [ ] Color Form
 - [ ] Combat Style
 - [ ] Form List
 - [ ] Global
 - [ ] Idle marker
 - [X] Keyword
 - [ ] Land Texture
 - [ ] Load Screen
 - [ ] *Material Object*
 - [ ] Message
 - [ ] Texture Set

**Special Effect**
 - [ ] AddOn Node
 - [ ] Camera Shot
 - [ ] *Debris*
 - [ ] Effect Shader
 - [ ] Explosion
 - [ ] Footstep
 - [ ] *Footstep Set*
 - [ ] *Hazard*
 - [ ] Imagespace
 - [ ] Imagespace Modifier
 - [ ] Impact Data
 - [ ] Impact Data Set
 - [ ] ~~Lens Flare~~
 - [ ] Material Type
 - [ ] Projectile
 - [ ] ~~Volumetric Lighting~~

**World Data**
 - [ ] *Climate*
 - [ ] Encounter Zone
 - [ ] Lighting Template
 - [ ] Location
 - [ ] Location Ref Type
 - [ ] *Shader Particle Geometry*
 - [ ] Visual Effect
 - [ ] *Weather Type*
 - [ ] Weather

**World Objects**
 - [ ] Activator
 - [ ] Container
 - [ ] Door
 - [ ] Flora
 - [ ] Furniture
 - [ ] *Grass*
 - [ ] Light
 - [ ] Movable Static
 - [ ] Static
 - [ ] ~~Static Collection~~
 - [ ] Tree

## Setting up for development

**Prerequisites:**

 - .NET 10 SDK
 - [DirectX 11 Redist](https://www.microsoft.com/en-us/download/details.aspx?id=17431)
 - A copy of Skyrim SE/AE
 - A copy of the original CreationKit (this is not strictly neccessary but if you want to do anything with Papyrus, you will
   likely need the original Papyrus sources and compiler that come with CK)

**Setting up:**

1. Fork the project and clone it locally
2. Open `SharpKit.sln` in your favorite editor

**Building:**

Build the `SharpKit/SharpKit.csproj` project with `dotnet build`

## FAQ

Q: Why is this WPF and not (Avalonia/UNO Platform/WindowsForms/other)?
 : **A:** Because Skyrim is a Window-only game, and as much as I love linux, some of the features (especially the render window)
   are easier to implement and integrate in WPF. **The app should run reasonably well in Wine/Proton**

Q: Does/Will this support XYZ?
 : **A:** Check the [Roadmap](#roadmap). If the roadmap is missing an existing Creation Kit feature, please open a PR to add it.
   If you'd like to request a completely new feature, please open an issue.

Q: What about Fallout, Starfield, Oblivion, TES VI?
 : **A:** When the Skyrim SE version is done-ish, we can start trying to port this to other games. When TES VI comes out,
   it will likely become priority #1, especially if Bethesda is as slow with releasing the official CK as usual.

Q: What about OldRim support?
 : **A:** It's almost 2027, please let OldRim die in peace.

Q: Some other question
 : **A:** If you have any questions, please create an issue or reach out on [Element](https://matrix.to/#/#sharpkitsse-dev:matrix.org).

## AI Disclaimer

**Majority of the code in the main `SharpKit` project is hand-written by a human, with an occasional AI-assisted
snippet of code.**

Majority of the code in `SharpKit.Renderer` is vibe-coded by Codex, becuase I don't
know enough about DirectX to write that myself in a reasonable amount of time.

All AI-generated code in the project is thoroughly human-reviewed before being commited.

## AI Contibutions

This project accepts AI-assisted contibutions to the `SharpKit` project and vibecoded contributions to the
`SharpKit.Renderer` project, as long as they're well scoped, short enough to review and high quality (what
constitutes a high-quality contribution is entirely up to the project maintainers).

## License

SharpKit is licensed under the GPLv3 license.

SharpKit
Copyright (C) 2026 Jakub Sycha (UTF-8x), and contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

For the full text of the license, please see [LICENSE](LICENSE).

## Disclaimer

### Independence and Trademarks

This project is an independent, clean-room reimplementation of functionality associated with the Bethesda Creation Kit. 
It is not affiliated with, endorsed by, sponsored by, or otherwise associated with Bethesda Softworks LLC, 
Bethesda Game Studios, ZeniMax Media Inc., or any of their affiliates.

Bethesda, The Elder Scrolls, The Elder Scrolls V: Skyrim, Skyrim, Creation Kit, and other related names, logos, 
and marks are trademarks or registered trademarks of their respective owners, including Bethesda Softworks LLC and 
its affiliates, as applicable.

All third-party trademarks and intellectual property referenced in this project remain the property of their respective
owners. References to third-party products and trademarks are made solely for identification, compatibility, and 
descriptive purposes. No ownership of, or rights to, third-party trademarks or intellectual property are claimed.

This project is independently developed and is not an official Bethesda product. No reference to any third-party 
trademark implies affiliation with, sponsorship by, or endorsement from its owner.

### Good Faith and Legal Compliance

This project is developed in good faith as an independent software engineering effort. Its authors intend to respect 
applicable intellectual property laws and to avoid infringing the rights of third parties.

To the best of the authors' knowledge and understanding, the project's development practices are intended to comply 
with applicable European Union and national laws concerning copyright, software interoperability, and lawful software 
reverse engineering, including relevant exceptions and limitations where applicable.

The project aims to implement software functionality independently rather than reproduce protected expression or 
redistribute proprietary materials. Any reverse engineering undertaken is intended to be limited to what the authors 
believe is legally permissible for purposes such as understanding software behavior, interoperability, and independent
implementation.

No proprietary source code, assets, or other materials belonging to third parties are intentionally claimed as the 
project's own. Any third-party materials included in the project, if any, should be identified and used only under an
applicable license or other lawful basis.

### No Legal Guarantee

These statements reflect the authors' intentions and understanding of applicable law; they are not a representation or 
guarantee that every aspect of the project, its development, distribution, or use complies with all applicable legal
requirements.

Laws vary by jurisdiction, and their application depends on the specific facts and circumstances. Users and 
contributors are responsible for ensuring that their own use, modification, and distribution of the project comply 
with applicable laws and third-party rights.

All third-party intellectual property remains the property of its respective owner.