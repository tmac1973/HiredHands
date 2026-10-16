# Followers and the Command Stone

Craft the **Command Stone** at a workbench with a hiring board of a high enough level within 30 m of it. Each quality
takes more followers:

| Quality | Board level | Followers | Materials |
|---|---|---|---|
| 1 | 2 | 1 | Surtling core 3, Greydwarf eye 20, Resin 20, Deer hide 10 |
| 2 | 4 | 2 | Iron 15, Guck 10, Ooze 10, Withered bone 10 |
| 3 | 6 | 3 | Black metal 15, Silver 10, Needle 10, Linen thread 10 |
| 4 | 8 | 4 | Flametal 15, Eitr 10, Black core 2, Molten core 2 |

## Controls

With the stone in hand (the list of your followers shows on the left of the screen):

| Input | On | Does |
|---|---|---|
| Left click | A board's hireling | Recruit it (needs the ward, if any) |
| Left click | Your follower at home | It's posted on that spot and goes back to its contract (see [Posts](#posts)) |
| Left click | Your follower in the field | Follow ↔ Stay |
| Left click | A tree or log / a rock | Your woodcutters / miners harvest it (and what it leaves behind), staying there (Stay) until you call them |
| Left click | An enemy | Your guards attack it |
| Left click | The ground | Your followers go there and hold; at home (within each one's area round the board) they're posted there |
| Right click | Your follower at home / a posted hireling | Back to work / clear the post |
| Right click | Your follower in another board's area | It **joins that board** and works there, if the board has room for it (its level, job and caps are checked as for a new contract; no fee). You need access to that board's area |
| Right click | Anything else | Recall: everyone within 50 m follows you |
| Middle click | Anywhere | **Retreat**: everyone drops the fight and runs with you, ignoring enemies, until 20 s pass without a hit |
| Shift+E | A hireling | Orders panel: follow mode (Follow, Stay, Gather Here), stance, what to gather, work at home, rename, send home, apply to all |
| E | Your follower | Its cargo, to load your own loot onto it |

- **On the map**: with the stone in hand, the map shows your followers and the hirelings of the boards you built as small
  coloured dots (workers blue, guards red, your followers gold) with their names, wherever they are. The pins go when you
  put the stone away.
- **Retreat key**: `RetreatKey` (Followers section, your own setting, unset by default) gives the retreat its own key or
  mouse button, so it works without the stone in hand.

## In the field

- **Gather Here**: a woodcutter or miner works around the spot you leave it until its cargo is full. A Steward, guard,
  archer or wizard picks up loot lying round its spot (`LootRadius`) between fights, and brings it home.
- **Sneaking**: crouch and your followers nearby crouch with you: slower, silent and harder to spot.
- **Keeping up**: followers sprint when you do, catch up faster when well behind, get themselves unstuck, and as a last
  resort reappear just behind you when you can't see them.
- **Portals and dungeons**: followers within 20 m go through with you. One carrying what the portal won't take (ore,
  metal) stays behind in Stay, as you would (`AllowNonTeleportableThroughPortals`). The world modifier that lets portals
  take everything applies to them too.
- **Ships**: take the helm (or stand on a ship that's under way) and your followers nearby board as passengers; they step
  off beside you when you're ashore. If the ship sinks they're left in the water to swim after you.

## Going home

*Send home* in the Shift+E panel walks a follower back to its board on a timer (25 s per 100 m, 1 to 20 minutes) with its
cargo; it delivers it and goes back to work. A follower left far behind, left in Stay while you're far away, or whose
owner has been offline for 5 minutes goes home the same way. With `ReturnHomeWithNonTeleportable` off, it first drops
what a portal wouldn't take, so going home isn't a free ore portal.

A follower of yours left inside its own board's area goes back to work there instead: in Stay or Gather Here after
2 minutes with you 150 m away, following after 30 s with you 60 m away, or 5 minutes after you log off.

## Posts

Any hireling can be posted on a spot at home: recruit it with the stone, then left click the spot (or the hireling
standing on it). The post has to be inside its area round the board.

- **Guards** stand watch there (a tower, a gate). They watch all round, fight by their stance and walk back to the post.
  Archers hold fire when the shot is blocked.
- **Workers** keep doing their jobs, but their area moves to the post (the same radius, centred there), and they wait at
  the post between jobs instead of at the board. Post the Cook in the kitchen, the Rancher by the pen, the Farmer by
  the fields. What they put in the chests round their post counts toward the board's orders.
- A gatherer with "Works at home" off waits at its post instead of wandering about the base.

Clear a post with a right click on the hireling (stone in hand) or *Clear post* in its Shift+E panel; it works round
the board again.
