using UnityEngine;
using UnityEngine.InputSystem;

public enum InputDeviceType
{
    KeyboardAndMouse,
    Gamepad
}

public class InputDeviceManager : MonoBehaviour
{
    public static InputDeviceManager Instance { get; private set; }
    public InputDeviceType CurrentDevice { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        // On s'abonne à l'événement global de changement d'action
        InputSystem.onActionChange += OnActionChange;
    }

    private void OnDisable()
    {
        InputSystem.onActionChange -= OnActionChange;
    }

    private void OnActionChange(object obj, InputActionChange change)
    {
        // On s'intéresse uniquement au moment où une action est effectuée
        if (change != InputActionChange.ActionStarted)
            return;

        // On récupère le dernier appareil qui a déclenché l'action
        var lastDevice = ((InputAction)obj).activeControl?.device;

        if (lastDevice == null)
            return;

        // On vérifie le type de l'appareil et on met à jour notre état
        if (lastDevice is Gamepad)
        {
            if (CurrentDevice != InputDeviceType.Gamepad)
            {
                CurrentDevice = InputDeviceType.Gamepad;
                OnDeviceChanged();
            }
        }
        else if (lastDevice is Keyboard || lastDevice is Mouse)
        {
            if (CurrentDevice != InputDeviceType.KeyboardAndMouse)
            {
                CurrentDevice = InputDeviceType.KeyboardAndMouse;
                OnDeviceChanged();
            }
        }
    }

    private void OnDeviceChanged()
    {
        Debug.Log($"Input device changed to: {CurrentDevice}");
        if (CurrentDevice == InputDeviceType.Gamepad)
        {
            // On cache et verrouille le curseur
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        else // KeyboardAndMouse
        {
            // On affiche et libère le curseur
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
}