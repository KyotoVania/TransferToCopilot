namespace User_Interface.Controllers
{
    using UnityEngine;
    using System.Collections;

    public class GameUIManager : MonoBehaviour
    {
        [Header("UI Prefabs & References")]
        [Tooltip("Le prefab du bandeau d'alerte de boss à afficher.")]
        [SerializeField] private GameObject bossWarningBannerPrefab;
        
        [Tooltip("Le Canvas principal de l'UI du jeu, où le bandeau sera instancié.")]
        [SerializeField] private Canvas mainUICanvas;

        [Header("Animation Settings")]
        [Tooltip("Durée totale d'affichage du bandeau")]
        [SerializeField] private float bannerDuration = 4f;
        
        [Tooltip("Durée du fade in/out")]
        [SerializeField] private float fadeDuration = 0.3f;
        

        // S'abonne aux événements quand le manager est activé.
        private void OnEnable()
        {
            EnemyRegistry.OnBossSpawned += HandleBossSpawned;
        }

        // Se désabonne pour éviter les erreurs.
        private void OnDisable()
        {
            EnemyRegistry.OnBossSpawned -= HandleBossSpawned;
        }

        /// <summary>
        /// Cette méthode est appelée automatiquement lorsque EnemyRegistry.OnBossSpawned est déclenché.
        /// </summary>
        private void HandleBossSpawned(EnemyUnit bossUnit)
        {
            Debug.Log($"[GameUIManager] Événement OnBossSpawned reçu pour {bossUnit.name}. Affichage du bandeau.");
            
            if (bossWarningBannerPrefab != null)
            {
                ShowBossBanner();
            }
            else
            {
                Debug.LogError("[GameUIManager] Le prefab du bandeau de boss n'est pas assigné !", this);
            }
        }

        private void ShowBossBanner()
        {
            if (mainUICanvas == null)
            {
                Debug.LogError("[GameUIManager] La référence au 'mainUICanvas' n'est pas assignée !", this);
                return;
            }

            GameObject bannerInstance = Instantiate(bossWarningBannerPrefab, mainUICanvas.transform);
            CanvasGroup bannerCanvasGroup = bannerInstance.GetComponent<CanvasGroup>();

            // S'il n'y a pas de CanvasGroup, on en ajoute un
            if (bannerCanvasGroup == null)
            {
                bannerCanvasGroup = bannerInstance.AddComponent<CanvasGroup>();
            }

            // On commence avec alpha à 0
            bannerCanvasGroup.alpha = 0f;

            // Animation d'entrée stylée avec un bounce
            LeanTween.alphaCanvas(bannerCanvasGroup, 1f, fadeDuration)
                .setEase(LeanTweenType.easeOutBounce)
                .setOnComplete(() => StartBlinkingAnimation(bannerCanvasGroup, bannerInstance));
        }

        private void StartBlinkingAnimation(CanvasGroup canvasGroup, GameObject bannerInstance)
        {
            float timePerBlink = 0.4f;
            int totalBlinks = Mathf.FloorToInt((bannerDuration - fadeDuration * 2) / timePerBlink);
    
            // On crée une séquence manuelle
            LTSeq sequence = LeanTween.sequence();
    
            // Ajouter chaque clignotement à la séquence
            for (int i = 0; i < totalBlinks; i++)
            {
                sequence.append(LeanTween.alphaCanvas(canvasGroup, 0.2f, timePerBlink * 0.5f).setEase(LeanTweenType.easeInOutSine));
                sequence.append(LeanTween.alphaCanvas(canvasGroup, 1f, timePerBlink * 0.5f).setEase(LeanTweenType.easeInOutSine));
            }
    
            // Ajouter l'animation de sortie
            sequence.append(() => {
                LeanTween.scale(bannerInstance, Vector3.zero, fadeDuration)
                    .setEase(LeanTweenType.easeInBack);
        
                LeanTween.alphaCanvas(canvasGroup, 0f, fadeDuration)
                    .setEase(LeanTweenType.easeInQuad)
                    .setOnComplete(() => {
                        if (bannerInstance != null)
                            Destroy(bannerInstance);
                    });
            });
        }
    }
}