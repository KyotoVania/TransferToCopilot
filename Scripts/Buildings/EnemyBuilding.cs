using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;

public class EnemyBuilding : Building
{
    [Header("Visual Feedback")]
    [SerializeField] private GameObject healthBarPrefab;
    [SerializeField] private float healthBarOffset = 1.5f;
    [SerializeField] private bool showHealthBar = true;

    // Health bar components
    private GameObject healthBarInstance;
    private Slider healthBarSlider;
    private TextMeshProUGUI healthText;



    // Effects for damage and destruction
    [Header("Effects")]
    [SerializeField] private GameObject damageVFXPrefab;
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private AudioClip destructionSound;

    // Audio source for SFX
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

// Also add this to visualize the team change
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

        // Optional: Show damage number as floating text
        ShowDamageNumber(damage);
    }

    private void ShowDamageNumber(int damage)
    {
        // You would implement floating damage text here
        // This is just a placeholder - you might want to create a custom component for this
        Debug.Log($"[ENEMY BUILDING] {gameObject.name} took {damage} damage!");
    }

    // Override die method to add special effects
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

    public override void OnDestroy()
    {
        // Unsubscribe from events
        Building.OnBuildingDamaged -= OnAnyBuildingDamaged;

        // Call base implementation
        base.OnDestroy();
    }
}