// Fichier: Scripts/Buildings/NeutralBuilding.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // Ajout pour ToList()

public class NeutralBuilding : Building
{
    [Header("Capture Settings")]
    [Tooltip("Can this building be captured?")]
    [SerializeField] private bool canBeCaptured = true;
    [Tooltip("Total 'beat efforts' required to capture this building.")]
    [SerializeField] private int beatsToCapture = 12; // Ex: 1 unité prend 12 battements, 2 unités prennent 6 battements.

    // --- État de Capture Actuel ---
    private float currentCaptureProgressPoints = 0f; // Progresse de 0 à beatsToCapture
    private TeamType teamActuellementEnCapture = TeamType.Neutral; // Quelle équipe est EN TRAIN de capturer
    private HashSet<Unit> unitesQuiCapturentActuellement = new HashSet<Unit>(); // Unités participant activement

    [Header("Effects (Optional)")]
    [SerializeField] private GameObject captureInProgressVFXPrefab; // Effet visuel quand une capture est en cours
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
    private Color currentColorDisplay; // Couleur actuellement affichée (pour lerp)
    private Color targetColorDisplay;  // Couleur cible pour l'affichage

    private AudioSource audioSource;
    private ParticleSystem captureInProgressParticlesInstance;

    // --- Propriétés Publiques ---
    public bool IsRecapturable => canBeCaptured; // Si un bâtiment neutre peut être capturé, il est "recapturable" par définition
    public bool IsBeingCaptured => teamActuellementEnCapture != TeamType.Neutral && teamActuellementEnCapture != this.Team;
    public TeamType CapturingInProgressByTeam => teamActuellementEnCapture;
    public float CaptureProgressNormalized => (beatsToCapture > 0) ? (currentCaptureProgressPoints / beatsToCapture) : 0f;
    public int BeatsToCaptureTotal => beatsToCapture;


    protected override IEnumerator Start()
    {
        FindRoofRenderer();
        targetColorDisplay = GetColorForTeam(this.Team); // Initialiser avec la couleur de l'équipe actuelle
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
                captureInProgressParticlesInstance.Stop(); // Arrêter par défaut
            }
        }

        RhythmManager.OnBeat += HandleBeatLogic;

        // Appel à la base pour l'attachement à la tuile, etc.
        yield return StartCoroutine(base.Start()); // base.Start() s'occupe de SetTeam(TeamType.Neutral) si c'est la valeur par défaut.

        // S'assurer que la couleur initiale est correcte après que base.Start() ait potentiellement appelé SetTeam.
        targetColorDisplay = GetColorForTeam(this.Team);
        currentColorDisplay = targetColorDisplay;
        UpdateRoofColor(currentColorDisplay);

        // Debug.Log($"[NeutralBuilding] {gameObject.name} initialized. Team: {this.Team}, BeatsToCapture: {beatsToCapture}");
    }

    private void Update()
    {
        // Transition douce de la couleur du toit
        if (currentColorDisplay != targetColorDisplay)
        {
            currentColorDisplay = Color.Lerp(currentColorDisplay, targetColorDisplay, Time.deltaTime * colorLerpSpeed);
            UpdateRoofColor(currentColorDisplay);
        }
    }

    public override void OnDestroy()
    {
        if (RhythmManager.Instance != null) // Vérifier si l'instance existe toujours
        {
            RhythmManager.OnBeat -= HandleBeatLogic;
        }
        base.OnDestroy();
    }

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
        // Logique pour appliquer la couleur au bon matériau du toit
        // (Simplifié, vous devrez peut-être ajuster cela si vous avez plusieurs matériaux)
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
        if (this.Team == teamAttemptingCapture) // Déjà capturé par cette équipe
        {
            // Debug.Log($"[{gameObject.name}] Already captured by Team {teamAttemptingCapture}.");
            return false;
        }
        if (capturingUnit == null || !IsUnitInCaptureRange(capturingUnit))
        {
            // Debug.LogWarning($"[{gameObject.name}] Unit {capturingUnit?.name ?? "Unknown"} cannot start capture: not in range or null.");
            return false;
        }

        // Si une autre équipe est EN TRAIN de capturer, ou si c'est une nouvelle équipe qui commence
        if (teamActuellementEnCapture != teamAttemptingCapture)
        {
            // Debug.Log($"[{gameObject.name}] Team {teamAttemptingCapture} is starting/taking over capture from Team {teamActuellementEnCapture}. Resetting progress.");
            NotifyUnitsOfCaptureStop(unitesQuiCapturentActuellement.ToList()); // Notifier les anciennes unités
            unitesQuiCapturentActuellement.Clear();
            currentCaptureProgressPoints = 0f; // Réinitialiser la progression pour la nouvelle équipe
            teamActuellementEnCapture = teamAttemptingCapture;

            // Mettre à jour les visuels pour la nouvelle équipe qui capture
            targetColorDisplay = GetColorForTeam(teamAttemptingCapture); // Couleur de l'équipe qui capture
            if (captureInProgressParticlesInstance != null)
            {
                var mainModule = captureInProgressParticlesInstance.main;
                mainModule.startColor = new ParticleSystem.MinMaxGradient(GetColorForTeam(teamAttemptingCapture));
                if (!captureInProgressParticlesInstance.isPlaying) captureInProgressParticlesInstance.Play();
            }
        }

        // Ajouter l'unité à la liste des unités en train de capturer pour cette équipe
        bool added = unitesQuiCapturentActuellement.Add(capturingUnit);
        if (added)
        {
            // Debug.Log($"[{gameObject.name}] Unit {capturingUnit.name} (Team {teamAttemptingCapture}) added to capturing units. Total now: {unitesQuiCapturentActuellement.Count}");
        }
        return true; // La tentative de démarrage/participation est acceptée
    }

    public void StopCapturing(Unit unit)
    {
        if (unit == null) return;

        if (unitesQuiCapturentActuellement.Remove(unit))
        {
            // Debug.Log($"[{gameObject.name}] Unit {unit.name} stopped capturing. Remaining capturers: {unitesQuiCapturentActuellement.Count}");
            if (unitesQuiCapturentActuellement.Count == 0 && teamActuellementEnCapture != TeamType.Neutral)
            {
                // Debug.Log($"[{gameObject.name}] No units left capturing for Team {teamActuellementEnCapture}. Capture attempt reset.");
                ResetCaptureAttemptVisuals(); // Ne réinitialise que l'état de tentative de capture, pas la propriété du bâtiment
            }
        }
    }

    private void NotifyUnitsOfCaptureStop(List<Unit> unitsToNotify)
    {
        foreach (Unit u in unitsToNotify)
        {
            u?.OnCaptureComplete(); // Ou une méthode OnCaptureInterrupted si vous voulez distinguer
        }
    }

    private void ResetCaptureAttemptVisuals()
    {
        teamActuellementEnCapture = TeamType.Neutral; // Plus personne ne capture activement
        // currentCaptureProgressPoints N'EST PAS réinitialisé ici, car une autre équipe pourrait reprendre.
        // La progression est réinitialisée uniquement si une équipe *différente* commence la capture.
        if (captureInProgressParticlesInstance != null && captureInProgressParticlesInstance.isPlaying)
        {
            captureInProgressParticlesInstance.Stop();
        }
        // La couleur du toit revient progressivement à la couleur de l'équipe qui possède actuellement le bâtiment
        targetColorDisplay = GetColorForTeam(this.Team);
    }

    private void HandleBeatLogic()
    {
        // Si des unités capturent et que le bâtiment n'appartient pas déjà à l'équipe qui capture
        if (unitesQuiCapturentActuellement.Count > 0 && teamActuellementEnCapture != this.Team && teamActuellementEnCapture != TeamType.Neutral)
        {
            currentCaptureProgressPoints += unitesQuiCapturentActuellement.Count; // Chaque unité contribue 1 point
            // Debug.Log($"[{gameObject.name}] Capture progress on beat by Team {teamActuellementEnCapture}: +{unitesQuiCapturentActuellement.Count} points. Total: {currentCaptureProgressPoints}/{beatsToCapture}");

            if (captureProgressSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(captureProgressSound);
            }

            // Notifier les unités qui capturent qu'un battement de capture a eu lieu
            // Copier la liste pour éviter les modifications pendant l'itération si OnCaptureBeat modifie la collection
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

    private void CompleteCaptureProcess(TeamType conqueringTeam)
    {
        // Debug.Log($"[{gameObject.name}] Capture COMPLETED by Team {conqueringTeam}! Current Owner: {this.Team}");

        TeamType oldTeam = this.Team;
        SetTeam(conqueringTeam); // Change la propriété du bâtiment. Cela appellera OnTeamChanged.

        if (captureCompleteSound != null && audioSource != null) audioSource.PlayOneShot(captureCompleteSound);
        if (captureInProgressParticlesInstance != null) captureInProgressParticlesInstance.Stop();

        // Notifier toutes les unités qui participaient que la capture est terminée
        NotifyUnitsOfCaptureStop(unitesQuiCapturentActuellement.ToList());
        unitesQuiCapturentActuellement.Clear();

        currentCaptureProgressPoints = 0f; // Réinitialiser pour la prochaine capture
        teamActuellementEnCapture = TeamType.Neutral; // Plus personne ne capture activement

        // Logique de jeu supplémentaire si nécessaire (par exemple, un événement global de capture)
        // BuildingManager.Instance.NotifyBuildingCaptured(this, oldTeam, newTeam);
    }


    // Cette méthode est appelée par la classe de base `Building` lorsque `SetTeam` est appelé.
    protected override void OnTeamChanged(TeamType newTeam)
    {
        base.OnTeamChanged(newTeam); // Appelle la logique de base (peut-être des logs)

        // Mettre à jour la couleur cible pour l'affichage
        targetColorDisplay = GetColorForTeam(newTeam);

        // Notifier le ParticleController si vous en avez un pour mettre à jour les effets visuels basés sur l'équipe
        if (ParticleController.Instance != null)
        {
            ParticleController.Instance.UpdateParticlesForBuilding(gameObject);
        }
        // Debug.Log($"[{gameObject.name}] Team changed to {newTeam}. Target color updated.");
    }

    // Vérifie si une unité est à portée de capture (généralement 1 tuile)
    private bool IsUnitInCaptureRange(Unit unit)
    {
        if (unit == null || unit.GetOccupiedTile() == null || this.occupiedTile == null || HexGridManager.Instance == null)
            return false;

        return HexGridManager.Instance.HexDistance(unit.GetOccupiedTile().column, unit.GetOccupiedTile().row, this.occupiedTile.column, this.occupiedTile.row) <= 1;
    }

    // Logique de dégâts pour les bâtiments neutres (peut être recapturé au lieu d'être détruit)
    public override void TakeDamage(int damage)
    {
        if (!IsTargetable) return;

        if (IsRecapturable && this.Team != TeamType.Neutral) // Si possédé et recapturable, il ne prend pas de "dégâts" mais peut être recapturé
        {
            // Les unités ennemies initieront une capture via StartCapture au lieu d'infliger des dégâts directs.
            // Si vous voulez qu'il prenne quand même des dégâts jusqu'à un certain point avant d'être recapturable :
            // base.TakeDamage(damage); // Si la vie tombe à 0, il pourrait devenir neutre.
            // Pour l'instant, si c'est recapturable, on suppose que l'attaque se traduit par une tentative de capture.
            // Debug.Log($"[{gameObject.name}] Is Recapturable and owned. Damage attempt might lead to recapture instead of health loss.");
            return; // Ne prend pas de dégâts de la manière traditionnelle s'il est destiné à être recapturé.
        }
        else // S'il est neutre et non recapturable OU s'il n'est simplement pas recapturable (mais peut être détruit)
        {
            base.TakeDamage(damage); // Comportement de dégâts normal
        }
    }
}