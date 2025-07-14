using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Represents a building controlled by enemy AI.
/// Features health visualization, damage effects, and team-specific visual feedback.
/// </summary>
public class EnemyBuilding : Building
{
    [Header("Visual Feedback")]
    /// <summary>
    /// Prefab for the health bar UI element.
    /// </summary>
    [SerializeField] private GameObject healthBarPrefab;
    /// <summary>
    /// Vertical offset for health bar positioning.
    /// </summary>
    [SerializeField] private float healthBarOffset = 1.5f;
    /// <summary>
    /// Whether to display the health bar.
    /// </summary>
    [SerializeField] private bool showHealthBar = true;

    /// <summary>
    /// Instance of the health bar GameObject.
    /// </summary>
    private GameObject healthBarInstance;
    /// <summary>
    /// Slider component for health bar.
    /// </summary>
    private Slider healthBarSlider;
    /// <summary>
    /// Text component for health display.
    /// </summary>
    private TextMeshProUGUI healthText;



    [Header("Effects")]
    /// <summary>
    /// Visual effect prefab for damage events.
    /// </summary>
    [SerializeField] private GameObject damageVFXPrefab;
    /// <summary>
    /// Audio clip for damage sound effects.
    /// </summary>
    [SerializeField] private AudioClip damageSound;
    /// <summary>
    /// Audio clip for destruction sound effects.
    /// </summary>
    [SerializeField] private AudioClip destructionSound;

    /// <summary>
    /// Audio source component for playing sound effects.
    /// </summary>
    private AudioSource audioSource;

    protected override IEnumerator Start()
    {
        // Set the team to Enemy right at the start
        SetTeam(TeamType.Enemy);

        // Rest of your existing start method
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

        Debug.Log($"[ENEMY BUILDING] {gameObject.name} initialized as {Team} team and ready for combat!");
    }

    /// <summary>
    /// Handles visual changes when the building's team changes.
    /// Applies red tinting for enemy team affiliation.
    /// </summary>
    /// <param name="newTeam">The new team this building belongs to.</param>
    protected override void OnTeamChanged(TeamType newTeam)
    {
        base.OnTeamChanged(newTeam);

        // Visual feedback for team change - you could update materials/colors here
        if (newTeam == TeamType.Enemy)
        {
            // For example, if you want to tint the building red for enemy team
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                // Apply a slight red tint to materials
                foreach (Material material in renderer.materials)
                {
                    Color originalColor = material.color;
                    Color tintedColor = Color.Lerp(originalColor, Color.red, 0.3f);
                    material.color = tintedColor;
                }
            }
        }
    }

    /// <summary>
    /// Creates and initializes the health bar UI element.
    /// </summary>
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

    /// <summary>
    /// Updates the health bar display with current health values.
    /// </summary>
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

    /// <summary>
    /// Handles damage events for this building specifically.
    /// </summary>
    /// <param name="building">The building that was damaged.</param>
    /// <param name="newHealth">The new health value.</param>
    /// <param name="damage">The amount of damage taken.</param>
    private void OnAnyBuildingDamaged(Building building, int newHealth, int damage)
    {
        // Only respond to events for this building
        if (building != this) return;

        // Update health bar
        UpdateHealthBar();

        // Play damage effects
        PlayDamageEffects(damage);
    }

    /// <summary>
    /// Plays visual and audio effects for damage events.
    /// </summary>
    /// <param name="damage">The amount of damage taken.</param>
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

        // Optional: Show damage number as floating text
        ShowDamageNumber(damage);
    }

    /// <summary>
    /// Shows floating damage numbers (placeholder implementation).
    /// </summary>
    /// <param name="damage">The amount of damage to display.</param>
    private void ShowDamageNumber(int damage)
    {
        // You would implement floating damage text here
        // This is just a placeholder - you might want to create a custom component for this
        Debug.Log($"[ENEMY BUILDING] {gameObject.name} took {damage} damage!");
    }

    /// <summary>
    /// Handles the destruction of the enemy building with special effects.
    /// </summary>
    protected override void Die()
    {
        Debug.Log($"[ENEMY BUILDING] {gameObject.name} has been destroyed!");

        // Play destruction sound
        if (destructionSound != null && audioSource != null)
        {
            AudioSource.PlayClipAtPoint(destructionSound, transform.position);
        }
        // Call base implementation to handle destruction logic
        base.Die();
    }

    /// <summary>
    /// Cleans up event subscriptions when the building is destroyed.
    /// </summary>
    public override void OnDestroy()
    {
        // Unsubscribe from events
        Building.OnBuildingDamaged -= OnAnyBuildingDamaged;

        // Call base implementation
        base.OnDestroy();
    }
}