using UnityEngine;
using System.Collections.Generic;

// Ajout de [ExecuteInEditMode] pour que le script fonctionne dans l'éditeur Unity
[ExecuteInEditMode]
public class CloudGenerator : MonoBehaviour
{
    [Header("Configuration des Nuages")]
    [Tooltip("La liste de vos prefabs de nuages. Faites-en glisser plusieurs pour de la variété.")]
    public List<GameObject> cloudPrefabs;

    [Tooltip("Le nombre total de nuages à générer.")]
    public int numberOfClouds = 200;

    [Header("Zone de Placement (Anneau)")]
    [Tooltip("Le centre autour duquel les nuages seront placés. Vous pouvez utiliser le centre de votre grille (board).")]
    public Transform centerPoint;

    [Tooltip("La distance minimale par rapport au centre.")]
    public float minRadius = 30f;

    [Tooltip("La distance maximale par rapport au centre.")]
    public float maxRadius = 80f;

    [Header("Placement en Hauteur")]
    [Tooltip("La hauteur minimale des nuages par rapport au centre.")]
    public float minHeight = -10f;

    [Tooltip("La hauteur maximale des nuages par rapport au centre.")]
    public float maxHeight = 15f;
    
    [Header("Variation Aléatoire")]
    [Tooltip("Taille minimale pour un nuage (ex: 0.8 = 80% de la taille originale).")]
    public float minScale = 0.8f;

    [Tooltip("Taille maximale pour un nuage (ex: 2.5 = 250% de la taille originale).")]
    public float maxScale = 2.5f;

    // Contexte Menu pour ajouter des boutons dans l'inspecteur du composant
    [ContextMenu("1. Générer les Nuages")]
    public void Generate()
    {
        // On s'assure d'avoir les prérequis
        if (cloudPrefabs == null || cloudPrefabs.Count == 0 || centerPoint == null)
        {
            Debug.LogError("[CloudGenerator] Assurez-vous d'assigner au moins un prefab de nuage et un point central !", this);
            return;
        }

        // On nettoie les anciens nuages avant d'en générer de nouveaux
        Clear();

        // On génère les nuages
        for (int i = 0; i < numberOfClouds; i++)
        {
            // --- 1. Calculer une position aléatoire en anneau ---
            float randomAngle = Random.Range(0f, 360f); // Un angle aléatoire sur le cercle
            float randomRadius = Random.Range(minRadius, maxRadius); // Une distance aléatoire dans l'anneau
            
            // Convertir les coordonnées polaires (angle, rayon) en coordonnées cartésiennes (x, z)
            Vector3 position = new Vector3(
                Mathf.Sin(randomAngle * Mathf.Deg2Rad) * randomRadius,
                0, // La hauteur est ajoutée ensuite
                Mathf.Cos(randomAngle * Mathf.Deg2Rad) * randomRadius
            );

            // --- 2. Ajouter la hauteur et centrer sur le point de référence ---
            position.y = Random.Range(minHeight, maxHeight);
            position += centerPoint.position;

            // --- 3. Choisir un prefab de nuage au hasard dans la liste ---
            GameObject randomCloudPrefab = cloudPrefabs[Random.Range(0, cloudPrefabs.Count)];

            // --- 4. Instancier le nuage ---
            GameObject cloudInstance = Instantiate(
                randomCloudPrefab, 
                position, 
                // Rotation Y aléatoire pour que tous les nuages ne soient pas orientés pareil
                Quaternion.Euler(0, Random.Range(0, 360), 0),
                // Le parent sera cet objet pour garder la scène propre
                this.transform 
            );
            
            // --- 5. Appliquer une taille aléatoire ---
            float randomScale = Random.Range(minScale, maxScale);
            cloudInstance.transform.localScale = Vector3.one * randomScale;
        }

        Debug.Log($"[CloudGenerator] {numberOfClouds} nuages ont été générés avec succès !", this);
    }

    // Un deuxième bouton pour nettoyer facilement la scène
    [ContextMenu("2. Nettoyer les Nuages")]
    public void Clear()
    {
        // On parcourt les enfants de cet objet à l'envers (plus sûr lors de la suppression)
        for (int i = this.transform.childCount - 1; i >= 0; i--)
        {
            // On utilise DestroyImmediate car nous sommes en mode éditeur. Destroy ne fonctionnerait pas.
            DestroyImmediate(this.transform.GetChild(i).gameObject);
        }
        Debug.Log("[CloudGenerator] Tous les nuages enfants ont été nettoyés.", this);
    }
}