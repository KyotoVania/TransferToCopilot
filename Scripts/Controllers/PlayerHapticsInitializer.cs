using UnityEngine;

// Ce script doit être placé sur le même GameObject que votre SequenceController.
public class PlayerHapticsInitializer : MonoBehaviour
{
    [Tooltip("Optionnel : Assignez manuellement l'objet qui sert de Listener Wwise. Si laissé vide, le script cherchera la caméra principale.")]
    public GameObject wwiseListenerObject;

    void Start()
    {
        // 1. Déterminer quel GameObject est le Listener principal.
        if (wwiseListenerObject == null)
        {
            if (Camera.main != null)
            {
                // Par défaut, le listener est souvent sur la caméra principale.
                wwiseListenerObject = Camera.main.gameObject;
                Debug.Log("[PlayerHapticsInitializer] Listener Wwise trouvé sur la caméra principale.");
            }
            else
            {
                Debug.LogError("[PlayerHapticsInitializer] Aucun Listener Wwise assigné et la caméra principale est introuvable. L'haptique risque de ne pas fonctionner.");
                return;
            }
        }

        // 2. Obtenir l'ID unique du GameObject Listener pour Wwise.
        ulong listenerId = AkUnitySoundEngine.GetAkGameObjectID(wwiseListenerObject);
        if (listenerId == AkUnitySoundEngine.AK_INVALID_GAME_OBJECT)
        {
            Debug.LogError("[PlayerHapticsInitializer] Le GameObject Listener n'est pas valide ou enregistré auprès de Wwise. Assurez-vous qu'il possède un composant AkGameObj.");
            return;
        }

        // 3. Préparer le tableau des listeners. Ici, un seul.
        ulong[] listenerIds = new ulong[] { listenerId };
        uint numberOfListeners = (uint)listenerIds.Length;

        // 4. Lier ce GameObject (l'émetteur de son/haptique) au listener spécifié.
        // C'est l'étape clé. On utilise AkUnitySoundEngine et la bonne surcharge de la méthode SetListeners.
        AKRESULT result = AkUnitySoundEngine.SetListeners(this.gameObject, listenerIds, numberOfListeners);

        if (result == AKRESULT.AK_Success)
        {
            Debug.Log($"[PlayerHapticsInitializer] Le lien entre l'émetteur '{this.gameObject.name}' et le listener '{wwiseListenerObject.name}' a été établi avec succès pour l'haptique.");
        }
        else
        {
            Debug.LogError($"[PlayerHapticsInitializer] Échec de la liaison avec le listener. Code d'erreur Wwise : {result}");
        }
    }
}