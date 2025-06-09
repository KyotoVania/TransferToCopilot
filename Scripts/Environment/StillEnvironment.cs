using UnityEngine;
using System.Collections;

public class StillEnvironment : Environment
{
    [Header("Forced Attachment (Optionnel)")]
    [Tooltip("Si une tuile est assignée ici, l'environnement tentera de s'y attacher au démarrage, ignorant la recherche de la plus proche.")]
    public Tile forceAttachTile = null;

    [Header("Granular Lock Settings")]
    [Tooltip("Verrouille la position locale sur l'axe X.")]
    [SerializeField] private bool lockLocalPositionX = true;
    [Tooltip("Verrouille la position locale sur l'axe Y.")]
    [SerializeField] private bool lockLocalPositionY = true;
    [Tooltip("Verrouille la position locale sur l'axe Z.")]
    [SerializeField] private bool lockLocalPositionZ = true;

    [Space(10)]
    [Tooltip("Verrouille la rotation locale sur l'axe X (Euler).")]
    [SerializeField] private bool lockLocalRotationX = true;
    [Tooltip("Verrouille la rotation locale sur l'axe Y (Euler).")]
    [SerializeField] private bool lockLocalRotationY = true;
    [Tooltip("Verrouille la rotation locale sur l'axe Z (Euler).")]
    [SerializeField] private bool lockLocalRotationZ = true;

    [Space(10)]
    [Tooltip("Verrouille l'échelle locale sur l'axe X.")]
    [SerializeField] private bool lockLocalScaleX = true;
    [Tooltip("Verrouille l'échelle locale sur l'axe Y.")]
    [SerializeField] private bool lockLocalScaleY = true;
    [Tooltip("Verrouille l'échelle locale sur l'axe Z.")]
    [SerializeField] private bool lockLocalScaleZ = true;

    private Vector3 _initialLocalPosition;
    private Quaternion _initialLocalRotation;
    private Vector3 _initialLocalScale;

    private bool _stillEnvironmentInitialized = false;

    protected override IEnumerator Start()
    {
        // --- NOUVEAU: Capturer l'échelle locale désirée AVANT toute modification par SetParent ou Tile.cs ---
        // Ceci représente l'échelle que vous avez définie dans l'éditeur pour cet objet, avant qu'il ne devienne enfant d'une tuile.
        Vector3 desiredFinalLocalScale = transform.localScale;
        // --- FIN NOUVEAU ---

        bool attachedThisCycle = false; // Renommé pour clarté et portée

        if (forceAttachTile != null)
        {
            if (!forceAttachTile.IsOccupied || forceAttachTile.currentEnvironment == this)
            {
                if (occupiedTile != null && occupiedTile != forceAttachTile)
                {
                    occupiedTile.RemoveEnvironment();
                }
                AttachToTile(forceAttachTile); // Cette méthode (de Environment.cs) appelle tile.AssignEnvironment()
                if (isAttached)
                {
                    attachedThisCycle = true;
                    // Debug.Log($"[{gameObject.name}/StillEnvironment] Force-attached to tile: {forceAttachTile.name}", this);
                }
                else
                {
                    // Debug.LogWarning($"[{gameObject.name}/StillEnvironment] Failed to force-attach to tile '{forceAttachTile.name}'. Falling back.", this);
                }
            }
            else
            {
                // Debug.LogWarning($"[{gameObject.name}/StillEnvironment] Cannot force-attach to tile '{forceAttachTile.name}' (occupied). Falling back.", this);
            }
        }

        if (!attachedThisCycle)
        {
            // Debug.Log($"[{gameObject.name}/StillEnvironment] No force attach or failed. Calling base.Start() for closest tile attachment.", this);
            yield return StartCoroutine(base.Start()); // base.Start() appelle AttachToTile -> tile.AssignEnvironment()
            // isAttached sera mis à jour par base.Start()
        }

        if (isAttached) // Vérifier le 'isAttached' qui est mis à jour par la classe de base ou l'attachement forcé.
        {
            // --- NOUVEAU: Réappliquer l'échelle locale désirée APRÈS le parentage et la compensation de Tile.cs ---
            // Cela annule la compensation d'échelle faite par Tile.AssignEnvironment,
            // permettant à StillEnvironment de conserver son échelle locale d'origine.
            transform.localScale = desiredFinalLocalScale;
            // --- FIN NOUVEAU ---

            // Maintenant, capturer les transformations locales finales pour le verrouillage.
            _initialLocalPosition = transform.localPosition; // Sera (0, yOffset, 0) par rapport à la tuile
            _initialLocalRotation = transform.localRotation; // Sera la rotation après ApplyRandomRotation (si activé)
            _initialLocalScale = transform.localScale;       // Sera desiredFinalLocalScale

            _stillEnvironmentInitialized = true;

            // Debug.Log($"[{gameObject.name}/StillEnvironment] Initialized. Tile: {occupiedTile.name}. " +
            //           $"DesiredLocalScale was: {desiredFinalLocalScale}, Effective InitialLocalScale for locking: {_initialLocalScale}. " +
            //           $"Initial Local Pos: {_initialLocalPosition}, Rot: {_initialLocalRotation.eulerAngles}", this);
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}/StillEnvironment] Failed to attach to any tile. Locking mechanisms will be disabled.", this);
        }
    }

    void LateUpdate()
    {
        if (!_stillEnvironmentInitialized || !isAttached)
        {
            return;
        }

        // Verrouillage de la Position Locale (inchangé)
        Vector3 currentLocalPos = transform.localPosition;
        bool positionNeedsUpdate = false;
        if (lockLocalPositionX && !Mathf.Approximately(currentLocalPos.x, _initialLocalPosition.x))
        {
            currentLocalPos.x = _initialLocalPosition.x;
            positionNeedsUpdate = true;
        }
        if (lockLocalPositionY && !Mathf.Approximately(currentLocalPos.y, _initialLocalPosition.y))
        {
            currentLocalPos.y = _initialLocalPosition.y;
            positionNeedsUpdate = true;
        }
        if (lockLocalPositionZ && !Mathf.Approximately(currentLocalPos.z, _initialLocalPosition.z))
        {
            currentLocalPos.z = _initialLocalPosition.z;
            positionNeedsUpdate = true;
        }
        if (positionNeedsUpdate)
        {
            transform.localPosition = currentLocalPos;
        }

        // Verrouillage de la Rotation Locale (inchangé)
        Vector3 currentLocalEuler = transform.localEulerAngles;
        Vector3 initialLocalEuler = _initialLocalRotation.eulerAngles;
        bool rotationNeedsUpdate = false;
        if (lockLocalRotationX && !Mathf.Approximately(currentLocalEuler.x, initialLocalEuler.x))
        {
            currentLocalEuler.x = initialLocalEuler.x;
            rotationNeedsUpdate = true;
        }
        if (lockLocalRotationY && !Mathf.Approximately(currentLocalEuler.y, initialLocalEuler.y))
        {
            currentLocalEuler.y = initialLocalEuler.y;
            rotationNeedsUpdate = true;
        }
        if (lockLocalRotationZ && !Mathf.Approximately(currentLocalEuler.z, initialLocalEuler.z))
        {
            currentLocalEuler.z = initialLocalEuler.z;
            rotationNeedsUpdate = true;
        }
        if (rotationNeedsUpdate)
        {
            transform.localRotation = Quaternion.Euler(currentLocalEuler);
        }

        // Verrouillage de l'Échelle Locale (inchangé - se base maintenant sur le _initialLocalScale corrigé)
        Vector3 currentLocalScale = transform.localScale;
        bool scaleNeedsUpdate = false;
        if (lockLocalScaleX && !Mathf.Approximately(currentLocalScale.x, _initialLocalScale.x))
        {
            currentLocalScale.x = _initialLocalScale.x;
            scaleNeedsUpdate = true;
        }
        if (lockLocalScaleY && !Mathf.Approximately(currentLocalScale.y, _initialLocalScale.y))
        {
            currentLocalScale.y = _initialLocalScale.y; // C'est ici que la magie opère maintenant
            scaleNeedsUpdate = true;
        }
        if (lockLocalScaleZ && !Mathf.Approximately(currentLocalScale.z, _initialLocalScale.z))
        {
            currentLocalScale.z = _initialLocalScale.z;
            scaleNeedsUpdate = true;
        }
        if (scaleNeedsUpdate)
        {
            transform.localScale = currentLocalScale;
        }
    }
    // La méthode SetBlocking est héritée de Environment.cs
}