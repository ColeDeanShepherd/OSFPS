# OSFPS

## To-Do

#### Current

* Implement bandwidth moving average.
* Set is fire pressed to false when switching weapons.
* Animate switching weapons.
* Add assault rifle.
* Improve client prediction.
  * Do correction in the past
* 
* Create a cool map.
* Implement delta game state sending.
* Rethink state compression
  - Compress game state?
  - Delta compression?
    - For each game object
    - number packets
    - server & client cache previous snapshots
    - server delta encodes current state relative to each client's latest ack'ed snapshot
      - only send changed properties?
      - compress entire delta?
    - need to send reliable creation and deletion messages separately

#### High-Priority

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
* Show dead/alive players in scoreboard.

#### Low-Priority

* Health/shield bars.
* Make sniper rifle shots visible if you shoot and don't move.
* Add listen servers.
* Improve server-side verification (use round trip time).
* Improve throwing grenade straight down.
* Remove system instances.
* Implement dead bodies.
	 Pick up weapons when in range if already holding key and didn't just drop them.	 