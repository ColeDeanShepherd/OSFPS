# OSFPS

## To-Do

#### Current

* Animate switching weapons.



* Improve client prediction.
  * Do correction in the past
  * Track timestamp on server, sync. with client, send with game states



* Implement delta game state sending.
  * fix bugs
  * handle player disconnects



* Fix destroy errors on close.

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
  * Make sure systems aren't updated twice.
  * Make sure changes to the game state aren't done twice.
* Improve server-side verification (use round trip time).
* Remove ECS system instances.
* Implement dead bodies.
* Pick up weapons when in range if already holding key and didn't just drop them.
* Show dead/alive players in scoreboard.