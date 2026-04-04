# AetherBlackbox Architecture

Conditionflag.incombat in plugin triggers logging automatically.

In pullmanager, sessions stay open 15 seconds post-wipe to catch late network packets.

Replays are gzipped json. Metadata is written in utf-8 before compression for faster ui reads.

A custom serialization binder strips namespaces and assembly names. Refactoring event classes requires migration.

In replayrenderer, buffs and debuffs are filtered to remove irrelevant data like fc buffs and permanent stances.

Networkmanager uses binary for high-frequency updates and json for low-frequency metadata. The websocket server could become a bottleneck.

In canvascontroller, coordinate deltas are batched and sent every 40ms to balance tracking and server load.

Drawings are stored as vector data for zooming/panning without losing quality.

Icons and textures are cached in memory to avoid disk access during rendering.

In pullmanager, locks are applied around the deaths collection to prevent race conditions. Minimal locking, but some contention during wipes.

Replay frame data grows with encounter time. No chunking or streaming, so memory usage increases with long encounters.
