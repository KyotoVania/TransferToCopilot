using UnityEngine;
using UnityEngine.UI; // Pour Image
using TMPro;          // Pour TextMeshProUGUI
using System.Collections.Generic;
using System.Collections;
using ScriptableObjects;


public class DialogueUIManager : MonoBehaviour
{
    public static DialogueUIManager Instance { get; private set; }

    [Header("UI Elements")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private Image speakerPortraitImage;
    [SerializeField] private TextMeshProUGUI dialogueTextDisplay;
    [SerializeField] private Button clickAdvanceButton;

    [Header("Configuration")]
    [SerializeField] private float textTypingSpeed = 50f;

    private Queue<DialogueEntry> _currentSequenceQueue;
    private DialogueEntry _currentEntry;
    private bool _isDisplayingDialogue = false;
    private bool _isTyping = false;
    private Coroutine _typingCoroutine;

    public static event System.Action OnDialogueSystemStart;
    public static event System.Action OnDialogueSystemEnd;

    private RhythmGameCameraController _cameraController;

    // NOUVEAU: Listes pour stocker les GameObjects des unités désactivées
    private List<GameObject> _deactivatedAllyUnitsByDialogue = new List<GameObject>();
    private List<GameObject> _deactivatedEnemyUnitsByDialogue = new List<GameObject>();


    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (dialoguePanel == null || speakerNameText == null || speakerPortraitImage == null || dialogueTextDisplay == null || clickAdvanceButton == null)
        {
            Debug.LogError($"[{gameObject.name}/DialogueUIManager] Un ou plusieurs éléments UI ne sont pas assignés!", this);
            enabled = false;
            return;
        }
        dialoguePanel.SetActive(false);
        _currentSequenceQueue = new Queue<DialogueEntry>();
    }

    void Start()
    {
        _cameraController = FindFirstObjectByType<RhythmGameCameraController>();
        if (_cameraController == null)
        {
            Debug.LogWarning($"[{gameObject.name}/DialogueUIManager] RhythmGameCameraController non trouvé.");
        }

        if (clickAdvanceButton != null)
        {
            clickAdvanceButton.onClick.RemoveAllListeners();
            clickAdvanceButton.onClick.AddListener(HandleAdvanceClick);
        }
    }

    public bool IsDialogueActive()
    {
        return _isDisplayingDialogue;
    }

    public void StartDialogue(DialogueSequence sequence)
    {
        if (sequence == null || sequence.entries.Count == 0) return;
        if (_isDisplayingDialogue) return;

        _isDisplayingDialogue = true;
        _currentSequenceQueue.Clear();
        foreach (var entry in sequence.entries)
        {
            _currentSequenceQueue.Enqueue(entry);
        }

        if (_cameraController != null)
        {
            _cameraController.controlsLocked = true;
            Debug.Log($"[{gameObject.name}/DialogueUIManager] Camera controls LOCKED by dialogue.");
        }

        // Désactiver les GameObjects des unités
        SetUnitsGameObjectsActive(false);

        OnDialogueSystemStart?.Invoke();
        Debug.Log($"[{gameObject.name}/DialogueUIManager] DialogueSystem START.");

        dialoguePanel.SetActive(true);
        DisplayNextEntry();
    }

    private void HandleAdvanceClick()
    {
        if (!_isDisplayingDialogue) return;
        if (_isTyping && _typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
            dialogueTextDisplay.text = _currentEntry.dialogueText;
            _isTyping = false;
        }
        else
        {
            DisplayNextEntry();
        }
    }

    private void DisplayNextEntry()
    {
        if (_currentSequenceQueue.Count > 0)
        {
            _currentEntry = _currentSequenceQueue.Dequeue();
            speakerNameText.text = _currentEntry.speakerName;
            if (_currentEntry.speakerPortrait != null)
            {
                speakerPortraitImage.sprite = _currentEntry.speakerPortrait;
                speakerPortraitImage.enabled = true;
            }
            else
            {
                speakerPortraitImage.enabled = false;
            }
            if (textTypingSpeed > 0)
            {
                if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
                _typingCoroutine = StartCoroutine(TypeText(_currentEntry.dialogueText));
            }
            else
            {
                dialogueTextDisplay.text = _currentEntry.dialogueText;
                _isTyping = false;
            }
        }
        else
        {
            EndDialogue();
        }
    }

    private IEnumerator TypeText(string textToType)
    {
        _isTyping = true;
        dialogueTextDisplay.text = "";
        float delay = 1.0f / Mathf.Max(1, textTypingSpeed);
        foreach (char letter in textToType.ToCharArray())
        {
            dialogueTextDisplay.text += letter;
            yield return new WaitForSecondsRealtime(delay);
        }
        _isTyping = false;
        _typingCoroutine = null;
    }

    private void EndDialogue()
    {
        if (!_isDisplayingDialogue) return;

        _isDisplayingDialogue = false;
        dialoguePanel.SetActive(false);
        _currentEntry = null;
        _currentSequenceQueue.Clear();
        if(_typingCoroutine != null) {
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
            _isTyping = false;
        }

        if (_cameraController != null)
        {
            _cameraController.controlsLocked = false;
            Debug.Log($"[{gameObject.name}/DialogueUIManager] Camera controls UNLOCKED by dialogue.");
        }

        // Réactiver les GameObjects des unités
        SetUnitsGameObjectsActive(true);

        OnDialogueSystemEnd?.Invoke();
        Debug.Log($"[{gameObject.name}/DialogueUIManager] DialogueSystem END.");
    }

    // MÉTHODE MODIFIÉE pour activer/désactiver les GameObjects des unités
    private void SetUnitsGameObjectsActive(bool shouldBeActive)
    {
        if (!shouldBeActive) // Si on désactive
        {
            _deactivatedAllyUnitsByDialogue.Clear();
            _deactivatedEnemyUnitsByDialogue.Clear();

            // Désactiver les unités alliées
            if (AllyUnitRegistry.Instance != null)
            {
                // Copier la liste pour itérer car SetActive(false) pourrait modifier la liste du registre via OnDisable
                foreach (AllyUnit allyUnit in AllyUnitRegistry.Instance.ActiveAllyUnits)
                {
                    if (allyUnit != null && allyUnit.gameObject.activeSelf)
                    {
                        _deactivatedAllyUnitsByDialogue.Add(allyUnit.gameObject);
                        allyUnit.gameObject.SetActive(false);
                    }
                }
                Debug.Log($"[DialogueUIManager] AllyUnits GameObjects DEACTIVATED (via Registry): {_deactivatedAllyUnitsByDialogue.Count}");
            }
            else // Fallback par tag si pas de registre
            {
                GameObject[] allyGameObjects = GameObject.FindGameObjectsWithTag("AllyUnit");
                foreach (GameObject unitGO in allyGameObjects)
                {
                    if (unitGO != null && unitGO.activeSelf)
                    {
                         _deactivatedAllyUnitsByDialogue.Add(unitGO);
                        unitGO.SetActive(false);
                    }
                }
                Debug.Log($"[DialogueUIManager] AllyUnits GameObjects DEACTIVATED (via Tag): {_deactivatedAllyUnitsByDialogue.Count}");
            }

            // Désactiver les unités ennemies
            GameObject[] enemyGameObjects = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (GameObject unitGO in enemyGameObjects)
            {
                if (unitGO != null && unitGO.activeSelf)
                {
                    _deactivatedEnemyUnitsByDialogue.Add(unitGO);
                    unitGO.SetActive(false);
                }
            }
            Debug.Log($"[DialogueUIManager] EnemyUnits GameObjects DEACTIVATED (via Tag): {_deactivatedEnemyUnitsByDialogue.Count}");
        }
        else // Si on réactive
        {
            foreach (GameObject unitGO in _deactivatedAllyUnitsByDialogue)
            {
                if (unitGO != null) // Vérifier s'il n'a pas été détruit entre-temps
                {
                    unitGO.SetActive(true);
                }
            }
            Debug.Log($"[DialogueUIManager] AllyUnits GameObjects REACTIVATED: {_deactivatedAllyUnitsByDialogue.Count}");
            _deactivatedAllyUnitsByDialogue.Clear();

            foreach (GameObject unitGO in _deactivatedEnemyUnitsByDialogue)
            {
                if (unitGO != null)
                {
                    unitGO.SetActive(true);
                }
            }
            Debug.Log($"[DialogueUIManager] EnemyUnits GameObjects REACTIVATED: {_deactivatedEnemyUnitsByDialogue.Count}");
            _deactivatedEnemyUnitsByDialogue.Clear();
        }
    }
}