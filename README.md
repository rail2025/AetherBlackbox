# Aether Blackbox

**AetherDraw + Death Recap = visualized combat replays for FFXIV**

Visually inspect your positioning leading up to death.

* Track healing, damage taken, buffs, debuffs, and barriers
* Scrub through a timeline of events
* Search status effects
* Zoom, pan, and analyze encounters in a 2D replay view

<br>

<img width="1007" height="642" alt="abb main window" src="https://github.com/user-attachments/assets/9079f588-a708-4c50-8508-643517b5a869" />
<br>
<img width="1007" alt="abb demo" src="https://github.com/user-attachments/assets/d1607f4a-031c-4486-9377-281b3f969505" />
<br>
<img width="1007" alt="abb demo" src="https://github.com/user-attachments/assets/a62c3d79-929b-4a29-94b5-74db5e90e0b0" />
<br>

---

## Architecture

AetherBlackbox is designed as a high-performance combat replay and visualization system with real-time multiplayer synchronization.

👉 Full design details: **[architecture.md](./architecture.md)**

### Highlights

* **Zero-touch logging:** Automatically records encounters
* **Replay system:** Timeline scrubbing with historical state reconstruction
* **Real-time collaboration:** WebSocket-based telestration (binary + JSON protocols)
* **Vector canvas:** Lossless zoom/pan with minimal network overhead
* **Performance-focused:** Aggressive caching and controlled concurrency

---

## Credits & Attribution

This project is a combined and modified work based on the following AGPL-licensed projects:

* **Combat Log Parser**
  https://github.com/Kouzukii/ffxiv-deathrecap
  Used for parsing Dalamud combat logs and extracting the 30-second pre-death event timeline.

* **Drawing App**
  https://github.com/rail2025/AetherDraw
  Used as the basis for the ImGui-based interactive replay and visualization UI.

This repository contains significant modifications and additional functionality, including replacing tabular output with a scrubbable replay window rendered via ImGui.

---

## Installation

Add the following repository URL to Dalamud:

```
https://raw.githubusercontent.com/rail2025/AetherBlackbox/refs/heads/main/repo.json
```
