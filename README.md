# OSFPS

## To-Do

#### Current

* Animate switching weapons.
* Improve client prediction.
  * Do correction in the past
* Improve throwing grenade straight down.
* Improve rocket launcher firing straight down
* Implement delta game state sending.
  - server & client cache previous snapshots
  - server delta encodes current state relative to each client's latest ack'ed snapshot

#### High-Priority

* Improve map.
* Set is fire pressed to false when switching weapons.
* Implement winning/losing, and game restarts.
* Improve networked shot intervals (send time shot)
* Add more weapons.
  - plasma pistol?
  - needler?
  - sword?
  - plasma rifle?
* Add sanity checking for shot rays.
* Implement melee.
* Implement radar.
* Implement crouching.
* Implement assists.
* Implement assassinations.
* Implement FFA/teams.

#### Low-Priority

* Make sniper rifle shots visible if you shoot and don't move.
* Add listen servers.
* Improve server-side verification (use round trip time).
* Remove ECS system instances.
* Implement dead bodies.
* Pick up weapons when in range if already holding key and didn't just drop them.
* Show dead/alive players in scoreboard.