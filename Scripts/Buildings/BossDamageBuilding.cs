// Fichier: Scripts/Buildings/BossDamageBuilding.cs
using UnityEngine;

public class BossDamageBuilding : NeutralBuilding
{
    [Header("Boss Targeting")]
    [Tooltip("Faites glisser ici le Boss que ce bâtiment doit attaquer.")]
    [SerializeField] private BossUnit targetBoss; // La nouvelle variable pour la cible !

    [Header("Boss Damage Settings")]
    [Tooltip("Le pourcentage de la vie maximale du boss à infliger comme dégâts lors de la capture.")]
    [Range(0f, 100f)]
    [SerializeField] private float damagePercentage = 10f;

    /// <summary>
    /// Cette méthode est appelée automatiquement lorsque l'équipe du bâtiment change.
    /// </summary>
    protected override void OnTeamChanged(TeamType newTeam)
    {
        base.OnTeamChanged(newTeam);

        // Si le bâtiment est capturé par le joueur
        if (newTeam == TeamType.Player)
        {
            // On vérifie si une cible a été assignée dans l'inspecteur
            if (targetBoss != null)
            {
                Debug.Log($"[BossDamageBuilding] Bâtiment capturé ! Inflige {damagePercentage}% de dégâts au boss '{targetBoss.name}'.");
                // On appelle la méthode pour infliger des dégâts sur notre cible spécifique
                targetBoss.TakePercentageDamage(damagePercentage);
            }
            else
            {
                // Message d'erreur si on a oublié d'assigner le boss dans l'éditeur
                Debug.LogError($"[BossDamageBuilding] Le bâtiment '{this.name}' a été capturé, mais aucun 'Target Boss' n'a été assigné dans l'inspecteur !");
            }
        }
    }
}