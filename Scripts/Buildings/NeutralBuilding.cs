// Fichier: Scripts/Buildings/NeutralBuilding.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class NeutralBuilding : Building
{
    [Header("Capture Settings")]
    [Tooltip("Can this building be captured?")]
    [SerializeField] private bool canBeCaptured = true;
    [Tooltip("Total 'beat efforts' required to capture this building.")]
    [SerializeField] private int beatsToCapture = 12;

    private float currentCaptureProgressPoints = 0f;
    private TeamType teamActuellementEnCapture = TeamType.Neutral;
    private HashSet<Unit> unitesQuiCapturentActuellement = new HashSet<Unit>();

    [Header("Effects (Optional)")]
    [SerializeField] private GameObject captureInProgressVFXPrefab;
    [SerializeField] private AudioClip captureProgressSound;
    [SerializeField] private AudioClip captureCompleteSound;

    [Header("Team Colors")]
    [SerializeField] private Color neutralColor = Color.grey;
    [SerializeField] private Color playerColor = new Color(0.8f, 0.2f, 0.2f);
    [SerializeField] private Color enemyColor = new Color(0.2f, 0.4f, 0.8f);
    [SerializeField] private float colorLerpSpeed = 2.0f;

    [Header("Visual Settings")]
    [SerializeField] private string roofMaterialName = "Roof";
    [SerializeField] private Transform roofObject;
    private Renderer roofRenderer;
    private Color currentColorDisplay;
    private Color targetColorDisplay;

    private AudioSource audioSource;
    private ParticleSystem captureInProgressParticlesInstance;

    public bool IsRecapturable => canBeCaptured;
    public bool IsBeingCaptured => teamActuellementEnCapture != TeamType.Neutral && teamActuellementEnCapture != this.Team;
    public TeamType CapturingInProgressByTeam => teamActuellementEnCapture;
    public float CaptureProgressNormalized => (beatsToCapture > 0) ? (currentCaptureProgressPoints / beatsToCapture) : 0f;
    public int BeatsToCaptureTotal => beatsToCapture;

    protected override IEnumerator Start()
    {
        FindRoofRenderer();
        targetColorDisplay = GetColorForTeam(this.Team);
        currentColorDisplay = targetColorDisplay;
        UpdateRoofColor(currentColorDisplay);

        if (GetComponent<AudioSource>() == null) audioSource = gameObject.AddComponent<AudioSource>();
        else audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 1.0f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.maxDistance = 20f;

        if (captureInProgressVFXPrefab != null)
        {
            GameObject vfxInstance = Instantiate(captureInProgressVFXPrefab, transform.position, Quaternion.identity, transform);
            captureInProgressParticlesInstance = vfxInstance.GetComponent<ParticleSystem>();
            if (captureInProgressParticlesInstance != null)
            {
                captureInProgressParticlesInstance.Stop();
            }
        }

        // --- MODIFICATION : Utilisation de MusicManager.Instance ---
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat += HandleBeatLogic;
        }

        yield return StartCoroutine(base.Start());

        targetColorDisplay = GetColorForTeam(this.Team);
        currentColorDisplay = targetColorDisplay;
        UpdateRoofColor(currentColorDisplay);
    }

    private void Update()
    {
        if (currentColorDisplay != targetColorDisplay)
        {
            currentColorDisplay = Color.Lerp(currentColorDisplay, targetColorDisplay, Time.deltaTime * colorLerpSpeed);
            UpdateRoofColor(currentColorDisplay);
        }
    }

    public override void OnDestroy()
    {
        // --- MODIFICATION : Utilisation de MusicManager.Instance ---
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.OnBeat -= HandleBeatLogic;
        }
        base.OnDestroy();
    }
    
    // --- MODIFICATION : Signature de la méthode mise à jour ---
    private void HandleBeatLogic(float beatDuration)
    {
        if (unitesQuiCapturentActuellement.Count > 0 && teamActuellementEnCapture != this.Team && teamActuellementEnCapture != TeamType.Neutral)
        {
            currentCaptureProgressPoints += unitesQuiCapturentActuellement.Count;
            
            if (captureProgressSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(captureProgressSound);
            }
            
            foreach (Unit unit in unitesQuiCapturentActuellement.ToList())
            {
                unit?.OnCaptureBeat();
            }

            if (currentCaptureProgressPoints >= beatsToCapture)
            {
                CompleteCaptureProcess(teamActuellementEnCapture);
            }
        }
    }
    
    // ... (Le reste du script NeutralBuilding.cs reste inchangé) ...
    #region Unchanged Code
    private void FindRoofRenderer()
    {
        if (roofObject != null) roofRenderer = roofObject.GetComponent<Renderer>();
        if (roofRenderer != null) return;
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            if (r.gameObject.name.ToLower().Contains(roofMaterialName.ToLower())) { roofRenderer = r; return; }
            foreach (Material mat in r.materials)
                if (mat.name.ToLower().Contains(roofMaterialName.ToLower())) { roofRenderer = r; return; }
        }
        if (renderers.Length > 0) roofRenderer = renderers[0];
    }

    private Color GetColorForTeam(TeamType team)
    {
        switch (team)
        {
            case TeamType.Player: return playerColor;
            case TeamType.Enemy: return enemyColor;
            default: return neutralColor;
        }
    }

    private void UpdateRoofColor(Color color)
    {
        if (roofRenderer == null) return;
        roofRenderer.material.color = color;
        if (roofRenderer.material.HasProperty("_EmissionColor"))
        {
            roofRenderer.material.EnableKeyword("_EMISSION");
            roofRenderer.material.SetColor("_EmissionColor", color * 0.5f);
        }
    }

    public bool StartCapture(TeamType teamAttemptingCapture, Unit capturingUnit)
    {
        if (!canBeCaptured || teamAttemptingCapture == TeamType.Neutral) return false;
        if (this.Team == teamAttemptingCapture) return false;
        if (capturingUnit == null || !IsUnitInCaptureRange(capturingUnit)) return false;
        
        if (teamActuellementEnCapture != teamAttemptingCapture)
        {
            NotifyUnitsOfCaptureStop(unitesQuiCapturentActuellement.ToList());
            unitesQuiCapturentActuellement.Clear();
            currentCaptureProgressPoints = 0f;
            teamActuellementEnCapture = teamAttemptingCapture;
            targetColorDisplay = GetColorForTeam(teamAttemptingCapture);
            if (captureInProgressParticlesInstance != null)
            {
                var mainModule = captureInProgressParticlesInstance.main;
                mainModule.startColor = new ParticleSystem.MinMaxGradient(GetColorForTeam(teamAttemptingCapture));
                if (!captureInProgressParticlesInstance.isPlaying) captureInProgressParticlesInstance.Play();
            }
        }
        
        bool added = unitesQuiCapturentActuellement.Add(capturingUnit);
        return true;
    }

    public void StopCapturing(Unit unit)
    {
        if (unit == null) return;
        if (unitesQuiCapturentActuellement.Remove(unit))
        {
            if (unitesQuiCapturentActuellement.Count == 0 && teamActuellementEnCapture != TeamType.Neutral)
            {
                ResetCaptureAttemptVisuals();
            }
        }
    }

    private void NotifyUnitsOfCaptureStop(List<Unit> unitsToNotify)
    {
        foreach (Unit u in unitsToNotify)
        {
            u?.OnCaptureComplete();
        }
    }

    private void ResetCaptureAttemptVisuals()
    {
        teamActuellementEnCapture = TeamType.Neutral;
        if (captureInProgressParticlesInstance != null && captureInProgressParticlesInstance.isPlaying)
        {
            captureInProgressParticlesInstance.Stop();
        }
        targetColorDisplay = GetColorForTeam(this.Team);
    }
    
    private void CompleteCaptureProcess(TeamType conqueringTeam)
    {
        TeamType oldTeam = this.Team;
        SetTeam(conqueringTeam);

        if (captureCompleteSound != null && audioSource != null) audioSource.PlayOneShot(captureCompleteSound);
        if (captureInProgressParticlesInstance != null) captureInProgressParticlesInstance.Stop();

        NotifyUnitsOfCaptureStop(unitesQuiCapturentActuellement.ToList());
        unitesQuiCapturentActuellement.Clear();

        currentCaptureProgressPoints = 0f;
        teamActuellementEnCapture = TeamType.Neutral;
    }

    protected override void OnTeamChanged(TeamType newTeam)
    {
        base.OnTeamChanged(newTeam);
        targetColorDisplay = GetColorForTeam(newTeam);
        if (ParticleController.Instance != null)
        {
            ParticleController.Instance.UpdateParticlesForBuilding(gameObject);
        }
    }
    
    private bool IsUnitInCaptureRange(Unit unit)
    {
        if (unit == null || unit.GetOccupiedTile() == null || this.occupiedTile == null || HexGridManager.Instance == null)
            return false;
        return HexGridManager.Instance.HexDistance(unit.GetOccupiedTile().column, unit.GetOccupiedTile().row, this.occupiedTile.column, this.occupiedTile.row) <= 1;
    }
    
    public override void TakeDamage(int damage,  Unit attacker = null)
    {
        if (!IsTargetable) return;
        if (IsRecapturable && this.Team != TeamType.Neutral)
        {
            return;
        }
        else
        {
            base.TakeDamage(damage);
        }
    }
    #endregion
}