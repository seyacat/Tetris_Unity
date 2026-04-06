using UnityEngine;
using UnityEngine.InputSystem;

public class QuitOnEscape : MonoBehaviour
{
    private void Update()
    {
        // Detectar si el teclado está conectado y se presionó Escape en este frame
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Debug.Log("Saliendo del juego...");
            Application.Quit();
            
            // Si estás probando dentro del Editor de Unity, esto detendrá el modo Play
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
