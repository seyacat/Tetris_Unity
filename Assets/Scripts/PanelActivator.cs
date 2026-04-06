using UnityEngine;
using UnityEngine.Events;

public class PanelActivator : MonoBehaviour
{
    [SerializeField] public UnityEvent OnActivated;

    private void OnEnable()
    {
        OnActivated?.Invoke();
    }
}
