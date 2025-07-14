using UnityEngine;

/// <summary>
/// Ce script fait en sorte que l'objet auquel il est attaché
/// fasse toujours face à la caméra principale de la scène.
/// C'est idéal pour les éléments d'UI en World Space comme les barres de vie,
/// afin qu'ils restent toujours lisibles pour le joueur.
/// </summary>
public class Billboard : MonoBehaviour
{
    // Référence à la caméra principale de la scène.
    // Nous la mettons en cache pour ne pas avoir à la rechercher à chaque frame,
    // ce qui est bien meilleur pour les performances.
    private Camera mainCamera;

    /// <summary>
    /// La méthode Start est appelée une seule fois au début, avant la première frame.
    /// C'est le moment idéal pour initialiser nos variables.
    /// </summary>
    void Start()
    {
        // On trouve la caméra principale de la scène et on la stocke
        // dans notre variable mainCamera.
        mainCamera = Camera.main;
    }

    /// <summary>
    /// La méthode LateUpdate est appelée une fois par frame, mais après
    /// que toutes les méthodes Update() aient été exécutées.
    /// On l'utilise ici pour être certain que la caméra a fini tous ses
    /// mouvements pour la frame en cours (ceux du joueur, du targeting, etc.).
    /// </summary>
    void LateUpdate()
    {
        // On vérifie si la référence à la caméra existe bien pour éviter les erreurs.
        if (mainCamera == null)
        {
            // Si on ne la trouve pas, on arrête le script pour cette frame.
            Debug.LogWarning("Billboard Script: Main Camera not found!");
            return;
        }

        // C'est ici que la magie opère !

        // 1. On oriente notre objet (la barre de vie) pour qu'il "regarde" la position de la caméra.
        transform.LookAt(mainCamera.transform);

        // 2. Par défaut, LookAt fait en sorte que l'axe Z (le "devant" de l'objet) pointe vers la cible.
        // Cela peut orienter notre barre de vie de manière étrange. On veut que ce soit la face qui soit visible.
        // Pour corriger ça, on inverse la rotation en la tournant de 180 degrés sur l'axe Y.
        // L'axe Y est l'axe vertical. En tournant autour, on fait un "demi-tour" à notre pancarte.
        transform.Rotate(0f, 180f, 0f);
    }
}