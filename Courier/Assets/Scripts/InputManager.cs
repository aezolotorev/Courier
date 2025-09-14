using UnityEngine;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }
    public InputSystem_Actions Controls { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        Controls = new InputSystem_Actions();
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable() => Controls?.Enable();
    private void OnDisable() => Controls?.Disable();
    private void OnDestroy() => Controls?.Dispose();
}