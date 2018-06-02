# OSFPS

## To-Do

#### Current

* Auto pickup weapon if empty or matches type
* Improve networked shot intervals (send time shot)
* Fix mouse visibility.
* Fix "InvalidOperationException: The NativeArray has been deallocated, it is not allowed to access it"
* Fix flashing weapon when changing weapons.
* Handle message too long.
  * Compress game state?
  * Delta compression?
    * number packets
    * client send acks
    * server tracks latest ack for each client
    * server & client cache previous snapshots
    * server delta encodes current state relative to each client's latest ack'ed snapshot
      * only send changed properties?
      * compress entire delta?
* Add gun recoil.
* Add more weapons.
  * battle rifle?
  * shotgun
  * sniper rifle
  * plasma pistol?
  * needler?
  * sword?
  * plasma rifle?
* Add sounds.
* Create a cool map.

#### High-Priority

* Add sanity checking for shot rays.
* Add sanity checking for
* Implement melee.
* Implement radar.
* Implement crouching.
* Implement assists.
* Implement winning/losing, and game restarts.
* Implement assassinations.
* Auto-reload when no bullets in mag.
* Implement FFA/teams.
* Show dead/alive players in scoreboard.
* Show player names above head.
* Add listen servers.

#### Low-Priority

* Allow cancelling connecting to server.
* Add customizable key bindings.
* Add bullet holes.
* Improve server-side verification (use round trip time).
* Set is fire pressed to false when switching weapons.
* Improve throwing grenade straight down.
* Implement delta game state sending.
* Remove system instances.
* Improve chat UI.
* Implement dead bodies.