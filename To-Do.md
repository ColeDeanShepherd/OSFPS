# To-Do

#### M.V.P.

- Fix grenades going through objects.
- Make game look the same in the editor and the standalone player.
- 
- Reposition weapons relative to player.
- Fix bugs with hit detection.
- Show reload & equip animations on other players.
- Fix destroy errors on close.
- Add a way to close a server without closing the game.
- Check if spawn point is blocked before spawning.
- Show other players' pings.
- Handle message send errors when disconnecting gracefully?
- Fix bug with picking up weapons not always working based on distance. (Server de-sync.?)

#### High-Priority

- Improve map.
- Set is fire pressed to false when switching weapons.
- Implement winning/losing, and game restarts.
- Improve networked shot intervals (send time shot)
- Add more weapons.
  - plasma pistol?
  - needler?
  - sword?
  - plasma rifle?
- Add sanity checking for shot rays.
- Implement melee.
- Implement radar.
- Implement crouching.
- Implement assists.
- Implement assassinations.
- Implement FFA/teams.

#### Low-Priority

- Make sniper rifle shots visible if you shoot and don't move.
- Add listen servers.
  - Make sure systems aren't updated twice.
  - Make sure changes to the game state aren't done twice.
- Improve server-side verification (use round trip time).
- Remove ECS system instances.
- Implement dead bodies.
- Pick up weapons when in range if already holding key and didn't just drop them.
- Show dead/alive players in scoreboard.