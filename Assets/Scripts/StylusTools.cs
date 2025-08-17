using UnityEngine;
using UnityEngine.InputSystem;

public class StylusTools : MonoBehaviour
{
    private InputActionReference _tipActionRef;
    private InputActionReference _grabActionRef;
    private InputActionReference _optionActionRef;
    private InputActionReference _middleActionRef;

    private void OnEnable()
    {
        _tipActionRef.action.Enable();
        _grabActionRef.action.Enable();
        _optionActionRef.action.Enable();
        _middleActionRef.action.Enable();
    }

    private void OnDisable()
    {
        _tipActionRef.action.Disable();
        _grabActionRef.action.Disable();
        _optionActionRef.action.Disable();
        _middleActionRef.action.Disable();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
