using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using Sirenix.OdinInspector;

public class PlayerBuilding : Building
{
    [System.Serializable]
    public class UnitSequence
    {
        [ListDrawerSettings(ShowFoldout = true)]
        public List<KeyCode> sequence;
        public GameObject unitPrefab;
    }

    [SerializeField, ListDrawerSettings(ShowFoldout = true)]
    private List<UnitSequence> unitSequences;

    private int beatCounter = 0;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject healthBarPrefab;
    [SerializeField] private float healthBarOffset = 1.5f;
    [SerializeField] private bool showHealthBar = true;

    // Health bar components
    private GameObject healthBarInstance;
    private Slider healthBarSlider;
    private TextMeshProUGUI healthText;

    // Effects for damage and repair
    [Header("Effects")]
    [SerializeField] private GameObject damageVFXPrefab;
    [SerializeField] private GameObject repairVFXPrefab;
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private AudioClip repairSound;

    // Audio source for SFX
    private AudioSource audioSource;

    private void OnEnable()
    {
        RhythmManager.OnBeat += HandleBeat;
        //SequenceController.OnSequenceExecuted += OnSequenceExecuted;
    }

    private void OnDisable()
    {
        RhythmManager.OnBeat -= HandleBeat;
       // SequenceController.OnSequenceExecuted -= OnSequenceExecuted;
    }

    protected override IEnumerator Start()
    {
        // Set the team to Player right at the start
        SetTeam(TeamType.Player);

        // Initialize audio
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f; // 3D sound
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.maxDistance = 20f;

        // Setup health bar if needed
        if (showHealthBar && healthBarPrefab != null)
        {
            CreateHealthBar();
        }

        // Subscribe to the damage event
        Building.OnBuildingDamaged += OnAnyBuildingDamaged;

        // Call base implementation to handle tile attachment, etc.
        yield return StartCoroutine(base.Start());

        Debug.Log($"[PLAYER BUILDING] {gameObject.name} initialized as {Team} team!");
    }

    private void HandleBeat()
    {
        beatCounter++;
        if (beatCounter >= Stats.goldGenerationDelay)
        {
            if (Stats.goldGeneration > 0)
            {
                GoldController.Instance.AddGold(Stats.goldGeneration);
            }
            beatCounter = 0;
        }
    }
    
    /*
    private void OnSequenceExecuted(Sequence executedSequence, int perfectCount)
    {
        foreach (var unitSequence in unitSequences)
        {
            if (AreSequencesEqual(executedSequence.targetSequence, unitSequence.sequence))
            {
                ProduceUnit(unitSequence.unitPrefab);
                break;
            }
        }
    }
    */
    private bool AreSequencesEqual(List<KeyCode> seq1, List<KeyCode> seq2)
    {
        if (seq1.Count != seq2.Count) return false;
        for (int i = 0; i < seq1.Count; i++)
        {
            if (seq1[i] != seq2[i]) return false;
        }
        return true;
    }

    private void ProduceUnit(GameObject unitPrefab)
    {
        if (unitPrefab == null)
        {
            Debug.LogError("Unit prefab is null!");
            return;
        }

        List<Tile> adjacentTiles = HexGridManager.Instance.GetAdjacentTiles(occupiedTile);
        foreach (Tile tile in adjacentTiles)
        {
            if (!tile.IsOccupied)
            {
                Instantiate(unitPrefab, tile.transform.position, Quaternion.identity);
                return;
            }
        }
        Debug.LogWarning("No available adjacent tiles to spawn unit!");
    }

    private void CreateHealthBar()
    {
        // Instantiate the health bar
        healthBarInstance = Instantiate(healthBarPrefab, transform);
        healthBarInstance.transform.localPosition = Vector3.up * healthBarOffset;

        // Make the health bar face the camera
        healthBarInstance.transform.rotation = Quaternion.Euler(30, 0, 0);

        // Get the slider and text components
        healthBarSlider = healthBarInstance.GetComponentInChildren<Slider>();
        healthText = healthBarInstance.GetComponentInChildren<TextMeshProUGUI>();

        // Initialize the health bar values
        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        if (healthBarSlider != null)
        {
            healthBarSlider.maxValue = MaxHealth;
            healthBarSlider.value = CurrentHealth;
        }

        if (healthText != null)
        {
            healthText.text = $"{CurrentHealth}/{MaxHealth}";
        }
    }

    // Event handler for damage
    private void OnAnyBuildingDamaged(Building building, int newHealth, int damage)
    {
        // Only respond to events for this building
        if (building != this) return;

        // Update health bar
        UpdateHealthBar();

        // Play damage effects
        PlayDamageEffects(damage);
    }

    private void PlayDamageEffects(int damage)
    {
        // Play damage sound
        if (damageSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(damageSound);
        }

        // Spawn damage VFX
        if (damageVFXPrefab != null)
        {
            GameObject vfx = Instantiate(
                damageVFXPrefab,
                transform.position + Vector3.up,
                Quaternion.identity
            );

            // Auto-destroy the VFX after 2 seconds
            Destroy(vfx, 2.0f);
        }
    }

    // Method to repair the building
    public void Repair(int amount)
    {
        if (CurrentHealth >= MaxHealth)
            return;

        // Calculate new health
        int newHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
        int actualRepair = newHealth - CurrentHealth;

        if (actualRepair <= 0)
            return;

        // Update health using the protected method from the base class
        SetCurrentHealth(newHealth);

        if (debugBuildingCombat)
        {
            Debug.Log($"[PLAYER BUILDING] {gameObject.name} repaired for {actualRepair}. Health: {CurrentHealth}/{MaxHealth}");
        }

        // Play repair effects
        PlayRepairEffects(actualRepair);

        // Update health bar
        UpdateHealthBar();
    }

    private void PlayRepairEffects(int amount)
    {
        // Play repair sound
        if (repairSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(repairSound);
        }

        // Spawn repair VFX
        if (repairVFXPrefab != null)
        {
            GameObject vfx = Instantiate(
                repairVFXPrefab,
                transform.position + Vector3.up,
                Quaternion.identity
            );

            // Auto-destroy the VFX after 2 seconds
            Destroy(vfx, 2.0f);
        }
    }

    // Override team change visuals
    protected override void OnTeamChanged(TeamType newTeam)
    {
        base.OnTeamChanged(newTeam);

        // Visual feedback for team change
        if (newTeam == TeamType.Player)
        {
            // For example, if you want to tint the building blue for player team
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                // Apply a slight blue tint to materials
                foreach (Material material in renderer.materials)
                {
                    Color originalColor = material.color;
                    Color tintedColor = Color.Lerp(originalColor, Color.blue, 0.3f);
                    material.color = tintedColor;
                }
            }
        }
    }

    public override void OnDestroy()
    {
        // Unsubscribe from events
        Building.OnBuildingDamaged -= OnAnyBuildingDamaged;
        RhythmManager.OnBeat -= HandleBeat;
       // SequenceController.OnSequenceExecuted -= OnSequenceExecuted;

        // Call base implementation
        base.OnDestroy();
    }
}