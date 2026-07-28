# Free Yoga for Farmers

Free Yoga for Farmers lets an event temporarily swap the farmer's sprite sheet — body, shirt, and pants — for a
fully custom one, so events can show poses and actions the base game's animation set can't. It's
built to be driven entirely by **content packs**: Free Yoga for Farmers itself ships no art or events, it's a
framework other mods hook into.

This document is for anyone building a content pack that uses Free Yoga for Farmers.

---

## Requirements

Your content pack needs two dependencies in its `manifest.json`:

- **Content Patcher** — used to load your PNGs and add your event to the game (`Load`/`EditData`).
- **FarmerYoga** (unique ID `ioicaleb.farmeryoga`) — used to trigger the sprite swap from your event,
  and to auto-generate a hair/accessory offset file for your sheet.

```json
"Dependencies": [
  { "UniqueID": "Pathoschild.ContentPatcher" },
  { "UniqueID": "ioicaleb.farmeryoga" }
]
```

## File layout

A minimal content pack looks like this:

```
YourPack/
├── manifest.json
├── content.json
├── FY_Offsets.json← auto-generated the first time your pack loads, then yours to edit
└── <subfolder> (If you want to organize them into subfolders)/
    ├── farmer_<suffix>.png
    ├── farmer_girl_<suffix>.png
    ├── shirts_<suffix>.png
    └── pants_<suffix>.png
```

`<suffix>` is whatever short name you pick for this sprite set (e.g. `yoga1`). It's used consistently
across all four image names and in the event command that activates them.
`<subfolder>` is completely optional. You can store your images however you want.

### content.json

Use Content Patcher's `Load` action to register your images under the asset names Free Yoga for Farmers expects
(`Characters/Farmer/farmer_<suffix>`, `farmer_girl_<suffix>`, `shirts_<suffix>`, `pants_<suffix>`), and
`EditData` to add your event. For example:

```json
{
  "Format": "2.2.0",
  "Changes": [
    { "Action": "Load", "Target": "Characters/Farmer/farmer_<suffix>", "FromFile": "Images/farmer_<suffix>.png" },
    { "Action": "Load", "Target": "Characters/Farmer/farmer_girl_<suffix>", "FromFile": "Images/farmer_girl_<suffix>.png" },
    { "Action": "Load", "Target": "Characters/Farmer/shirts_<suffix>", "FromFile": "Images/shirts_<suffix>.png" },
    { "Action": "Load", "Target": "Characters/Farmer/pants_<suffix>", "FromFile": "Images/pants_<suffix>.png" },
    {
      "Action": "EditData",
      "Target": "Data/Events/Farmhouse",
      "Entries": {
        "yourEventKey": "none/5 5/farmer 5 7 2/skippable/changeFarmerSprite <suffix>/ ... your event ... /pause 1000/globalFade/pause 500/end"
      }
    }
  ]
}
```

You always need all four `farmer_`/`farmer_girl_`/`shirts_`/`pants_` images for a given suffix, even
if the shirt or pants art is just fully transparent placeholder — Free Yoga for Farmers doesn't require them to
contain anything, but it does look for them at those asset names.

## Using it in an event

Two custom event commands are available anywhere in an event script:

- **`changeFarmerSprite <suffix>`** — switches the farmer to your custom sheet, hides their equipped
  shirt/pants for the duration, and swaps in your hair/accessory offsets for this suffix.
- **`changeFarmerSprite base`** — reverts everything: texture, hair/accessory offsets, and the
  player's real shirt/pants.

You don't strictly have to call `changeFarmerSprite base` yourself — it happens automatically the
moment the event ends (`Game1.eventUp` becomes false) — but calling it explicitly partway through an
event is fine if you want to switch back before the event finishes.

### Always fade before `end`

**End every custom-sprite event with a `globalFade` (and a short `pause` after it) before `end`.**
Event teardown briefly shows the farmer's real vanilla appearance for a frame as the game's own
cleanup runs, independent of anything Free Yoga for Farmers does — this is normal Stardew event behavior, not a
bug, and the standard fix community-wide is the same one you'd want anyway for a smooth transition:

```
.../pause 1000/globalFade/pause 500/end
```

Skipping this will show a brief, jarring flash of the farmer's normal clothes right at the end of the
event.

## Sprite sheet conventions

All sheets use a 16×32 pixel grid per pose, matching the base game's convention.

- **`farmer_<suffix>.png` / `farmer_girl_<suffix>.png`** — your custom body. Each row is 32px tall.
  **Only the leftmost 6 columns of each row count as distinct poses** — this matches the vanilla
  layout, where the remaining columns to the right of those 6 are arm and accessory sub-frames for
  those same 6 poses, not additional poses. Your event's `animate`/frame commands should only ever
  reference indices within that 6-per-row range.
- **`shirts_<suffix>.png`** — 8×8 cells. Doesn't need to match the body sheet's dimensions — only its
  own internal frame count/order needs to line up pose-for-pose with the body (frame *N* here should
  be the same pose as frame *N* in the body sheet).
- **`pants_<suffix>.png`** — 16×32 cells, same pose-for-pose correspondence as shirts.
- All three are read left-to-right, top-to-bottom, wrapping to a new row automatically based on each
  file's own width — you don't need to pad a sheet out to some fixed size, just make sure it actually
  contains a cell for every pose your event uses.
- **There's no built-in "nude" flag or coverage rule.** Whatever you paint as the resting skin tone in
  the body sheet's torso/leg regions *is* the default look — there's nothing to toggle, and nothing
  stopping pants/shirts from being as minimal or as full-coverage as you draw them. Free Yoga for Farmers fully
  suppresses the vanilla fallback shirt/pants, so your art is the only thing that ends up on screen.

## Hair and accessory offsets (`FY_Offsets.json`)

Since your custom poses don't match the vanilla animation set, hair and accessories need their own
per-frame position offsets or they'll draw in the wrong place relative to your custom body.

The first time your content pack loads, Free Yoga for Farmers scans your `farmer_*.png` files and writes a
`FY_Offsets.json` at your pack's root, pre-filled with `[0, 0]` for every pose it found. From there,
use the in-game SMAPI console (not an event command) to dial them in:

```
setFrameOffset <suffix> <frame-index> <x-offset> <y-offset>
getFrameOffset <suffix> <frame-index>
```

- `frame-index` is the pose index within the 6-per-row body scheme described above (0-based).
- Offsets are in pixels; positive X is right, positive Y is down.
- `setFrameOffset` writes back to your pack's `FY_Offsets.json` immediately, so changes persist
  between sessions — playtest a pose, nudge it, and reload the event to check.
- Every `SheetID` (your `<suffix>`) must be unique across **every** loaded Free Yoga for Farmers content pack,
  not just your own — if two packs register the same suffix, only one will be used, and the console
  will log a warning telling you which pack won.

##Known limitations
Hair, hats, and accessories aren't repositioned by Free Yoga for Farmers beyond the offset system above. They're drawn using the game's normal per-frame offset tables, redirected to your FY_Offsets.json values — but there's currently no equivalent handling if your poses need something more elaborate than a simple X/Y nudge (e.g. hair needing to flip or restyle entirely for certain poses).
Multiplayer is not currently supported. The custom body texture is a synced field and should be visible to other players, but the shirt/pants suppression and overlay are scoped to your own client's view of your own farmer — other players will likely see your custom body with the default vanilla shirt/pants still showing rather than your custom clothing. Treat any multiplayer use as unsupported until this is revisited.
No automatic validation that shirt/pants pose counts match the body sheet. If shirts_<suffix> or pants_<suffix> has fewer poses than your body sheet actually uses, the mismatched poses will wrap around to reuse earlier frames rather than erroring — check visually across your full pose range rather than assuming coverage from a couple of test frames.

##Roadmap

Not yet implemented, but planned for a future update:

Proper hair/hat/accessory handling beyond simple per-frame X/Y offsets.
Multiplayer support.
Support for gender-neutral/non-binary player models (currently, sheets are only split along the game's existing male/farmer_-female/farmer_girl_ convention).
