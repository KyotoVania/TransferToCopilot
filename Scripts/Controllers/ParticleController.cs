using UnityEngine;
using System;
using System.Collections.Generic;

public class ParticleController : MonoBehaviour
{
    public static ParticleController Instance;

    [Header("Debug Settings")]
    [SerializeField]
    private bool debugLogging = true;

    [Serializable]
    public class ParticleEffectEntry
    {
        public string Tag;
        public GameObject ParticlePrefab;
    }

    [Serializable]
    public class TeamParticleEffectEntry
    {
        public string Tag;
        public GameObject NeutralParticlePrefab;
        public GameObject PlayerParticlePrefab;
        public GameObject EnemyParticlePrefab;
        public float YPosition = 0.0f;
    }

    [SerializeField]
    private List<ParticleEffectEntry> particleEffects = new List<ParticleEffectEntry>();

    [Header("Team-Based Effects for NeutralBuildings")]
    [SerializeField]
    private List<TeamParticleEffectEntry> teamParticleEffects = new List<TeamParticleEffectEntry>();

    private Dictionary<ParticleEffectEntry, Dictionary<GameObject, GameObject>> effectInstances
        = new Dictionary<ParticleEffectEntry, Dictionary<GameObject, GameObject>>();

    private Dictionary<TeamParticleEffectEntry, Dictionary<GameObject, GameObject>> teamEffectInstances
        = new Dictionary<TeamParticleEffectEntry, Dictionary<GameObject, GameObject>>();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        foreach (var entry in particleEffects)
        {
            effectInstances[entry] = new Dictionary<GameObject, GameObject>();
        }

        foreach (var entry in teamParticleEffects)
        {
            teamEffectInstances[entry] = new Dictionary<GameObject, GameObject>();
        }
    }

    private void Start()
    {
        // --- MODIFICATION : Utilisation de MusicManager.Instance ---
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat += UpdateParticleEffects;
        }

        // Initial update to place effects
        UpdateParticleEffects(0); // On peut passer 0 car la durée n'est pas utilisée ici
    }

    private void OnDestroy()
    {
        // --- MODIFICATION : Utilisation de MusicManager.Instance ---
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= UpdateParticleEffects;
        }
    }

    // --- MODIFICATION : Signature de la méthode mise à jour ---
    public void UpdateParticleEffects(float beatDuration)
    {
        if (debugLogging)
            Debug.Log("[ParticleController] Beginning UpdateParticleEffects");

        // Handle standard particle effects
        foreach (var entry in particleEffects)
        {
            GameObject[] targets = GameObject.FindGameObjectsWithTag(entry.Tag);
            HashSet<GameObject> currentTargets = new HashSet<GameObject>(targets);

            if (debugLogging)
                Debug.Log($"[ParticleController] Found {targets.Length} objects with tag '{entry.Tag}'");

            foreach (GameObject target in targets)
            {
                if (!effectInstances[entry].ContainsKey(target))
                {
                    GameObject effectInstance = Instantiate(entry.ParticlePrefab, target.transform);
                    effectInstance.transform.localPosition = Vector3.zero;
                    effectInstances[entry][target] = effectInstance;

                    if (debugLogging)
                        Debug.Log($"[ParticleController] Created standard effect '{entry.ParticlePrefab.name}' at {target.name}, position: {target.transform.position}, local position: {Vector3.zero}");
                }
            }

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
            foreach (GameObject target in toRemove)
            {
                effectInstances[entry].Remove(target);
            }
        }

        // Handle team-based particle effects for NeutralBuildings
        foreach (var entry in teamParticleEffects)
        {
            GameObject[] targets = GameObject.FindGameObjectsWithTag(entry.Tag);
            HashSet<GameObject> currentTargets = new HashSet<GameObject>(targets);

            if (debugLogging)
                Debug.Log($"[ParticleController] Found {targets.Length} objects with tag '{entry.Tag}' for team effects");

            foreach (GameObject target in targets)
            {
                NeutralBuilding building = target.GetComponent<NeutralBuilding>();
                if (building == null)
                    continue;

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

                Vector3 auraPosition = CalculateAuraPosition(target, entry.YPosition);

                if (teamEffectInstances[entry].ContainsKey(target))
                {
                    GameObject existingEffect = teamEffectInstances[entry][target];

                    if (existingEffect == null || existingEffect.name != prefabToUse.name + "(Clone)")
                    {
                        if (existingEffect != null)
                        {
                            if (debugLogging)
                                Debug.Log($"[ParticleController] Replacing team effect on {target.name} from {existingEffect.name} to {prefabToUse.name} (team: {teamName})");

                            Destroy(existingEffect);
                        }
                        
                        GameObject newEffect = Instantiate(prefabToUse);
                        newEffect.transform.position = auraPosition;
                        newEffect.transform.localScale = prefabToUse.transform.localScale;
                        ParticleFollower follower = newEffect.AddComponent<ParticleFollower>();
                        follower.SetTarget(target, entry.YPosition);
                        teamEffectInstances[entry][target] = newEffect;

                        if (debugLogging)
                            Debug.Log($"[ParticleController] Created team effect '{prefabToUse.name}' for team {teamName} at world pos: {auraPosition}, target: {target.name}");
                    }
                    else
                    {
                        existingEffect.transform.position = auraPosition;
                        ParticleFollower follower = existingEffect.GetComponent<ParticleFollower>();
                        if (follower != null)
                        {
                            follower.SetTarget(target, entry.YPosition);
                        }
                    }
                }
                else
                {
                    GameObject effectInstance = Instantiate(prefabToUse);
                    effectInstance.transform.position = auraPosition;
                    effectInstance.transform.localScale = prefabToUse.transform.localScale;
                    ParticleFollower follower = effectInstance.AddComponent<ParticleFollower>();
                    follower.SetTarget(target, entry.YPosition);
                    teamEffectInstances[entry][target] = effectInstance;

                    if (debugLogging)
                        Debug.Log($"[ParticleController] Created new team effect '{prefabToUse.name}' for team {teamName} at world pos: {auraPosition}, target: {target.name}");
                }
            }

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
            foreach (GameObject target in toRemove)
            {
                teamEffectInstances[entry].Remove(target);
            }
        }
    }

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

                Vector3 auraPosition = CalculateAuraPosition(building, entry.YPosition);

                if (teamEffectInstances[entry].ContainsKey(building))
                {
                    GameObject existingEffect = teamEffectInstances[entry][building];
                    if (existingEffect == null || existingEffect.name != prefabToUse.name + "(Clone)")
                    {
                        if (existingEffect != null)
                        {
                            if (debugLogging)
                                Debug.Log($"[ParticleController] Replacing specific building effect on {building.name} from {existingEffect.name} to {prefabToUse.name} (team: {teamName})");
                            Destroy(existingEffect);
                        }

                        GameObject newEffect = Instantiate(prefabToUse);
                        newEffect.transform.position = auraPosition;
                        newEffect.transform.localScale = prefabToUse.transform.localScale;
                        ParticleFollower follower = newEffect.AddComponent<ParticleFollower>();
                        follower.SetTarget(building, entry.YPosition);
                        teamEffectInstances[entry][building] = newEffect;

                        if (debugLogging)
                            Debug.Log($"[ParticleController] Created specific building effect '{prefabToUse.name}' for team {teamName} at world pos: {auraPosition}, target: {building.name}");
                    }
                    else
                    {
                        existingEffect.transform.position = auraPosition;
                        ParticleFollower follower = existingEffect.GetComponent<ParticleFollower>();
                        if (follower != null)
                        {
                            follower.SetTarget(building, entry.YPosition);
                        }
                    }
                }
                else
                {
                    GameObject effectInstance = Instantiate(prefabToUse);
                    effectInstance.transform.position = auraPosition;
                    effectInstance.transform.localScale = prefabToUse.transform.localScale;
                    ParticleFollower follower = effectInstance.AddComponent<ParticleFollower>();
                    follower.SetTarget(building, entry.YPosition);
                    teamEffectInstances[entry][building] = effectInstance;

                    if (debugLogging)
                        Debug.Log($"[ParticleController] Created new building-specific effect '{prefabToUse.name}' for team {teamName} at world pos: {auraPosition}, target: {building.name}");
                }
                break;
            }
        }
    }

    private Vector3 CalculateAuraPosition(GameObject target, float yOffset)
    {
        Bounds buildingBounds = GetBuildingBounds(target);
        Vector3 basePosition = new Vector3(
            buildingBounds.center.x,
            buildingBounds.min.y + yOffset,
            buildingBounds.center.z
        );

        if (debugLogging)
            Debug.Log($"[ParticleController] Calculated aura position for {target.name}: {basePosition}, bounds: min={buildingBounds.min}, max={buildingBounds.max}, center={buildingBounds.center}");

        return basePosition;
    }

    private Bounds GetBuildingBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponents<Renderer>();

        if (renderers.Length == 0)
        {
            List<Renderer> firstLevelRenderers = new List<Renderer>();
            foreach (Transform child in target.transform)
            {
                Renderer childRenderer = child.GetComponent<Renderer>();
                if (childRenderer != null)
                {
                    firstLevelRenderers.Add(childRenderer);
                }
            }
            if (firstLevelRenderers.Count > 0)
            {
                renderers = firstLevelRenderers.ToArray();
            }
            else
            {
                if (debugLogging)
                    Debug.LogWarning($"[ParticleController] No direct renderers found for {target.name}, falling back to all child renderers");
                renderers = target.GetComponentsInChildren<Renderer>();
            }
        }

        if (renderers.Length == 0)
        {
            if (debugLogging)
                Debug.LogWarning($"[ParticleController] No renderers found for {target.name}, using transform position");
            Vector3 position = target.transform.position;
            Bounds fallbackBounds = new Bounds(position, Vector3.one);
            return fallbackBounds;
        }

        Bounds resultBounds = renderers[0].bounds;
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