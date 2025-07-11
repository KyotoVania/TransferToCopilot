using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Gère les éléments visuels de l'écran de chargement.
/// Ce script est placé sur l'objet racine du prefab de l'écran de chargement.
/// Il est piloté par le GameManager.
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    [Header("Références UI")]
    [Tooltip("La barre de progression principale.")]
    [SerializeField] private Slider loadingProgressBar;

    [Tooltip("Le texte affichant le pourcentage (optionnel).")]
    [SerializeField] private TextMeshProUGUI loadingTextValue;

    [Tooltip("Le conteneur des objets qui tournent pour l'animation.")]
    [SerializeField] private GameObject loadingRotateContainer;

    [Header("Paramètres d'Animation")]
    [Tooltip("Vitesse de rotation de l'animation en degrés par seconde.")]
    [SerializeField] private float rotationSpeed = -90f; // Négatif pour tourner dans le sens des aiguilles d'une montre

    private List<Transform> _rotatingParts = new List<Transform>();

    private void Awake()
    {
        // On s'assure que les références sont bien là
        if (loadingProgressBar == null)
        {
            Debug.LogError("[LoadingScreenManager] La référence vers le Slider n'est pas assignée !", this);
            enabled = false;
        }

        // Peupler la liste des éléments rotatifs
        if (loadingRotateContainer != null)
        {
            foreach (Transform child in loadingRotateContainer.transform)
            {
                _rotatingParts.Add(child);
            }
        }
    }

    private void Update()
    {
        // Animer la rotation en continu si le panel est actif
        if (loadingRotateContainer != null && loadingRotateContainer.activeInHierarchy)
        {
            loadingRotateContainer.transform.Rotate(0, 0, rotationSpeed * Time.unscaledDeltaTime);
        }
    }

    /// <summary>
    /// Met à jour la valeur de la barre de progression et le texte associé.
    /// </summary>
    /// <param name="progress">La progression du chargement, de 0.0 à 1.0.</param>
    public void UpdateProgress(float progress)
    {
        if (loadingProgressBar != null)
        {
            // La progression de LoadSceneAsync s'arrête à 0.9. On la mappe sur 1.0 pour que la barre aille jusqu'au bout.
            float displayProgress = Mathf.Clamp01(progress / 0.9f);
            loadingProgressBar.value = displayProgress *10; // Convertir en pourcentage
            Debug.Log($"[LoadingScreenManager] Mise à jour de la progression : {displayProgress * 100}%");
        }

        if (loadingTextValue != null)
        {
            loadingTextValue.text = $"{Mathf.CeilToInt(progress * 100)}%";
        }
    }
}