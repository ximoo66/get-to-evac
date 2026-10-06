using System;
using UnityEngine;
using UnityEngine.InputSystem;
using static NetCodeInputActions;

[CreateAssetMenu(fileName = "New Input Reader", menuName = "Input/Input Reader")]
public class InputReader : ScriptableObject, IPlayerActions
{
    public Action<bool> PrimaryAttackEvent;
    public Action<bool> JumpEvent;
    public Action<bool> CancelEvent;
    public Action<bool> RunEvent;
    public Action<Vector2> MoveEvent;

    private NetCodeInputActions myActions;

    private void OnEnable()
    {
        if (myActions == null)
        {
            myActions = new NetCodeInputActions();
            myActions.Player.SetCallbacks(this);
        }

        myActions.Enable();
    }


    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
            JumpEvent?.Invoke(true);
        else if (context.canceled)
            JumpEvent?.Invoke(false);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        MoveEvent?.Invoke(context.ReadValue<Vector2>());
    }

    public void OnPrimaryAttack(InputAction.CallbackContext context)
    {
        if (context.performed)
            PrimaryAttackEvent?.Invoke(true);
        else if (context.canceled)
            PrimaryAttackEvent?.Invoke(false);
    }
    public void OnCancel(InputAction.CallbackContext context)
    {
        if (context.performed)
            CancelEvent?.Invoke(true);
        else if (context.canceled)
            CancelEvent?.Invoke(false);
    }
    public void OnRun(InputAction.CallbackContext context)
    {
        if (context.performed)
            RunEvent?.Invoke(true);
        else if (context.canceled)
            RunEvent?.Invoke(false);
    }
}
