# Black Box Reborn

Every run leaves a flight record. Black Box Reborn tracks who hauled the loot, who broke it and how much
is still out there, and shows it on a monitor in the truck and in a stats panel you open with **F6**.

A rebuilt successor to the original BlackBox idea, written from scratch for the current game version.

## What you get

**Truck monitor** (back wall of the truck, readable from across the truck)
- During a level: extracted, lost and remaining value, who damaged the most valuables and the latest event.
- In the shop and in the truck lobby: the report of the last level, with MVP and Butterfingers.

**Stats panel** (press **F6**, arrows to navigate, **F6** or **Esc** to close)
- **Level**: extracted / lost / discovered / remaining, a value bar and a live activity feed.
- **Players**: hauled value, value broken, valuables destroyed, hits, damage taken and deaths, plus the
  list of valuables each player damaged.
- **Run**: totals and a table with every level of the run.

**HUD**: value still left on the map next to the haul goal (in the truck by default).

## How it works

- **Client-side only.** Each player computes the stats from data the game already syncs, so it works
  whether or not the host or anyone else has it. No networked objects, no REPOLib.
- **Hauled value** is split by how long each player carried the valuable (pushing a cart counts too).
- **Damage** is split between everyone holding the valuable. If nobody is holding it, it goes to whoever
  touched it in the last 10 seconds (throws, things rolling away) or to the player right next to it
  (bumping into it, hitting it with something else).
- **Destroyed by something else?** The feed says what: an enemy, a trap, a player's grenade, or the
  valuable falling off the map. That damage goes to *Environment*, not to whoever was holding it.
  Valuables the map itself loses while the level is generating are not counted as lost.
- Loot bags (Valuable Box) are followed: their contents count when the bag is extracted.

## Configuration

`BepInEx/config/vicxelcode.BlackBoxReborn.cfg`

| Setting | Default | |
|---|---|---|
| General.Language | Auto | Auto follows the game language (English / Spanish) |
| Panel.ToggleKey | F6 | Key that opens the panel |
| Panel.Scale | 1 | Panel size |
| Monitor.Enabled | true | Monitor in the truck |
| Monitor.PageSeconds | 7 | Seconds per page in the shop report |
| HUD.RemainingValue | InTruck | InTruck / Always / Never |

## Notes

- Not affiliated with semiwork. No game art is used.
- Not affiliated with the original BlackBox by RoemisTeam; this mod contains none of its code or assets.
  You can uninstall the original BlackBox and its fixers.
