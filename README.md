---

# Aether Blackbox

See what actually caused your wipe.

Aether Blackbox records your pulls and lets you replay them in a 2D timeline. You can scrub through positioning, damage, and mechanics, or sync it with your own footage to see what happened from your POV.

---

<br>

<img width="1007" height="642" alt="abb main window" src="https://github.com/user-attachments/assets/fab0596c-f07f-4844-bfb7-d10eb01e8bc1" />

---

## How it works

1. Do a pull like normal (it records automatically)
2. Open the replay and scrub back through the fight
3. Check positioning, damage, buffs, etc leading up to the wipe

If you want more detail or want to share things with your group, you can export it to the web app.

---

## What you can do

### Replay fights

* Scrub through a timeline of the encounter
* See where everyone was standing at any moment
* Track damage taken, healing, buffs, and barriers

---

### Sync with your footage

![VOD sync](docs/vod-sync.gif)

* Link your OBS recordings to the replay
* Watch your POV alongside the 2D timeline

---

### Draw on the fight

<img width="1007" alt="abb demo" src="https://github.com/user-attachments/assets/e2679e01-9274-454a-b884-0230e857395d" />

* Add annotations directly on the arena
* Draw markers that stick to players or bosses over time

---

### Save and reuse stuff

* Turn moments into simple slides
* Build a small library of strats or wipe reviews

---

## Where it runs

### In-game plugin

* Records encounters automatically
* Lets you immediately review a wipe
* Basic replay and drawing tools

### Web app

* VOD sync
* Better tools for reviewing and sharing
* Saved replays and plans

---

## When this is useful

* "where was I standing"
* "why did that mechanic hit me"
* "what actually killed us here"

---

## Getting started

<!-- Link to your site / tutorial -->

* Walkthrough: [https://blackbox.aetherdraw.me](https://blackbox.aetherdraw.me)
* Download / install: [https://github.com/rail2025/AetherBlackbox](https://github.com/rail2025/AetherBlackbox)

---

## Notes

* Works best if you already record your gameplay (for VOD sync)
* You can still use the replay without any video

---



<br>
<img width="1007" alt="abb demo" src="https://github.com/user-attachments/assets/fd3aae03-a96e-4ca6-b2d7-cef4e8121a25" />
<br>

<br>

---

## Architecture

AetherBlackbox is designed as a high-performance combat replay and visualization system with real-time multiplayer synchronization.

design details: **[architecture.md](./architecture.md)**

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
