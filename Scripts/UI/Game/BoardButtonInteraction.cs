using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using ScriptableObjects;

// Now implements EventSystem interfaces for proper input support
public class BoardButtonInteraction : MonoBehaviour, 
    IPointerClickHandler, 
    IPointerEnterHandler, 
    IPointerExitHandler,
    ISubmitHandler,
    ISelectHandler,
    IDeselectHandler
{
    public enum BoardActionType
    {
        GoToLobby,
        GoToNextLevel
    }

    [Tooltip("Définit l'action que ce bouton déclenchera.")]
    public BoardActionType actionType;

    [Header("Configuration pour 'Prochain Niveau'")]
    [Tooltip("Assigner le LevelData_SO pour le prochain niveau (seulement si actionType est GoToNextLevel).")]
    public LevelData_SO nextLevelData;

    [Header("Visual Feedback")]
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float clickScale = 0.9f;
    [SerializeField] private float animationSpeed = 0.1f;
    
    private Vector3 _originalScale;
    private Coroutine _currentAnimation;
    private bool _isHovered = false;
    private bool _isSelected = false;

    private void Awake()
    {
        _originalScale = transform.localScale;
        
        // Ensure this object has a collider for raycasting
        Collider collider = GetComponent<Collider>();
        if (collider == null)
        {
            Debug.LogError($"[BoardButtonInteraction] No Collider found on {gameObject.name}. Adding BoxCollider.");
            gameObject.AddComponent<BoxCollider>();
        }
    }

    // Mouse click
    public void OnPointerClick(PointerEventData eventData)
    {
        ExecuteAction();
    }

    // Gamepad/Keyboard submit
    public void OnSubmit(BaseEventData eventData)
    {
        ExecuteAction();
    }

    // Mouse hover
    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        AnimateScale(hoverScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        if (!_isSelected)
        {
            AnimateScale(1f);
        }
    }

    // Gamepad/Keyboard selection
    public void OnSelect(BaseEventData eventData)
    {
        _isSelected = true;
        AnimateScale(hoverScale);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _isSelected = false;
        if (!_isHovered)
        {
            AnimateScale(1f);
        }
    }

    private void ExecuteAction()
    {
        Debug.Log($"[BoardButtonInteraction] Action exécutée : {gameObject.name}, Type : {actionType}", this);

        // Click feedback animation
        StartCoroutine(ClickFeedbackAnimation());

        // Reset Time.timeScale before loading
        Time.timeScale = 1f;

        if (GameManager.Instance == null)
        {
            Debug.LogError("[BoardButtonInteraction] GameManager.Instance non trouvé!", this);
            return;
        }

        // Restore gameplay controls before transitioning
        switch (actionType)
        {
            case BoardActionType.GoToLobby:
                GameManager.Instance.LoadHub();
                break;
            case BoardActionType.GoToNextLevel:
                if (nextLevelData != null)
                {
                    GameManager.Instance.LoadLevel(nextLevelData);
                }
                else
                {
                    Debug.LogWarning($"[BoardButtonInteraction] 'Next Level Data' non assigné pour {gameObject.name}.", this);
                    GameManager.Instance.LoadHub();
                }
                break;
        }
    }

    private void AnimateScale(float targetScale)
    {
        if (_currentAnimation != null)
        {
            StopCoroutine(_currentAnimation);
        }
        _currentAnimation = StartCoroutine(ScaleAnimation(_originalScale * targetScale));
    }

    private IEnumerator ScaleAnimation(Vector3 targetScale)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < animationSpeed)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / animationSpeed;
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        transform.localScale = targetScale;
        _currentAnimation = null;
    }

    private IEnumerator ClickFeedbackAnimation()
    {
        // Scale down
        yield return ScaleAnimation(_originalScale * clickScale);
        // Scale back
        yield return ScaleAnimation(_isHovered || _isSelected ? _originalScale * hoverScale : _originalScale);
    }

    private void OnDisable()
    {
        // Reset scale when disabled
        transform.localScale = _originalScale;
        if (_currentAnimation != null)
        {
            StopCoroutine(_currentAnimation);
            _currentAnimation = null;
        }
    }
}