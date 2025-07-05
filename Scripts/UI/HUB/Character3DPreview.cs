namespace UI.HUB
{
    using UnityEngine;
    using UnityEngine.EventSystems;
    #if UNITY_EDITOR
    using UnityEditor.Animations;
    #endif

    // On ajoute les interfaces IPointerDownHandler, etc.
    public class Character3DPreview : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [Header("Références")]
        [SerializeField] private Transform characterSpawnPoint;
        [SerializeField] private string previewLayerName = "CharacterPreview";

        [Header("Animation")]
        [Tooltip("Vitesse de rotation automatique du modèle.")]
        [SerializeField] private float autoRotationSpeed = 20f;
        [Tooltip("Vitesse de rotation avec la souris.")]
        [SerializeField] private float mouseRotationSpeed = 10f; // Nouvelle variable

        private GameObject _currentCharacterInstance;
        private int _previewLayer;
        private bool _isDragging = false; // Pour savoir si on est en train de glisser

        void Awake()
        {
            _previewLayer = LayerMask.NameToLayer(previewLayerName);
            if (_previewLayer == -1)
            {
                Debug.LogError($"[Character3DPreview] Le layer '{previewLayerName}' n'existe pas.");
                enabled = false;
            }
        }

        void Update()
        {
            // Fait tourner le personnage sur lui-même SEULEMENT si on ne glisse pas la souris
            if (_currentCharacterInstance != null && !_isDragging)
            {
                _currentCharacterInstance.transform.Rotate(Vector3.up, autoRotationSpeed * Time.deltaTime);
            }
        }


        public void OnPointerDown(PointerEventData eventData)
        {
            // Le clic commence
            _isDragging = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // Le clic est relâché
            _isDragging = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Pendant le glissement
            if (_isDragging && _currentCharacterInstance != null)
            {
                // On fait tourner le modèle en fonction du mouvement horizontal de la souris (eventData.delta.x)
                _currentCharacterInstance.transform.Rotate(Vector3.up, -eventData.delta.x * mouseRotationSpeed * Time.deltaTime, Space.World);
            }
        }

        public void ShowCharacter(GameObject characterPrefab)
        {
            ClearPreview();
            if (characterPrefab == null) return;
            
            // Instanciation du personnage
            _currentCharacterInstance = Instantiate(characterPrefab, characterSpawnPoint.position, characterSpawnPoint.rotation, characterSpawnPoint);
           
            SetLayerRecursively(_currentCharacterInstance, _previewLayer);
            
            // 🎯 NOUVELLE LOGIQUE : Démarrer l'animation "Idle" automatiquement
            StartIdleAnimation();
        }
        
        /// <summary>
        /// Démarre l'animation "Idle" sur le personnage instancié pour éviter la T-pose
        /// </summary>
        private void StartIdleAnimation()
        {
            if (_currentCharacterInstance == null) return;
            
            // Rechercher le composant Animator sur l'instance ou ses enfants
            Animator animator = _currentCharacterInstance.GetComponentInChildren<Animator>();
            
            if (animator != null)
            {
                // Vérifier que l'Animator Controller est assigné
                if (animator.runtimeAnimatorController != null)
                {
                    // Cas normal : Controller assigné, essayer de jouer "Idle"
                    if (HasAnimationState(animator, "Idle"))
                    {
                        animator.Play("Idle", 0, 0f);
                        Debug.Log($"[Character3DPreview] Animation 'Idle' démarrée pour {_currentCharacterInstance.name}");
                    }
                    else
                    {
                        Debug.LogWarning($"[Character3DPreview] L'état 'Idle' n'existe pas dans le controller de {_currentCharacterInstance.name}");
                        // Essayer des noms alternatifs communs
                        TryAlternativeIdleStates(animator);
                    }
                }
                else
                {
                    Debug.LogWarning($"[Character3DPreview] Aucun Animator Controller assigné pour {_currentCharacterInstance.name}");
                    
                    // 🎯 NOUVELLE LOGIQUE : Créer un controller temporaire pour l'animation
                    #if UNITY_EDITOR
                    CreateTemporaryIdleController(animator);
                    #else
                    // En build, essayer de forcer l'animator à jouer une animation de base
                    ForceBasicIdleAnimation(animator);
                    #endif
                }
            }
            else
            {
                Debug.LogWarning($"[Character3DPreview] Aucun composant Animator trouvé sur {_currentCharacterInstance.name} ou ses enfants");
            }
        }
        
        /// <summary>
        /// Vérifie si un état d'animation existe dans le controller
        /// </summary>
        private bool HasAnimationState(Animator animator, string stateName)
        {
            if (animator.runtimeAnimatorController == null) return false;
            
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name.Equals(stateName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// Essaye des noms alternatifs pour l'état Idle
        /// </summary>
        private void TryAlternativeIdleStates(Animator animator)
        {
            string[] alternativeNames = { "idle", "Idle_01", "Standing", "Default", "Rest", "Wait" };
            
            foreach (string altName in alternativeNames)
            {
                if (HasAnimationState(animator, altName))
                {
                    animator.Play(altName, 0, 0f);
                    Debug.Log($"[Character3DPreview] Animation alternative '{altName}' démarrée pour {_currentCharacterInstance.name}");
                    return;
                }
            }
            
            Debug.LogWarning($"[Character3DPreview] Aucune animation Idle trouvée pour {_currentCharacterInstance.name}");
        }
        
        #if UNITY_EDITOR
        /// <summary>
        /// Crée un Animator Controller temporaire avec un état Idle (Editor uniquement)
        /// </summary>
        private void CreateTemporaryIdleController(Animator animator)
        {
            // Chercher une animation Idle dans les ressources du modèle
            AnimationClip idleClip = FindIdleAnimationClip();
            
            if (idleClip != null)
            {
                // 🎯 CORRECTION : Créer un controller temporaire en mémoire sans chemin
                AnimatorController tempController = new AnimatorController();
                tempController.name = "TempIdleController";
                
                // Créer le layer par défaut s'il n'existe pas
                if (tempController.layers.Length == 0)
                {
                    tempController.AddLayer("Base Layer");
                }
                
                // Obtenir la state machine du premier layer
                var rootStateMachine = tempController.layers[0].stateMachine;
                
                // Créer un état Idle
                var idleState = rootStateMachine.AddState("Idle");
                idleState.motion = idleClip;
                
                // Définir comme état par défaut
                rootStateMachine.defaultState = idleState;
                
                // Assigner le controller temporaire
                animator.runtimeAnimatorController = tempController;
                animator.Play("Idle", 0, 0f);
                
                Debug.Log($"[Character3DPreview] Controller temporaire créé avec animation Idle pour {_currentCharacterInstance.name}");
            }
            else
            {
                Debug.LogWarning($"[Character3DPreview] Aucune animation Idle trouvée dans les ressources de {_currentCharacterInstance.name}");
                
                // 🎯 FALLBACK : Créer un controller basique sans animation spécifique
                CreateBasicIdleController(animator);
            }
        }
        
        /// <summary>
        /// Crée un controller basique avec état Idle vide pour éviter la T-pose
        /// </summary>
        private void CreateBasicIdleController(Animator animator)
        {
            try
            {
                // Créer un controller minimal
                AnimatorController basicController = new AnimatorController();
                basicController.name = "BasicIdleController";
                
                // Créer le layer par défaut s'il n'existe pas
                if (basicController.layers.Length == 0)
                {
                    basicController.AddLayer("Base Layer");
                }
                
                // Obtenir la state machine du premier layer
                var rootStateMachine = basicController.layers[0].stateMachine;
                
                // Créer un état Idle vide (sans animation)
                var idleState = rootStateMachine.AddState("Idle");
                
                // Définir comme état par défaut
                rootStateMachine.defaultState = idleState;
                
                // Assigner le controller
                animator.runtimeAnimatorController = basicController;
                
                Debug.Log($"[Character3DPreview] Controller basique créé pour {_currentCharacterInstance.name}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Character3DPreview] Erreur lors de la création du controller basique : {e.Message}");
                
                // Dernier recours : désactiver temporairement l'Animator
                animator.enabled = false;
                animator.enabled = true;
            }
        }
        
        /// <summary>
        /// Cherche une animation Idle dans les ressources du GameObject
        /// </summary>
        private AnimationClip FindIdleAnimationClip()
        {
            // 🎯 AMÉLIORATION : Chercher d'abord dans le GameObject instancié
            
            // 1. Chercher dans tous les composants Animation du préfab
            Animation[] animations = _currentCharacterInstance.GetComponentsInChildren<Animation>();
            
            foreach (var anim in animations)
            {
                if (anim.clip != null && anim.clip.name.ToLower().Contains("idle"))
                {
                    Debug.Log($"[Character3DPreview] Animation Idle trouvée dans composant Animation : {anim.clip.name}");
                    return anim.clip;
                }
            }
            
            // 2. Chercher dans l'Animator si il a des clips assignés directement
            Animator selfAnimator = _currentCharacterInstance.GetComponentInChildren<Animator>();
            if (selfAnimator != null && selfAnimator.runtimeAnimatorController != null)
            {
                foreach (var clip in selfAnimator.runtimeAnimatorController.animationClips)
                {
                    if (clip.name.ToLower().Contains("idle"))
                    {
                        Debug.Log($"[Character3DPreview] Animation Idle trouvée dans Animator : {clip.name}");
                        return clip;
                    }
                }
            }
            
            // 3. Chercher dans les ressources du projet (plus large)
            AnimationClip[] allClips = Resources.FindObjectsOfTypeAll<AnimationClip>();
            foreach (var clip in allClips)
            {
                // Filtrer seulement les clips qui semblent appartenir à ce modèle
                if (clip.name.ToLower().Contains("idle") && 
                    (clip.name.ToLower().Contains("barbarian") || 
                     clip.name.ToLower().Contains("warrior") ||
                     clip.name.ToLower().Contains("character")))
                {
                    Debug.Log($"[Character3DPreview] Animation Idle trouvée dans Resources : {clip.name}");
                    return clip;
                }
            }
            
            // 4. Chercher n'importe quelle animation Idle comme dernier recours
            foreach (var clip in allClips)
            {
                if (clip.name.ToLower().Contains("idle"))
                {
                    Debug.Log($"[Character3DPreview] Animation Idle générique trouvée : {clip.name}");
                    return clip;
                }
            }
            
            Debug.LogWarning($"[Character3DPreview] Aucune animation Idle trouvée pour {_currentCharacterInstance.name}");
            return null;
        }
        #endif
        
        /// <summary>
        /// Force une animation de base sans controller (Runtime uniquement)
        /// </summary>
        private void ForceBasicIdleAnimation(Animator animator)
        {
            // Essayer de forcer l'animator à prendre une pose neutre
            if (animator.avatar != null && animator.avatar.isHuman)
            {
                // Pour les avatars humanoid, on peut forcer une pose T-pose moins prononcée
                animator.enabled = false;
                animator.enabled = true;
                
                Debug.Log($"[Character3DPreview] Animation forcée pour {_currentCharacterInstance.name} (avatar humanoid)");
            }
            else
            {
                Debug.LogWarning($"[Character3DPreview] Impossible de forcer une animation pour {_currentCharacterInstance.name}");
            }
        }
        
        public void ClearPreview()
        {
            if (_currentCharacterInstance != null)
            {
                Destroy(_currentCharacterInstance);
                _currentCharacterInstance = null;
            }
        }
        
        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}

