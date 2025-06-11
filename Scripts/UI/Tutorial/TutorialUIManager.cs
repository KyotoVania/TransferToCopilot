// Fichier: Scripts/UI/Tutorial/TutorialUIManager.cs (Version Corrigée)
using UnityEngine;
using TMPro;
using System.Collections;

public class TutorialUIManager : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI tutorialTextDisplay;
    [SerializeField] private float fadeDuration = 0.3f;

    private CanvasGroup canvasGroup;
    private Coroutine currentFadeCoroutine;

    private void Awake()
    {
        // On s'assure que le CanvasGroup existe
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // --- MODIFICATION CLÉ ---
        // On ne désactive plus le panelRoot ici.
        // On le rend juste invisible et non-interactable.
        // L'objet reste ACTIF dans la hiérarchie.
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // On peut cacher le panelRoot si on veut, mais le plus important est que le GameObject
        // qui porte ce script reste actif. Pour plus de simplicité, laissons le panelRoot être le même
        // que ce GameObject.
        if (panelRoot == null)
        {
            panelRoot = this.gameObject;
        }
    }

    public void ShowStep(TutorialStep step)
    {
        if (step == null)
        {
            HidePanel();
            return;
        }

        tutorialTextDisplay.text = step.tutorialText;

        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
        }
        currentFadeCoroutine = StartCoroutine(FadePanel(true));
    }

    public void HidePanel()
    {
        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
        }
        currentFadeCoroutine = StartCoroutine(FadePanel(false));
    }

    private IEnumerator FadePanel(bool fadeIn)
    {
        // --- NOUVELLE LOGIQUE ROBUSTE ---
        // 1. On s'assure que le GameObject est actif AVANT de faire quoi que ce soit.
        // C'est la correction principale de l'erreur.
        if (fadeIn)
        {
            panelRoot.SetActive(true);
        }

        float targetAlpha = fadeIn ? 1f : 0f;
        float startAlpha = canvasGroup.alpha;
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            // Utiliser unscaledDeltaTime est une bonne pratique pour les animations d'UI
            // qui ne doivent pas être affectées par un Time.timeScale modifié.
            elapsedTime += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;

        // Mettre à jour l'interactivité
        canvasGroup.interactable = fadeIn;
        canvasGroup.blocksRaycasts = fadeIn;

        // 2. Si on a fait un fade out, on peut maintenant désactiver le GameObject
        // en toute sécurité, une fois l'animation terminée.
        if (!fadeIn)
        {
            panelRoot.SetActive(false);
        }

        currentFadeCoroutine = null;
    }
}