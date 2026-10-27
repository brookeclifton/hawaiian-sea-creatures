# Hawaiian Reef for the CyberCANOE wall

This folder builds a swimmable Hawaiian reef for the CC_CANOE stereo wall rig. It covers
steps 4-8 of the plan: block-out, the underwater look, sea life, swimming, and selection.
One editor window, **Tools > Reef Builder**, builds all of it. Every species starts as a
generated placeholder, so the whole experience works before any Sketchfab model is in. You
then swap your real models in one at a time.

Built for: Unity 6000.5 · URP 17.5 (Forward+) · Input System · CC_CANOE 2026-04-03, LAVA LED
wall (6.19 × 1.75 m, side-by-side, viewer 4.28 m from the wall).

---

## Quick start

1. Open `Assets/CC_ALL/CC_LAVA_LED_WALL_SCENE`.
2. Open **Tools > Reef Builder**.
3. Click **Save scene as Assets/Scenes/ReefScene.unity**. This keeps the CC_CANOE demo
   scene untouched and puts your scene first in Build Settings.
4. Click **Build everything (1-5)**. It takes about 10-30 seconds the first time, because it
   also generates the caustic frames and placeholder models. If it asks to import
   *TextMesh Pro Essential Resources*, allow it, then click Build again.
5. Save the scene (Cmd/Ctrl+S) and press **Play**.

In the Game view the image appears squashed and side-by-side. That's normal: CC_CANOE is
rendering both eyes. To look around at a normal aspect while you work, use the Scene view.

## Controls

| Xbox controller | Keyboard (for development) | Action |
|---|---|---|
| Left stick | W A S D | Swim (toward where you look) |
| Right stick | Arrow keys | Look (yaw, limited pitch, auto-levels) |
| RT / LT | Space / C | Rise / sink |
| Click left stick (hold) | Left Shift | Boost |
| A | E or Enter | Examine the highlighted species (info card); in the recipe game this also collects an ingredient |
| D-pad / left stick ← → | Arrow keys or A / D | Choose a recipe on the start screen |
| Menu (☰) | M | Back to the recipe start screen |
| B | Q or Backspace | Close the info card |
| D-pad ← / → | Tab / Shift+Tab | Cycle between species in view |
| X | F | Collect (only when collecting is turned on) |
| Hold View (≈1 s) | Hold R | Glide back to the start |
| n/a | Esc | Quit (CC_CANOE) |

---

## What the builder makes

Everything generated goes under **`REEF (generated)`**. Each button replaces only its own
child, so you can rebuild one step without losing the others. Anything you place yourself
outside that object is never touched.

| Step | Creates |
|---|---|
| **1 Set up CC_HUB** | Adds `ReefCCRig`, `SwimController`, `SpeciesSelector` and `SpeciesBasket` to CC_HUB. Disables `DPadFlyer` and `CCaux_Waypointer`. Sets camera near/far to 0.05 / 120 m and turns on post-processing for all 3 cameras. Creates **Bounds and Spawn**. |
| **2 Seafloor** | Terrain (150 m) with gently sloping sand, **spur-and-groove** reef ridges running offshore (+Z), and patch reefs out on the sand. A sand channel runs straight out from the spawn point. The ground is painted as sand, reef rock and rubble. |
| **3 Scatter** | Every layer in the settings: basalt boulders, mound, branching and plate corals, patch-reef coral heads, shells and tiger shark teeth on the sand, and urchins. |
| **4 Underwater look** | Sun with **animated caustics** on its light cookie, water-coloured exponential fog that darkens with depth, a trilight ambient from above, an underwater sky (with Snell's window), the underside of the water surface, 3D light shafts, **marine snow**, and a post-processing volume (neutral tonemap, bloom, red absorption). |
| **5 Sea life** | A tiger shark on a wide loop that fades in and out of the fog, a green sea turtle circling the "hero" coral head near the start, a turtle resting on the sand beside it, territorial humuhumunukunukuāpuaʻa on the spur tops, and placeholder fish schools. |

The layout is deterministic. Change `seed` in the settings to get a different reef.

### Settings: `Assets/ReefProject/ReefBuildSettings.asset`

Click **Edit** in the Reef Builder window. The main knobs:

- **General / Swimmable area**: surface height, bounds size, spawn position and heading.
- **Seafloor**: spur spacing, width and height, where the spurs start and end, and the patch reef count and size.
- **Layers**: one entry per kind of scatter. Put your prefabs in **Prefabs** (empty means placeholders). Also here: the zone, count, scale range, spacing, clustering, slope alignment, collider, and selectable settings (fraction and species).
- **Key characters**: `tigerShark`, `swimmingTurtles` and `restingTurtles`. Each has a prefab, species, count, speed, route radius and height.
- **Other characters**: any other animal (eel, ray, octopus, monk seal, …). Add one entry per species, assign its prefab and SpeciesData, and pick a route: *Wide Reef Loop*, *Patch Reef Loop*, *Rest On Sand* or *Hover Near Reef*.
- **Fish / schools**: prefab, species, behaviour (`Schooling`, `LooseAggregation` or `Territorial`), zone, groups, fish per group, and `nearSpawn` (which keeps a group close to the start).
- **Atmosphere**: light shaft count, marine snow count, caustic frames and resolution, fog density.

Zones: `SpurTops`, `SpurSides`, `SandChannels`, `PatchReefs`, `ReefAnywhere` and `Anywhere`.

---

## Your species (already set up)

The 15 models you downloaded have been optimized (smaller textures, fewer polygons, real-world
size, facing +Z) and placed in `Assets/ReefSpecies/<name>/<name>.glb`. To turn them into
reef-ready prefabs and put them in the reef, run:

**Tools > Reef > Set Up My Species (and rebuild reef)**, then save the scene (Cmd+S) and press Play.

For each model this creates, in the same folder:
- `<name>.prefab`, the version the reef uses (selectable, and swimming or animated);
- `<name>_Info.asset`, the info-card text. It contains placeholder descriptions for your team to
  replace. Running the tool again never overwrites these.
- `<name>_Swim.mat`, for fish that wiggle using the swim shader.

It also fills in the Reef Builder settings: the shark and turtle slots, Other characters
(manta, whale), Schools (coral fish, parrotfish, ʻahi) and Layers (lava rock, limu, shells,
teeth, coconuts/driftwood). You can then tweak counts and sizes in `ReefBuildSettings.asset` and
use **Rebuild scatter / Rebuild life** as usual. The originals in `Assets/Models` are no longer
used, so you can move them out of the project to make Unity faster.

To add another species later, follow the steps below and put its prefab into a list in the settings.

## Bringing in your Sketchfab models

This is the part you'll repeat for every species.

### 1. Download and log it
- Prefer **FBX**. Otherwise take glTF and convert it through Blender to FBX.
- Record the model URL, author and **license** in your asset tracker. CC-BY models must be
  credited in the experience (for example, on a credits card or the title screen). Avoid
  ND (No-Derivatives) licenses, because you'll be modifying the models.

### 2. Prep in Maya (same checklist every time)
- **Real-world size.** Examples: tiger shark 3-4 m, green sea turtle shell about 1 m,
  humuhumu about 25 cm, cowrie 5-9 cm, tiger shark tooth 2.5-3.5 cm. Stereo makes wrong
  sizes obvious.
- **Face +Z (nose forward), Y up.** Then **Freeze Transformations** and **Delete History**.
  The swim shader bends the mesh along its own Z axis, so this step matters.
- **Pivot.** Fish and animals: at the body's centre. Coral, rocks and shells: at the base,
  where it touches the ground.
- **Poly budgets** (every triangle is drawn twice, once per eye):

  | Model | Triangle budget |
  |---|---|
  | Schooling fish | 1-3k |
  | Hero fish | 5-10k |
  | Turtle | 15-25k |
  | Shark | 20-30k |
  | Coral head | 3-10k |
  | Shell or tooth | 0.5-2k |

- **LODs.** Name the meshes `_LOD0`, `_LOD1` and `_LOD2`, and Unity builds the LOD Group for you.
- **Materials.** Combine to one material where possible.
- **Export** FBX (units: centimetres, Y-up, include animation only if the model is rigged).

### 3. Import into Unity
- Model tab: **Convert Units** on and scale factor 1. Check the size against the 1 m
  CC_FRONT height or a placeholder.
- Materials tab: **Extract Materials**, then make sure they are URP Lit. Use
  *Edit > Rendering > Materials > Convert Selected Built-in Materials to URP* if they come in pink.
- Rigged or animated models: in the Animation tab, set the swim clip to **Loop Time**. Drop
  the model in the scene, and Unity creates an Animator. `FishSchool` and `PathSwimmer`
  already scale the animation speed to the swimming speed.

### 4. Make it a reef-ready prefab
1. Drag the model into the scene.
2. **Fish with no animation:** select it and use
   **Tools > Reef > Make Selected Fish Swim**. This copies its textures onto a
   `Reef/FishSwim` material fitted to the model's length. Then tune *Tail Swing*,
   *Tail Beat Speed* and *Waves* on the material.
3. **Make it selectable:** use **Tools > Reef > Make Selected Selectable** (the swim tool
   does this for you).
4. Create its **SpeciesData**: Project window > right-click > **Create > Reef > Species Data**.
   Fill in the Hawaiian, common and scientific names, category, status, description and fun
   fact, and optionally an image and a pronunciation recording. Assign it on
   `SelectableSpecies`.
5. Drag it into the Project window to make a **prefab**.

### 5. Put it in the reef
- In `ReefBuildSettings`, add the prefab to the right **Layer**, **Character** or **School**,
  and assign its SpeciesData there too.
- Click **3 Scatter** (for corals, shells, teeth and rocks) or **5 Sea life** (for animals) again.
- For a hand-placed hero moment, place the prefab yourself **outside** `REEF (generated)`.
  Use **Tools > Reef > Give Selected A Swim Path**, then drag its waypoints to shape the route.

The ʻokina and kahakō display correctly with TextMesh Pro's default font. Its dynamic
fallback font has the glyphs.

---

## How the runtime pieces fit

| Script | On | Job |
|---|---|---|
| `ReefCCRig` | CC_HUB | Finds CC_HEAD, CC_FRONT and the cameras. Gives the true view ray (head to wall centre). |
| `SwimController` | CC_HUB | Swimming. Water-like inertia, capped turning, no roll, limited and auto-levelling pitch, rotation around the head, collisions, soft bounds, return to start. |
| `ReefBounds` | Bounds and Spawn | Swimmable box, water surface limit, spawn point and heading. |
| `SpeciesSelector` | CC_HUB | Picks the species nearest the centre inside a cone, checks line of sight, highlights it, and handles A, B, X and D-pad. Raises events. |
| `SelectableSpecies` | each species | Aim point and size, glowing rim highlight (also works on rigged and wiggling fish), and collect / hide. |
| `SpeciesData` | asset | Names and facts for the info card. |
| `SelectionReticle` | runtime | Ring and name tag at the target's depth. At rest, it sits at the wall's own depth. |
| `SpeciesInfoPanel` | runtime, child of CC_HUB | World-space info card that floats beside the selection ring, at about the species' depth, and follows it. It stays the same apparent size, stays inside the wall's edges, moves in front of rock that would hide it, and a thin line links it to the ring. Set **Placement** to *Fixed On Wall* for the old fixed spot. |
| `SpeciesBasket` | CC_HUB | Optional collecting game (score, checklist, events). |
| `FishSchool` | fish groups | Flocking or territorial fish that avoid rock, the floor, the surface and the viewer. |
| `SwimPath` + `PathSwimmer` | shark and turtles | Smooth designed routes. The animal keeps a respectful distance from the viewer. Without a path, it rests. |
| `UnderwaterEnvironment` | Atmosphere | Fog, water colour by depth, ambient, sky, sun strength, camera settings. |
| `CausticsAnimator` | Sun | Plays the caustic frames on the light cookie. |
| `MarineSnow` | Atmosphere | Endless field of drifting specks around the viewer. |

Shaders (`Reef/...`):
- `FishSwim`: URP Simple Lit plus a procedural swim wiggle.
- `HighlightRim`: the selection glow.
- `SoftParticle`: marine snow.
- `LightShaft`: the light shafts.
- `UnderwaterSky`: the sky.
- `WaterSurface`: the surface seen from below.
- `UnlitVertexColor`: the reticle.

### Stereo-wall rules this follows
- **Nothing in screen space.** CC_CANOE composites both eyes on a Screen Space canvas, so
  all UI here is world-space. Never add a Screen Space Overlay canvas for content.
- **Comfortable UI depth.** The idle reticle sits at the wall's depth (about 4.3 m, zero
  parallax). The info card sits beside its species but never closer than 2.5 m, and it's
  kept inside the wall's edges so it's never cut off by the frame.
- **Real scale everywhere.** 1 unit = 1 m.
- **Keep CC_HUB's rotation at 0,0,0 in the editor.** CC_CAMERA_SETUP offsets the eyes along
  world X at Start. Set the starting direction on the **Spawn Point** instead; the swim
  controller applies it after Start.
- **Comfort for the audience.** The swim controller never rolls, caps turn speed, and
  auto-levels pitch. Marine snow fades out very close to the eyes.

---

## Tuning and performance

The wall renders every frame twice at full wall resolution. If the frame rate drops, try
these first:

1. **MSAA on the eye render textures.** `CC_ALL/CC/CC_LEFT_RT` and `CC_RIGHT_RT` are set to
   8×. Try 4× or 2×.
2. **SSAO.** Your `PC_Renderer` has Screen Space Ambient Occlusion on, and it's expensive
   times two eyes. Try disabling it or lowering its quality.
3. **Counts.** Lower the scatter layer counts, fish per school, and `marineSnowCount`.
4. **Shadows.** Lower the URP shadow distance (currently 50 m, and everything past about
   35 m is fogged anyway).
5. **GPU Resident Drawer.** Turning it on in the URP asset helps with many repeated coral
   meshes.

Feel:

| Where | Setting | Effect |
|---|---|---|
| `SwimController` | `swimSpeed` | Top speed (1.8 m/s default) |
| `SwimController` | `waterDrag` | How long you glide after letting go |
| `SwimController` | `maxPitch` | Look-up/down limit (30°) |
| `SwimController` | `autoLevel` | Pitch returns to level when the stick is released |
| `SpeciesSelector` | `coneAngle` | Aiming generosity (12°) |
| `SpeciesSelector` | `maxDistance` | Selection range (10 m) |
| `UnderwaterEnvironment` | `fogDensity` | Visibility (0.05 means about 30-40 m) |
| `UnderwaterEnvironment` | water colours | Water colour at the surface and at depth |
| Reef volume | Bloom, Colour Adjustments, White Balance, Channel Mixer | Overall grade |

## The recipe game
The game is on CC_HUB as **ReefRecipeGame**. "Set Up My Species" adds it, or you can run
**Tools > Reef > Set Up Recipe Game** on its own.

1. **Start screen.** The six recipes are shown in a row with their pictures. Choose one with the
   D-pad or left stick and press A. Swimming is paused while the start screen is up.
2. **Collect.** A small recipe card sits in the top-right corner of the wall and pops out slightly.
   Aim at an ingredient and press A. Its info card opens and the ingredient is checked off.
   - Once an ingredient has enough, no more of it can be selected. For example, after 3 limu
     for the seaweed salad, the ring won't land on seaweed any more.
   - Things that aren't in the recipe can still be examined for their info. Tick
     **Only Ingredients Selectable** to turn that off.
3. **Maikaʻi!** When everything is found, a card with the recipe's picture pops out with a
   **Hana hou?** button. Press A to go back to the start screen. Collected shells, seaweed, wood
   and coconuts come back, and the viewer glides back to the start.

Press Menu (M on the keyboard) at any time to give up and return to the start screen.
To swim freely without the game (for example, for a guided tour), untick **ReefRecipeGame** on CC_HUB.

**Editing recipes.** The recipes are assets in `Assets/ReefRecipes/` (Leiomano, Poke, LeiPupu,
Luhee, Ia, LauAiLimu), and their pictures are in `Assets/ReefRecipes/Images/`. Each ingredient has
a label, a count and the species that count for it. For example, "Any fish" lists every fish, and
"Cone shell" accepts both shell models. To add a recipe, use Create > Reef > Recipe, then add it to
**ReefRecipeGame > Recipes**. Running the setup again never overwrites your edits.

The older basket game (**SpeciesSelector > Collecting Enabled**, X to collect) is still available,
but leave it off while the recipe game is in use.

## Troubleshooting
- **Pink materials.** A shader failed to compile. Check the Console, or open
  `Logs/ReefConsole.log` in the project folder: `Editor/ReefConsoleLog.cs` writes every
  Console error and each Reef shader's status there. It's editor-only and safe to delete.
- **No info card text.** Import *Window > TextMeshPro > Import TMP Essential Resources*.
- **Fish swim sideways or bend the wrong way.** The model doesn't face +Z in its own mesh
  space. Re-export it from Maya facing +Z with frozen transforms.
- **The viewer starts inside a rock.** Move the **Spawn Point** under
  `REEF (generated)/Bounds and Spawn`, or rebuild with a different `seed`.
- **"placed N of M" in the Console.** A scatter layer ran out of room in its zone. Lower
  its Min Spacing or count, or change the zone.

## Guided tours
`CCaux_Waypointer` is switched off because its keys (S, R, X, Space) and buttons (X, Y, B)
clash with swimming and selection. To record a guided flythrough, untick
`SwimController > Disable Waypointer`, turn the Waypointer on, and turn off
`Keyboard Fallback` while recording.
