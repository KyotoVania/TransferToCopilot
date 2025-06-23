using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MomentumDisplay : MonoBehaviour
{
    [Header("Références UI")]
    [Tooltip("L'Image qui sert de jauge de remplissage. Doit avoir son 'Image Type' sur 'Filled'.")]
    [SerializeField] private Image momentumFillImage;

    [Tooltip("Liste des GameObjects des 3 icônes de charge. Elles seront activées/désactivées.")]
    [SerializeField] private List<GameObject> chargeIcons;

    private MomentumManager _momentumManager;

    // La méthode Start est appelée APRES toutes les méthodes Awake.
    void Start()
    {
        // La validation des références reste ici.
        if (momentumFillImage == null || chargeIcons == null || chargeIcons.Count != 3)
        {
            Debug.LogError("[MomentumDisplay] Une ou plusieurs références UI ne sont pas assignées correctement !", this);
            this.enabled = false;
            return;
        }

        // Maintenant, quand on cherche l'instance, MomentumManager.Awake() aura déjà été exécuté.
        _momentumManager = MomentumManager.Instance;
        if (_momentumManager != null)
        {
            // On s'abonne à l'événement.
            _momentumManager.OnMomentumChanged += UpdateMomentumDisplay;
            // Et on met à jour l'affichage une première fois avec les valeurs actuelles.
            UpdateMomentumDisplay(_momentumManager.CurrentCharges, _momentumManager.CurrentMomentumValue);
        }
        else
        {
            // Cette erreur ne devrait plus se produire si le manager est bien dans la scène Core.
            Debug.LogError("[MomentumDisplay] MomentumManager.Instance non trouvé ! Assurez-vous que la scène 'Core' est bien chargée.", this);
            this.enabled = false;
        }
    }

    // OnDestroy est la contrepartie de Start. On l'utilise pour se désabonner.
    private void OnDestroy()
    {
        // Toujours se désabonner pour éviter les fuites de mémoire.
        if (_momentumManager != null)
        {
            _momentumManager.OnMomentumChanged -= UpdateMomentumDisplay;
        }
    }

    // La méthode de mise à jour reste identique.
    private void UpdateMomentumDisplay(int charges, float momentumValue)
    {
        if (momentumFillImage != null)
        {
            momentumFillImage.fillAmount = momentumValue / 3.0f;
        }

        if (chargeIcons != null)
        {
            for (int i = 0; i < chargeIcons.Count; i++)
            {
                if (chargeIcons[i] != null)
                {
                    chargeIcons[i].SetActive(i < charges);
                }
            }
        }
    }
}