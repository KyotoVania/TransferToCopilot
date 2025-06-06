using UnityEngine;
using TMPro;
using System.Text;
using Game.Observers;

public class GoldUI : MonoBehaviour, IGoldObserver
{
    [SerializeField] private TMP_Text goldText;

    private readonly StringBuilder _stringBuilder = new StringBuilder(20);
    private const string GoldPrefix = "Gold: ";

    private void Awake()
    {
        if (goldText == null)
        {
            Debug.LogError($"[{nameof(GoldUI)}] Le composant TMP_Text n'est pas assigné!", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (!enabled) return;

        if (GoldController.Instance == null)
        {
            Debug.LogError($"[{nameof(GoldUI)}] GoldController.Instance est null! Assurez-vous qu'il est présent dans la scène.", this);
            enabled = false;
            return;
        }

        GoldController.Instance.AddObserver(this);
        OnGoldUpdated(GoldController.Instance.GetCurrentGold());
    }

    private void OnDisable()
    {
        if (enabled && GoldController.Instance != null)
        {
            GoldController.Instance.RemoveObserver(this);
        }
    }

    public void OnGoldUpdated(int newAmount)
    {
        if (!enabled || goldText == null) return;

        _stringBuilder.Clear()
            .Append(GoldPrefix)
            .Append(newAmount);

        goldText.text = _stringBuilder.ToString();
    }
}