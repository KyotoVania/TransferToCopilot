using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Game.Observers;
public class AutoHorizontalComboDisplay : MonoBehaviour, IComboObserver
{
    [Header("Sprites numériques (0 à 9)")]
    [Tooltip("Tableau de 10 sprites correspondant aux chiffres 0 à 9.")]
    public Sprite[] digitSprites;

    [Header("Layout Settings")]
    [Tooltip("Nombre de chiffres à afficher (par défaut 3).")]
    public int numDigits = 3;
    [Tooltip("Espacement horizontal (en pixels) entre chaque chiffre.")]
    public float spacing = 50f;

    [Header("Dimensions des cellules")]
    [Tooltip("Largeur (en pixels) de chaque cellule.")]
    public float childWidth = 100f;
    [Tooltip("Hauteur (en pixels) de chaque cellule.")]
    public float childHeight = 100f;

    [Header("Délai et animation")]
    [Tooltip("Délai (en secondes) avant de lancer l'animation d'apparition lors de la première apparition.")]
    public float displayDelay = 1.0f;
    [Tooltip("Décalage vertical initial (en pixels) pour l'apparition.")]
    public float ySpawnOffset = 100f;
    [Tooltip("Durée (en secondes) de l'animation de descente vers la position finale.")]
    public float fallDuration = 0.5f;

    private Image[] digitCells;
    private Vector2[] finalPositions;
    private int[] currentDigits;
    private Coroutine displayCoroutine;
    private int pendingCombo;
    private bool firstAppearance = true;

    private void Awake()
    {
        InitializeDisplay();
    }

    private void InitializeDisplay()
    {
        if (digitSprites == null || digitSprites.Length != 10)
        {
            Debug.LogError($"[{nameof(AutoHorizontalComboDisplay)}] Le tableau digitSprites doit contenir exactement 10 sprites (0 à 9).");
            enabled = false;
            return;
        }

        CreateMissingCells();
        InitializeArrays();
        SetupDigitCells();

        pendingCombo = 0;
        UpdateDisplay(0);
    }

    private void CreateMissingCells()
    {
        if (transform.childCount < numDigits)
        {
            int toCreate = numDigits - transform.childCount;
            for (int i = 0; i < toCreate; i++)
            {
                GameObject newCell = new GameObject("DigitCell" + i, typeof(RectTransform), typeof(Image));
                newCell.transform.SetParent(transform, false);
            }
        }
    }

    private void InitializeArrays()
    {
        digitCells = new Image[numDigits];
        finalPositions = new Vector2[numDigits];
        currentDigits = new int[numDigits];
    }

    private void SetupDigitCells()
    {
        for (int i = 0; i < numDigits; i++)
        {
            digitCells[i] = transform.GetChild(i).GetComponent<Image>();
            if (digitCells[i] == null)
            {
                Debug.LogError($"[{nameof(AutoHorizontalComboDisplay)}] L'enfant {i} n'a pas de composant Image!");
                enabled = false;
                return;
            }

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

        digitCells[index].sprite = digitSprites[0];
        currentDigits[index] = -1;

        Color c = digitCells[index].color;
        c.a = 0f;
        digitCells[index].color = c;
    }

    private void OnEnable()
    {
        if (!enabled) return;

        if (ComboController.Instance == null)
        {
            Debug.LogError($"[{nameof(AutoHorizontalComboDisplay)}] ComboController.Instance is null!");
            enabled = false;
            return;
        }

        Debug.Log("Adding combo display as observer..."); // Add this
        ComboController.Instance.AddObserver(this);
    }

    private void OnDisable()
    {
        if (ComboController.Instance != null)
        {
            ComboController.Instance.RemoveObserver(this);
        }
    }

    // Implémentation de IComboObserver
    public void OnComboUpdated(int newCombo)
    {
        Debug.Log($"Combo display received update: {newCombo}"); // Add this
        UpdateDisplay(newCombo);
    }

    public void OnComboReset()
    {
        ResetDisplay();
    }

    private void UpdateDisplay(int comboCount)
    {
        pendingCombo = comboCount;
        if (displayCoroutine == null)
        {
            displayCoroutine = StartCoroutine(UpdateDisplayCoroutine());
        }
    }

    private IEnumerator UpdateDisplayCoroutine()
    {
        if (firstAppearance)
        {
            yield return new WaitForSeconds(displayDelay);
            firstAppearance = false;
        }

        string s = pendingCombo.ToString().PadLeft(numDigits, '0');
        bool anyChange = false;

        for (int i = 0; i < numDigits; i++)
        {
            int newDigit = int.Parse(s[i].ToString());
            if (newDigit != currentDigits[i])
            {
                anyChange = true;
                yield return StartCoroutine(AnimateCell(i, newDigit));
            }
        }

        if (!anyChange)
            displayCoroutine = null;
        else
            displayCoroutine = StartCoroutine(UpdateDisplayCoroutine());
    }

    private IEnumerator AnimateCell(int index, int newDigit)
    {
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
            rt.anchoredPosition = Vector2.Lerp(startPos, finalPositions[index], t);
            yield return null;
        }

        rt.anchoredPosition = finalPositions[index];
        currentDigits[index] = newDigit;
    }

    private void ResetDisplay()
    {
        UpdateDisplay(0);
    }
}