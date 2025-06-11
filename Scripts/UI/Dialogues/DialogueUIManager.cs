// Fichier: Scripts2/UI/Dialogues/DialogueUIManager.cs (Corrigé)
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using ScriptableObjects; // <--- LIGNE AJOUTÉE ICI

/// <summary>
/// Gère UNIQUEMENT l'affichage du panneau de dialogue et du texte.
/// Il est maintenant contrôlé par des managers externes comme DialogueSequenceManager.
/// </summary>
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

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (dialoguePanel == null || clickAdvanceButton == null)
        {
            Debug.LogError($"[DialogueUIManager] Le panneau de dialogue ou le bouton pour avancer ne sont pas assignés !", this);
            enabled = false;
            return;
        }
        dialoguePanel.SetActive(false);
        _currentSequenceQueue = new Queue<DialogueEntry>();
    }

    void Start()
    {
        clickAdvanceButton.onClick.AddListener(HandleAdvanceClick);
    }

    public bool IsDialogueActive()
    {
        return _isDisplayingDialogue;
    }

    // La signature de cette méthode attend maintenant ScriptableObjects.DialogueSequence
    public void StartDialogue(DialogueSequence sequence)
    {
        if (sequence == null || sequence.entries.Count == 0 || _isDisplayingDialogue) return;

        _isDisplayingDialogue = true;
        _currentSequenceQueue.Clear();
        foreach (var entry in sequence.entries)
        {
            _currentSequenceQueue.Enqueue(entry);
        }

        OnDialogueSystemStart?.Invoke();
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
            if (speakerNameText != null) speakerNameText.text = _currentEntry.speakerName;
            if (speakerPortraitImage != null)
            {
                speakerPortraitImage.sprite = _currentEntry.speakerPortrait;
                speakerPortraitImage.enabled = (_currentEntry.speakerPortrait != null);
            }
            if (dialogueTextDisplay != null)
            {
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

        OnDialogueSystemEnd?.Invoke();
    }
}