# Doors, stairs and ladders

Inside a hiring board's area, hirelings find their way through your buildings: through closed doors and up and down
stairs to any floor you can walk to. The game's own walking map treats a closed door as a wall and often doesn't join the
floors of a house, so the board scans its area for doors and for anything that climbs from one floor to another, and
hirelings route through those when the game's map can't get them there.

- **Automatic, modded pieces included.** Stairs and ladders are recognised by their shape (a surface that climbs steadily
  from one floor to another), so build pieces from other mods work without any setup. The scan is redone a couple of
  seconds after anything near the board is built or removed.
- **Doors** are opened, walked through and closed behind (never in a player's face). Locked doors, doors under a ward the
  board's owner can't use, and doors you've marked to leave shut (Shift+E on the door) stay closed.
- **Ladders**: a hireling that can't physically climb one (or any stair it can't manage) hops to the top after a few
  seconds.
- **Followers** follow you through the house the same way while you're both inside a board's area. Elsewhere they
  follow as before.
- **Under roofs and in caves**: hirelings find the floor they stand on, not the roof above or (with HearthBelow) the hill
  over a dug-out cave.
- **Water**: hirelings keep to dry land where they can walk, and one stuck swimming in place is lifted onto dry ground
  after 30 seconds.
- **Settings** (*Work* section, server): `BaseNavLinks` (on) turns all of this off, back to the 0.2 handling;
  `HirelingsCloseDoors` (on) off leaves doors open behind them.
- **See what it found:** `vfh_navlinks show` draws every link (doors green, stairs yellow, ladders cyan, blocked red) and
  each hireling's route; `hide`, `scan` (rescan the nearest board now) and `list` (print its links).
- **A piece it gets wrong** (rare): name its prefab in the data file.

```yaml
navLinks:
  include: [MyModLadder]     # always try this piece as a stair or ladder
  exclude: [MyModFancyRamp]  # never use this piece as one
```
