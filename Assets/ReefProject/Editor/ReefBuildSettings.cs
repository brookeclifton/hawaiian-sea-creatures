using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reef.EditorTools
{
    /// <summary>Where on the reef something may be placed.</summary>
    public enum ReefZone
    {
        [Tooltip("Tops of the spurs (reef ridges).")] SpurTops,
        [Tooltip("Sloping sides of the spurs.")] SpurSides,
        [Tooltip("Sand channels (grooves) between spurs and the open sand.")] SandChannels,
        [Tooltip("Isolated coral heads out on the sand.")] PatchReefs,
        [Tooltip("Any reef (spurs or patch reefs).")] ReefAnywhere,
        [Tooltip("Anywhere on the seafloor.")] Anywhere
    }

    /// <summary>Which generated placeholder to use when a layer has no prefabs of its own.</summary>
    public enum PlaceholderKind
    {
        MoundCoral,
        BranchingCoral,
        PlateCoral,
        Boulder,
        Shell,
        SharkTooth,
        Urchin
    }

    [Serializable]
    public class ScatterLayer
    {
        public string name = "Layer";
        public bool enabled = true;
        [Tooltip("Your prefabs for this layer (e.g. Sketchfab coral models). Leave empty to use generated placeholders.")]
        public List<GameObject> prefabs = new List<GameObject>();
        public PlaceholderKind placeholder = PlaceholderKind.MoundCoral;
        public ReefZone zone = ReefZone.ReefAnywhere;
        [Min(0)] public int count = 100;
        [Tooltip("Random uniform scale range. Placeholders are 1 unit in size, so this is their size in metres. For real-scale prefabs use ~0.8-1.2.")]
        public Vector2 scale = new Vector2(0.8f, 1.2f);
        [Tooltip("Minimum distance between items of this layer, metres (multiplied by scale).")]
        public float minSpacing = 1f;
        [Tooltip("0 = evenly spread, 1 = strongly clumped.")]
        [UnityEngine.Range(0f, 1f)] public float clustering = 0.5f;
        [Tooltip("0 = always upright, 1 = lie along the slope.")]
        [UnityEngine.Range(0f, 1f)] public float alignToSlope = 0.5f;
        [Tooltip("How far to sink the item into the ground (fraction of its scale) so it doesn't float.")]
        public float sink = 0.05f;
        [Tooltip("Maximum ground slope in degrees.")]
        [UnityEngine.Range(0f, 90f)] public float maxSlope = 60f;
        [Tooltip("Add a MeshCollider so the viewer and fish bump into it and it blocks selection line-of-sight.")]
        public bool addCollider = true;
        public bool castShadows = true;

        [Header("Selection")]
        public bool makeSelectable = false;
        public SpeciesData species;
        [Tooltip("Fraction of the items that are selectable (e.g. 0.25 = every fourth coral head).")]
        [UnityEngine.Range(0f, 1f)] public float selectableFraction = 1f;
        [Tooltip("Shrink and disappear when collected (shells, teeth).")]
        public bool hideWhenCollected = false;
    }

    /// <summary>How a key character moves around the reef.</summary>
    public enum CharacterRoute
    {
        [Tooltip("Big slow loop around the whole reef (sharks, rays).")] WideReefLoop,
        [Tooltip("Circle one of the patch reefs (turtles, big fish).")] PatchReefLoop,
        [Tooltip("Rest on the sand beside a patch reef.")] RestOnSand,
        [Tooltip("Hover in a small loop close to the reef (eels at a ledge, an octopus, a big solitary fish).")] HoverNearReef
    }

    [Serializable]
    public class CharacterSpec
    {
        [Tooltip("Only used for the 'Other characters' list.")]
        public string name = "Animal";
        public bool enabled = true;
        [Tooltip("Only used for the 'Other characters' list (the shark and turtle slots have fixed routes).")]
        public CharacterRoute route = CharacterRoute.PatchReefLoop;
        [Tooltip("Your model prefab (faces +Z, real-world scale). Empty = generated placeholder.")]
        public GameObject prefab;
        public SpeciesData species;
        [Tooltip("Placeholder body length in metres (ignored when a prefab is assigned).")]
        public float placeholderLength = 1f;
        [Min(0)] public int count = 1;
        public float speed = 0.8f;
        [Tooltip("Route radius (x, z) in metres.")]
        public Vector2 pathRadius = new Vector2(20f, 16f);
        [Tooltip("Swimming height above the seafloor, metres (min, max).")]
        public Vector2 heightAboveFloor = new Vector2(2f, 3.5f);
    }

    [Serializable]
    public class SchoolSpec
    {
        public string name = "Reef fish";
        public bool enabled = true;
        [Tooltip("Your fish prefab (faces +Z, real-world scale). Empty = generated placeholder.")]
        public GameObject prefab;
        public SpeciesData species;
        public float placeholderLength = 0.2f;
        public Color placeholderColor = new Color(1f, 0.85f, 0.1f);
        public SchoolBehaviour behaviour = SchoolBehaviour.Schooling;
        public ReefZone zone = ReefZone.PatchReefs;
        [Tooltip("How many separate schools / territories to create.")]
        [Min(0)] public int groups = 3;
        [Tooltip("Fish per school / territory.")]
        [Min(1)] public int fishPerGroup = 20;
        public float homeRadius = 5f;
        public float cruiseSpeed = 0.6f;
        [Tooltip("Height of the school centre above the seafloor, metres.")]
        public float heightAboveFloor = 2f;
        [Tooltip("Keep these groups within this distance of the spawn point (0 = anywhere) so the audience meets them early.")]
        public float nearSpawn = 0f;
    }

    /// <summary>
    /// All the knobs for the Reef Builder. Lives at Assets/ReefProject/ReefBuildSettings.asset.
    /// Assign your Sketchfab prefabs to the layers / characters / schools here, then rebuild.
    /// </summary>
    public class ReefBuildSettings : ScriptableObject
    {
        [Header("General")]
        [Tooltip("Change to get a different (but repeatable) reef layout.")]
        public int seed = 1234;
        [Tooltip("World height of the ocean surface.")]
        public float surfaceHeight = 12f;

        [Header("Swimmable area")]
        [Tooltip("Size of the box the viewer can swim in (metres). The reef extends further so the edges disappear into the fog.")]
        public Vector3 boundsSize = new Vector3(80f, 26f, 80f);
        public Vector3 boundsCenter = new Vector3(0f, 0f, 4f);
        [Tooltip("Where the viewer's HEAD starts.")]
        public Vector3 spawnHeadPosition = new Vector3(0f, 5.5f, -32f);
        [Tooltip("Starting facing direction (degrees, 0 = +Z / north toward the reef).")]
        public float spawnYaw = 0f;

        [Header("Seafloor (spur-and-groove reef)")]
        public float terrainSize = 150f;
        [Tooltip("Heightmap resolution: 257, 513 or 1025.")]
        public int heightmapResolution = 513;
        [Tooltip("Sand height at the shoreward (south, -Z) edge.")]
        public float sandHeightSouth = 1.5f;
        [Tooltip("Sand height at the seaward (north, +Z) edge.")]
        public float sandHeightNorth = -3.5f;
        [Tooltip("Distance between spurs (reef ridges), metres.")]
        public float spurSpacing = 14f;
        [Tooltip("Spur width as a fraction of the spacing.")]
        [UnityEngine.Range(0.2f, 0.8f)] public float spurWidth = 0.62f;
        public float spurHeight = 3.2f;
        [Tooltip("Spurs run from this Z ...")]
        public float spurStartZ = -24f;
        [Tooltip("... to this Z.")]
        public float spurEndZ = 70f;
        [Tooltip("Isolated coral heads on the sand.")]
        public int patchReefCount = 8;
        public Vector2 patchReefRadius = new Vector2(2.5f, 5f);
        public Vector2 patchReefHeight = new Vector2(1.2f, 2.8f);
        [Tooltip("How far past the swim bounds to keep scattering reef (visible in the distance).")]
        public float scatterMargin = 16f;

        [Header("Reef scatter")]
        public List<ScatterLayer> layers = new List<ScatterLayer>();

        [Header("Key characters")]
        public CharacterSpec tigerShark = new CharacterSpec { placeholderLength = 3.4f, speed = 0.8f, pathRadius = new Vector2(30f, 24f), heightAboveFloor = new Vector2(2.5f, 4f) };
        public CharacterSpec swimmingTurtles = new CharacterSpec { placeholderLength = 1.0f, speed = 0.35f, pathRadius = new Vector2(7f, 6f), heightAboveFloor = new Vector2(1.5f, 3f) };
        [Tooltip("Turtles resting on the sand near coral heads.")]
        public CharacterSpec restingTurtles = new CharacterSpec { placeholderLength = 1.0f, speed = 0f, count = 1 };
        [Tooltip("Any other animals: add one entry per species, assign its prefab and SpeciesData, and pick a route.")]
        public List<CharacterSpec> otherCharacters = new List<CharacterSpec>();

        [Header("Fish")]
        public List<SchoolSpec> schools = new List<SchoolSpec>();

        [Header("Atmosphere")]
        public int lightShaftCount = 14;
        public int marineSnowCount = 2200;
        [Tooltip("Frames in the caustic loop.")]
        public int causticFrames = 32;
        public int causticResolution = 256;
        public float causticTileSize = 5f;
        public float fogDensity = 0.05f;

        [Header("CyberCANOE")]
        [Tooltip("Disable the spinning demo cube/sphere that come with the CC_CANOE scenes.")]
        public bool disableCCDemoObjects = true;
    }
}
