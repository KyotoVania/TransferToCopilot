using UnityEngine;
using System;
using System.Collections.Generic;

public class ParticleController : MonoBehaviour
{
    public static ParticleController Instance;

    [Header("Debug Settings")]
    [SerializeField]
    private bool debugLogging = true; // Toggle for enabling/disabling debug logs

    [Serializable]
    public class ParticleEffectEntry
    {
        public string Tag;                // The tag to search for in the scene.
        public GameObject ParticlePrefab; // The prefab to instantiate on matching objects.
    }

    [Serializable]
    public class TeamParticleEffectEntry
    {
        public string Tag;                      // The tag to search for in the scene.
        public GameObject NeutralParticlePrefab;  // Particle for Neutral team.
        public GameObject PlayerParticlePrefab;   // Particle for Player team.
        public GameObject EnemyParticlePrefab;    // Particle for Enemy team.
        public float YPosition = 0.0f;            // Y position for the aura (distance from the ground)
    }

    [SerializeField]
    private List<ParticleEffectEntry> particleEffects = new List<ParticleEffectEntry>();

    [Header("Team-Based Effects for NeutralBuildings")]
    [SerializeField]
    private List<TeamParticleEffectEntry> teamParticleEffects = new List<TeamParticleEffectEntry>();

    // Tracks for each ParticleEffectEntry which GameObjects already have an instantiated effect.
    private Dictionary<ParticleEffectEntry, Dictionary<GameObject, GameObject>> effectInstances
        = new Dictionary<ParticleEffectEntry, Dictionary<GameObject, GameObject>>();

    // Tracks team-based effect instances
    private Dictionary<TeamParticleEffectEntry, Dictionary<GameObject, GameObject>> teamEffectInstances
        = new Dictionary<TeamParticleEffectEntry, Dictionary<GameObject, GameObject>>();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        // Initialize the tracking dictionary for each particle effect entry.
        foreach (var entry in particleEffects)
        {
            effectInstances[entry] = new Dictionary<GameObject, GameObject>();
        }

        // Initialize the tracking dictionary for each team-based particle effect entry.
        foreach (var entry in teamParticleEffects)
        {
            teamEffectInstances[entry] = new Dictionary<GameObject, GameObject>();
        }
    }

    private void Start()
    {
        // Subscribe to the RhythmManager beat event.
        RhythmManager.OnBeat += UpdateParticleEffects;

        // Initial update to place effects
        UpdateParticleEffects();
    }

    private void OnDestroy()
    {
        // Unsubscribe from the event to avoid memory leaks.
        RhythmManager.OnBeat -= UpdateParticleEffects;
    }

    // This method is called on each beat by the RhythmManager.
    public void UpdateParticleEffects()
    {
        if (debugLogging)
            Debug.Log("[ParticleController] Beginning UpdateParticleEffects");

        // Handle standard particle effects
        foreach (var entry in particleEffects)
        {
            // Find all GameObjects in the scene with the designated tag.
            GameObject[] targets = GameObject.FindGameObjectsWithTag(entry.Tag);
            // Use a HashSet to track the current targets for this entry.
            HashSet<GameObject> currentTargets = new HashSet<GameObject>(targets);

            if (debugLogging)
                Debug.Log($"[ParticleController] Found {targets.Length} objects with tag '{entry.Tag}'");

            // Add missing particle effects.
            foreach (GameObject target in targets)
            {
                if (!effectInstances[entry].ContainsKey(target))
                {
                    GameObject effectInstance = Instantiate(entry.ParticlePrefab, target.transform);
                    effectInstance.transform.localPosition = Vector3.zero; // Center the effect
                    effectInstances[entry][target] = effectInstance;

                    if (debugLogging)
                        Debug.Log($"[ParticleController] Created standard effect '{entry.ParticlePrefab.name}' at {target.name}, position: {target.transform.position}, local position: {Vector3.zero}");
                }
            }

            // Remove particle effects for objects that no longer exist or no longer match.
            List<GameObject> toRemove = new List<GameObject>();
            foreach (var kvp in effectInstances[entry])
            {
                GameObject trackedTarget = kvp.Key;
                if (!currentTargets.Contains(trackedTarget))
                {
                    if (kvp.Value != null)
                    {
                        Destroy(kvp.Value);
                        if (debugLogging)
                            Debug.Log($"[ParticleController] Removed standard effect from missing object with tag '{entry.Tag}'");
                    }
                    toRemove.Add(trackedTarget);
                }
            }
            // Clean up the tracking dictionary.
            foreach (GameObject target in toRemove)
            {
                effectInstances[entry].Remove(target);
            }
        }

        // Handle team-based particle effects for NeutralBuildings
        foreach (var entry in teamParticleEffects)
        {
            // Find all GameObjects in the scene with the designated tag.
            GameObject[] targets = GameObject.FindGameObjectsWithTag(entry.Tag);
            // Use a HashSet to track the current targets for this entry.
            HashSet<GameObject> currentTargets = new HashSet<GameObject>(targets);

            if (debugLogging)
                Debug.Log($"[ParticleController] Found {targets.Length} objects with tag '{entry.Tag}' for team effects");

            // Add/update particle effects based on team.
            foreach (GameObject target in targets)
            {
                // Get the NeutralBuilding component to check its team
                NeutralBuilding building = target.GetComponent<NeutralBuilding>();
                if (building == null)
                    continue;

                // Determine which particle prefab to use based on team
                GameObject prefabToUse = null;
                string teamName = "";
                switch (building.Team)
                {
                    case TeamType.Neutral:
                        prefabToUse = entry.NeutralParticlePrefab;
                        teamName = "Neutral";
                        break;
                    case TeamType.Player:
                        prefabToUse = entry.PlayerParticlePrefab;
                        teamName = "Player";
                        break;
                    case TeamType.Enemy:
                        prefabToUse = entry.EnemyParticlePrefab;
                        teamName = "Enemy";
                        break;
                }

                if (prefabToUse == null)
                    continue;

                // Calculate the position at the base of the building
                Vector3 auraPosition = CalculateAuraPosition(target, entry.YPosition);

                // If we already have an effect for this target
                if (teamEffectInstances[entry].ContainsKey(target))
                {
                    // Get the existing effect
                    GameObject existingEffect = teamEffectInstances[entry][target];

                    // Only check if we need to replace the effect due to team change
                    // Not every beat should cause replacement
                    if (existingEffect == null || existingEffect.name != prefabToUse.name + "(Clone)")
                    {
                        if (existingEffect != null)
                        {
                            if (debugLogging)
                                Debug.Log($"[ParticleController] Replacing team effect on {target.name} from {existingEffect.name} to {prefabToUse.name} (team: {teamName})");

                            Destroy(existingEffect);
                        }

                        // Create a new effect in the scene (not as a child)
                        GameObject newEffect = Instantiate(prefabToUse);

                        // Set absolute world position
                        newEffect.transform.position = auraPosition;

                        // Ensure proper scale is set
                        newEffect.transform.localScale = prefabToUse.transform.localScale;

                        // Add a ParticleFollower component to track the target
                        ParticleFollower follower = newEffect.AddComponent<ParticleFollower>();
                        follower.SetTarget(target, entry.YPosition);

                        teamEffectInstances[entry][target] = newEffect;

                        if (debugLogging)
                            Debug.Log($"[ParticleController] Created team effect '{prefabToUse.name}' for team {teamName} at world pos: {auraPosition}, target: {target.name}");
                    }
                    else
                    {
                        // Just update the position
                        existingEffect.transform.position = auraPosition;

                        // Update the follower component
                        ParticleFollower follower = existingEffect.GetComponent<ParticleFollower>();
                        if (follower != null)
                        {
                            follower.SetTarget(target, entry.YPosition);
                        }

                        if (debugLogging && Time.frameCount % 30 == 0) // Log position check periodically to avoid spam
                            Debug.Log($"[ParticleController] Maintained team effect at world pos: {auraPosition}, target: {target.name}");
                    }
                }
                else
                {
                    // Create a new effect in the scene (not as a child)
                    GameObject effectInstance = Instantiate(prefabToUse);

                    // Set absolute world position
                    effectInstance.transform.position = auraPosition;

                    // Ensure proper scale is set
                    effectInstance.transform.localScale = prefabToUse.transform.localScale;

                    // Add a ParticleFollower component to track the target
                    ParticleFollower follower = effectInstance.AddComponent<ParticleFollower>();
                    follower.SetTarget(target, entry.YPosition);

                    teamEffectInstances[entry][target] = effectInstance;

                    if (debugLogging)
                        Debug.Log($"[ParticleController] Created new team effect '{prefabToUse.name}' for team {teamName} at world pos: {auraPosition}, target: {target.name}");
                }
            }

            // Remove particle effects for objects that no longer exist or no longer match.
            List<GameObject> toRemove = new List<GameObject>();
            foreach (var kvp in teamEffectInstances[entry])
            {
                GameObject trackedTarget = kvp.Key;
                if (!currentTargets.Contains(trackedTarget))
                {
                    if (kvp.Value != null)
                    {
                        Destroy(kvp.Value);
                        if (debugLogging)
                            Debug.Log($"[ParticleController] Removed team effect from missing object with tag '{entry.Tag}'");
                    }
                    toRemove.Add(trackedTarget);
                }
            }
            // Clean up the tracking dictionary.
            foreach (GameObject target in toRemove)
            {
                teamEffectInstances[entry].Remove(target);
            }
        }
    }

    // This is a targeted modification for the UpdateParticlesForBuilding method
    // to fix position issues with neutral building particles
    public void UpdateParticlesForBuilding(GameObject building)
    {
        if (building == null)
            return;

        if (debugLogging)
            Debug.Log($"[ParticleController] UpdateParticlesForBuilding called for {building.name}");

        foreach (var entry in teamParticleEffects)
        {
            if (building.CompareTag(entry.Tag))
            {
                NeutralBuilding neutralBuilding = building.GetComponent<NeutralBuilding>();
                if (neutralBuilding == null)
                    continue;

                // Determine which particle prefab to use based on team
                GameObject prefabToUse = null;
                string teamName = "";
                switch (neutralBuilding.Team)
                {
                    case TeamType.Neutral:
                        prefabToUse = entry.NeutralParticlePrefab;
                        teamName = "Neutral";
                        break;
                    case TeamType.Player:
                        prefabToUse = entry.PlayerParticlePrefab;
                        teamName = "Player";
                        break;
                    case TeamType.Enemy:
                        prefabToUse = entry.EnemyParticlePrefab;
                        teamName = "Enemy";
                        break;
                }

                if (prefabToUse == null)
                {
                    if (debugLogging)
                        Debug.LogWarning($"[ParticleController] No prefab found for team {teamName} on {building.name}");
                    continue;
                }

                // Calculate the position at the base of the building
                Vector3 auraPosition = CalculateAuraPosition(building, entry.YPosition);

                // If we already have an effect for this target
                if (teamEffectInstances[entry].ContainsKey(building))
                {
                    // Check if we need to replace the effect due to team change
                    GameObject existingEffect = teamEffectInstances[entry][building];
                    if (existingEffect == null || existingEffect.name != prefabToUse.name + "(Clone)")
                    {
                        if (existingEffect != null)
                        {
                            if (debugLogging)
                                Debug.Log($"[ParticleController] Replacing specific building effect on {building.name} from {existingEffect.name} to {prefabToUse.name} (team: {teamName})");

                            Destroy(existingEffect);
                        }

                        // Create a new effect in the scene (not as a child)
                        GameObject newEffect = Instantiate(prefabToUse);

                        // Set absolute world position
                        newEffect.transform.position = auraPosition;

                        // Ensure proper scale is set
                        newEffect.transform.localScale = prefabToUse.transform.localScale;

                        // Add a ParticleFollower component to track the target
                        ParticleFollower follower = newEffect.AddComponent<ParticleFollower>();
                        follower.SetTarget(building, entry.YPosition);

                        teamEffectInstances[entry][building] = newEffect;

                        if (debugLogging)
                            Debug.Log($"[ParticleController] Created specific building effect '{prefabToUse.name}' for team {teamName} at world pos: {auraPosition}, target: {building.name}");
                    }
                    else
                    {
                        // Just update the position
                        existingEffect.transform.position = auraPosition;

                        // Update the follower component
                        ParticleFollower follower = existingEffect.GetComponent<ParticleFollower>();
                        if (follower != null)
                        {
                            follower.SetTarget(building, entry.YPosition);
                        }

                        if (debugLogging)
                            Debug.Log($"[ParticleController] Reset position for building effect at world pos: {auraPosition}, target: {building.name}");
                    }
                }
                else
                {
                    // Create a new effect in the scene (not as a child)
                    GameObject effectInstance = Instantiate(prefabToUse);

                    // Set absolute world position
                    effectInstance.transform.position = auraPosition;

                    // Ensure proper scale is set
                    effectInstance.transform.localScale = prefabToUse.transform.localScale;

                    // Add a ParticleFollower component to track the target
                    ParticleFollower follower = effectInstance.AddComponent<ParticleFollower>();
                    follower.SetTarget(building, entry.YPosition);

                    teamEffectInstances[entry][building] = effectInstance;

                    if (debugLogging)
                        Debug.Log($"[ParticleController] Created new building-specific effect '{prefabToUse.name}' for team {teamName} at world pos: {auraPosition}, target: {building.name}");
                }

                break; // We found and processed the matching entry
            }
        }
    }

    // Calculates the absolute world position for the aura at the base of the target
    private Vector3 CalculateAuraPosition(GameObject target, float yOffset)
    {
        Bounds buildingBounds = GetBuildingBounds(target);
        Vector3 basePosition = new Vector3(
            buildingBounds.center.x,      // X center
            buildingBounds.min.y + yOffset, // Y at the bottom + offset
            buildingBounds.center.z       // Z center
        );

        if (debugLogging)
            Debug.Log($"[ParticleController] Calculated aura position for {target.name}: {basePosition}, bounds: min={buildingBounds.min}, max={buildingBounds.max}, center={buildingBounds.center}");

        return basePosition;
    }

    // Modify this method in ParticleController class
    private Bounds GetBuildingBounds(GameObject target)
    {
        // Option 1: Only use renderers directly on the parent object
        Renderer[] renderers = target.GetComponents<Renderer>();

        // If the parent has no renderers, we need to find the main renderers while avoiding sub-prefabs
        if (renderers.Length == 0)
        {
            // Find only first-level children with renderers
            List<Renderer> firstLevelRenderers = new List<Renderer>();

            foreach (Transform child in target.transform)
            {
                Renderer childRenderer = child.GetComponent<Renderer>();
                if (childRenderer != null)
                {
                    firstLevelRenderers.Add(childRenderer);
                }
            }

            // If we found first-level renderers, use those
            if (firstLevelRenderers.Count > 0)
            {
                renderers = firstLevelRenderers.ToArray();
            }
            else
            {
                // Fallback to using all renderers if needed
                // But log a warning so you know this case is happening
                if (debugLogging)
                    Debug.LogWarning($"[ParticleController] No direct renderers found for {target.name}, falling back to all child renderers");

                renderers = target.GetComponentsInChildren<Renderer>();
            }
        }

        if (renderers.Length == 0)
        {
            // Fallback to just using the transform position
            if (debugLogging)
                Debug.LogWarning($"[ParticleController] No renderers found for {target.name}, using transform position");

            Vector3 position = target.transform.position;
            Bounds fallbackBounds = new Bounds(position, Vector3.one);
            return fallbackBounds;
        }

        // Start with the first renderer's bounds
        Bounds resultBounds = renderers[0].bounds;

        // Expand to include all other renderers
        for (int i = 1; i < renderers.Length; i++)
        {
            resultBounds.Encapsulate(renderers[i].bounds);
        }

        return resultBounds;
    }
}

// Helper component to make particles follow a target object
public class ParticleFollower : MonoBehaviour
{
    private GameObject target;
    private float yOffset;

    public void SetTarget(GameObject newTarget, float newYOffset)
    {
        target = newTarget;
        yOffset = newYOffset;
    }

    private void LateUpdate()
    {
        if (target != null)
        {
            // Get the target's renderer bounds
            Bounds targetBounds = GetBuildingBounds(target);

            // Update position to follow the target
            transform.position = new Vector3(
                targetBounds.center.x,
                targetBounds.min.y + yOffset,
                targetBounds.center.z
            );
        }
    }

    private Bounds GetBuildingBounds(GameObject target)
    {
        // Option 1: Only use renderers directly on the parent object
        Renderer[] renderers = target.GetComponents<Renderer>();

        // If the parent has no renderers, we need to find the main renderers while avoiding sub-prefabs
        if (renderers.Length == 0)
        {
            // Find only first-level children with renderers
            List<Renderer> firstLevelRenderers = new List<Renderer>();

            foreach (Transform child in target.transform)
            {
                Renderer childRenderer = child.GetComponent<Renderer>();
                if (childRenderer != null)
                {
                    firstLevelRenderers.Add(childRenderer);
                }
            }

            // If we found first-level renderers, use those
            if (firstLevelRenderers.Count > 0)
            {
                renderers = firstLevelRenderers.ToArray();
            }
            else
            {
                renderers = target.GetComponentsInChildren<Renderer>();
            }
        }

        if (renderers.Length == 0)
        {
            Vector3 position = target.transform.position;
            Bounds fallbackBounds = new Bounds(position, Vector3.one);
            return fallbackBounds;
        }

        // Start with the first renderer's bounds
        Bounds resultBounds = renderers[0].bounds;

        // Expand to include all other renderers
        for (int i = 1; i < renderers.Length; i++)
        {
            resultBounds.Encapsulate(renderers[i].bounds);
        }

        return resultBounds;
    }
}