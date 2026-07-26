# Game design target

## Product statement

Lumen Circuit is a low-friction arcade kart racer for keyboard and controller. The first ten seconds should be understandable without a tutorial: accelerate, steer, hop into a drift, release for a burst, collect an orb, and use an item. Depth comes from line choice, drift timing, item timing, defensive positioning, and maintaining momentum through readable corners.

## Design pillars

1. **Immediate control clarity.** Steering reacts quickly at low speed and becomes calmer at high speed. The kart should feel planted rather than like a simulation vehicle.
2. **Expressive drift rhythm.** A hop commits the kart to a slide. Counter-steering changes charge rate and line shape. Releasing at a charge tier creates an unmistakable mini-turbo.
3. **Readable race state.** Position, lap, upcoming road, item state, and speed feedback remain legible at a glance.
4. **Fair chaos.** Items alter decisions but do not erase skill. Catch-up systems are bounded and visible in telemetry.
5. **Original presentation.** Mechanics may belong to the broad arcade-racing genre; characters, visual language, tracks, sounds, item silhouettes, UI composition, names, and branding must be original.

## First vertical slice

- One 90–120 second circuit.
- Eight racers, three laps.
- Keyboard and common controller inputs.
- Four original item types.
- Boost pads and pickup rows.
- Race countdown, live rank, timer, finish order, restart.
- AI with driving-line awareness and limited catch-up assistance.
- 1080p target: 60 FPS on a mid-range discrete GPU; code should remain scalable toward Steam Deck-class hardware.

## Handling target

The intended feeling is responsive and slightly elastic, not realistic. Forward speed is force-driven, while lateral velocity is deliberately damped. Steering authority follows a speed curve. Drift reduces lateral grip, adds controlled yaw authority, and accumulates charge only while grounded and moving. Mini-turbo tiers should reward sustained clean slides without requiring frame-perfect input.

Initial tuning is centralized in `KartTuning`. Do not hide handling constants throughout unrelated scripts.

## Item philosophy

- **Burst Drive:** direct acceleration and a raised speed ceiling.
- **Arc Pulse:** a forward energy pulse that lightly homes and causes a short spin.
- **Cloud Shield:** one defensive absorb plus a brief visible shield window.
- **Glimmer Trap:** a stationary hazard dropped behind the kart.

The visual design, names, color language, behavior tuning, and silhouettes must remain distinct from items in existing commercial kart racers.
