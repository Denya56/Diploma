using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Code Puzzle Event Channel", menuName = "Puzzles/Code Puzzle Event Channel")]
public class CodePuzzleEventChannel : ScriptableObject
{
    /// <summary>
    /// Code for 3 Levers: 3 1 2 <=> R B G
    /// </summary>
    [Tooltip("Current puzzle's solution. Order of code numbers sended by triggers")]
    [SerializeField] public int[] code;

    // Action to add code number to the player's solution
    public event Action<int> OnCodeNumEventAdd;
    // Action to remove code number to the player's solution
    public event Action<int> OnCodeNumEventRemove;
    // Action to check player's solution when all levers were triggered
    public event Action<bool> OnCheckCodeEvent;
    // Action to temporally active/deactive levers
    public event Action<bool> OnSettingLeversEvent;

    public void CodeNumEventAdd(int leverCode)
    {
        OnCodeNumEventAdd?.Invoke(leverCode);
    }

    public void CodeNumEventRemove(int leverCode)
    {
        OnCodeNumEventRemove?.Invoke(leverCode);
    }

    public void CheckCodeEvent(bool _isCorrect)
    {
        OnCheckCodeEvent?.Invoke(_isCorrect);
    }

    public void SetLeversEvent(bool _isReady)
    {
        OnSettingLeversEvent?.Invoke(_isReady);
    }
}
