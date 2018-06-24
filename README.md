# OSFPS

## To-Do

#### Current

* 
* 
* Create a cool map.
* Implement delta game state sending.
* Rethink state compression
  - Compress game state?
  - Delta compression?
    - For each game object
      - initialize a bit mask with a bit for each property
      - if bit set to 1, the property changed
      - if bit set to 0, the propert didn't change
      - for all properties, prepend with change bit in buffer
      - if property changed, write the property value in the buffer as well,
      - When reading the packet, look for change bits set to 1
      - If read change bit set to 1, actually read and set the property
      - I think I need to change from serializing lists of each type of object to serializing a list of all objects marked by type
    - number packets
    - server & client cache previous snapshots
    - server delta encodes current state relative to each client's latest ack'ed snapshot
      - only send changed properties?
      - compress entire delta?
    - need to send reliable creation and deletion messages separately

#### High-Priority

* Pick up weapons when in range if already holding key and didn't just drop them.	
* Implement winning/losing, and game restarts.
* Improve networked shot intervals (send time shot)
* Add more weapons.
  - plasma pistol?
  - needler?
  - sword?
  - plasma rifle?
  - Add assault rifle.
* Add sanity checking for shot rays.
* Implement melee.
* Implement radar.
* Implement crouching.
* Implement assists.
* Implement assassinations.
* Auto-reload when no bullets in mag.
* Implement FFA/teams.
* Show dead/alive players in scoreboard.

#### Low-Priority

* Use unity animations for recoil.
* Add listen servers.
* Improve server-side verification (use round trip time).
* Set is fire pressed to false when switching weapons.
* Improve throwing grenade straight down.
* Remove system instances.
* Implement dead bodies.