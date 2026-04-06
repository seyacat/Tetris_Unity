using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PanelManager : MonoBehaviour
{
    [SerializeField] private CanvasGroup[] panels;
    private int currentPanelIndex = 0;
    private bool isTransitioning = false;
    private const float FADE_DURATION = 1f;

    private void Start()
    {
        InitializePanels();
    }

    private void InitializePanels()
    {
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] == null)
            {
                Debug.LogError($"Panel at index {i} is null!");
                continue;
            }

            if (i == 0)
            {
                panels[i].alpha = 1f;
                panels[i].interactable = true;
                panels[i].blocksRaycasts = true;
            }
            else
            {
                panels[i].alpha = 0f;
                panels[i].interactable = false;
                panels[i].blocksRaycasts = false;
            }
        }
    }

    public void Next()
    {
        if (isTransitioning || panels.Length == 0)
            return;

        int nextPanelIndex = (currentPanelIndex + 1) % panels.Length;
        StartCoroutine(TransitionPanels(currentPanelIndex, nextPanelIndex));
    }

    private IEnumerator TransitionPanels(int fromIndex, int toIndex)
    {
        isTransitioning = true;

        CanvasGroup fromPanel = panels[fromIndex];
        CanvasGroup toPanel = panels[toIndex];

        // Disable interaction for both panels during transition
        fromPanel.interactable = false;
        fromPanel.blocksRaycasts = false;
        toPanel.interactable = false;
        toPanel.blocksRaycasts = false;

        // Activate the incoming panel before fading in
        toPanel.gameObject.SetActive(true);

        float elapsedTime = 0f;

        // Fade out current panel and fade in next panel simultaneously
        while (elapsedTime < FADE_DURATION)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / FADE_DURATION;

            fromPanel.alpha = Mathf.Lerp(1f, 0f, progress);
            toPanel.alpha = Mathf.Lerp(0f, 1f, progress);

            yield return null;
        }

        // Ensure final state
        fromPanel.alpha = 0f;
        toPanel.alpha = 1f;

        // Deactivate the outgoing panel when it reaches alpha 0
        fromPanel.gameObject.SetActive(false);

        // Enable interaction only for the new panel at 100%
        toPanel.interactable = true;
        toPanel.blocksRaycasts = true;

        currentPanelIndex = toIndex;
        isTransitioning = false;
    }

    public int GetCurrentPanelIndex()
    {
        return currentPanelIndex;
    }
}
