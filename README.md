# OSFPS

## To-Do

#### Current

* Add customizable key bindings.

#### High-Priority

* Implement winning/losing, and game restarts.
* Rethink state compression.
  - Compress game state?
  - Delta compression?
    - number packets
    - client send acks
    - server tracks latest ack for each client
    - server & client cache previous snapshots
    - server delta encodes current state relative to each client's latest ack'ed snapshot
      - only send changed properties?
      - compress entire delta?
* Auto pickup weapon if empty or matches type
* Improve networked shot intervals (send time shot)
* Add more weapons.
  - battle rifle?
  - assault rifle
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
* Auto-reload when no bullets in mag.
* Implement FFA/teams.
* Show dead/alive players in scoreboard.
* Add listen servers.

#### Low-Priority

* Improve server-side verification (use round trip time).
* Set is fire pressed to false when switching weapons.
* Improve throwing grenade straight down.
* Implement delta game state sending.
* Remove system instances.
* Improve chat UI.
* Implement dead bodies.