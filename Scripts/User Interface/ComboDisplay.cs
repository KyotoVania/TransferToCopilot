using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Game.Observers;

public class AutoHorizontalComboDisplay : MonoBehaviour, IComboObserver
{
    [Header("Sprites numériques (0 à 9)")]
    public Sprite[] digitSprites;

    [Header("Layout Settings")]
    public int numDigits = 3;
    public float spacing = 50f;

    [Header("Dimensions des cellules")]
    public float childWidth = 100f;
    public float childHeight = 100f;

    [Header("Animation")]
    [Tooltip("Délai (en secondes) avant de lancer l'animation à chaque fois que l'UI est activée.")]
    public float displayDelay = 0.35f; // J'ai renommé cette variable pour plus de clarté
    public float ySpawnOffset = 100f;
    public float fallDuration = 0.5f;

    private HorizontalLayoutGroup layoutGroup;
    private Image[] digitCells;
    private Vector2[] finalPositions;
    private int[] currentDigits;
    private Coroutine displayUpdateCoroutine;
    private bool isInitialized = false;

    private void Awake()
    {
        layoutGroup = GetComponent<HorizontalLayoutGroup>();
        InitializeDisplay();
    }

    private void OnEnable()
    {
        if (!isInitialized) InitializeDisplay();
        if (!enabled) return;

        if (ComboController.Instance != null)
        {
            ComboController.Instance.AddObserver(this);
            StartCoroutine(AnimateOnEnable());
        }
        else
        {
            Debug.LogError($"[{nameof(AutoHorizontalComboDisplay)}] ComboController.Instance is null!");
            enabled = false;
        }
    }

    private void OnDisable()
    {
        if (ComboController.Instance != null)
        {
            ComboController.Instance.RemoveObserver(this);
        }
        StopAllCoroutines();
        HideAllCells();
    }

    private IEnumerator AnimateOnEnable()
    {
        // LOG DE DÉBOGAGE : Vérifiez cette ligne dans votre console pour voir la valeur du délai utilisé.
        Debug.Log($"[{nameof(AutoHorizontalComboDisplay)}] AnimateOnEnable: En attente d'un délai de {displayDelay} secondes.");

        // Attendre le délai à chaque activation.
        yield return new WaitForSeconds(displayDelay);

        // Lancer l'animation avec la valeur actuelle du combo.
        if (ComboController.Instance != null)
        {
            TriggerDisplayUpdate(ComboController.Instance.comboCount);
        }
    }

    private void TriggerDisplayUpdate(int comboValue)
    {
        if (displayUpdateCoroutine != null)
        {
            StopCoroutine(displayUpdateCoroutine);
        }
        displayUpdateCoroutine = StartCoroutine(AnimateDisplayUpdate(comboValue));
    }

    public void OnComboUpdated(int newCombo)
    {
        TriggerDisplayUpdate(newCombo);
    }

    public void OnComboReset()
    {
        TriggerDisplayUpdate(0);
    }

    private IEnumerator AnimateDisplayUpdate(int comboCount)
    {
        if (layoutGroup != null) layoutGroup.enabled = false;

        string s = comboCount.ToString().PadLeft(numDigits, '0');
        if (s.Length > numDigits) s = s.Substring(s.Length - numDigits);

        bool hasAnimated = false;
        for (int i = 0; i < numDigits; i++)
        {
            int newDigit = int.Parse(s[i].ToString());
            if (newDigit != currentDigits[i] || (digitCells[i] != null && digitCells[i].color.a < 1f))
            {
                hasAnimated = true;
                StartCoroutine(AnimateCell(i, newDigit));
            }
        }

        if (hasAnimated)
        {
            yield return new WaitForSeconds(fallDuration);
        }

        if (layoutGroup != null) layoutGroup.enabled = true;

        displayUpdateCoroutine = null;
    }

    private IEnumerator AnimateCell(int index, int newDigit)
    {
        currentDigits[index] = newDigit;
        RectTransform rt = digitCells[index].GetComponent<RectTransform>();
        Vector2 startPos = finalPositions[index] + new Vector2(0, ySpawnOffset);

        digitCells[index].sprite = digitSprites[newDigit];
        rt.anchoredPosition = startPos;

        Color c = digitCells[index].color;
        c.a = 1f;
        digitCells[index].color = c;

        float elapsed = 0f;
        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fallDuration);
            float easedT = 1f - Mathf.Pow(1f - t, 3);
            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, finalPositions[index], easedT);
            yield return null;
        }

        rt.anchoredPosition = finalPositions[index];
    }

    // --- Méthodes d'initialisation (Setup) ---

    private void InitializeDisplay(){ if (isInitialized) return; if (digitSprites == null || digitSprites.Length != 10) { enabled = false; return; } CreateMissingCells(); InitializeArrays(); SetupDigitCells(); isInitialized = true; }
    private void CreateMissingCells(){ if (transform.childCount < numDigits){ int toCreate = numDigits - transform.childCount; for (int i = 0; i < toCreate; i++){ GameObject newCell = new GameObject("DigitCell" + (transform.childCount + i), typeof(RectTransform), typeof(Image)); newCell.transform.SetParent(transform, false);}}}
    private void InitializeArrays(){ digitCells = new Image[numDigits]; finalPositions = new Vector2[numDigits]; currentDigits = new int[numDigits];}

    private void SetupDigitCells()
    {
        for (int i = 0; i < numDigits; i++)
        {
            digitCells[i] = transform.GetChild(i).GetComponent<Image>();
            if (digitCells[i] == null) { enabled = false; return; }
            SetupDigitCell(i);
        }
    }

    private void SetupDigitCell(int index)
    {
        RectTransform rt = digitCells[index].GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(childWidth, childHeight);
        float posX = (index - (numDigits - 1)) * spacing;
        rt.anchoredPosition = new Vector2(posX, rt.anchoredPosition.y);
        finalPositions[index] = rt.anchoredPosition;
        HideCell(index);
    }

    private void HideCell(int index)
    {
        if (digitCells[index] == null) return;
        digitCells[index].sprite = digitSprites[0];
        currentDigits[index] = -1;
        Color c = digitCells[index].color;
        c.a = 0f;
        digitCells[index].color = c;
    }

    private void HideAllCells()
    {
        if (digitCells == null) return;
        for (int i = 0; i < digitCells.Length; i++)
        {
            HideCell(i);
        }
    }
}