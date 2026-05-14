using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Diagnostics;
using System.IO;
using System.Collections;
using UnityEngine.Networking;

public class PauseMenuManager : MonoBehaviour
{
    public static PauseMenuManager Instance { get; private set; }

    [Header("Canvas References")]
    [SerializeField] Canvas pauseMenuCanvas;

    [Header("Gameplay UI")]
    [SerializeField] Canvas gameplayUICanvas;

    [Header("Text References")]
    [SerializeField] TextMeshProUGUI pauseText;

    [Header("Button References")]
    [SerializeField] Button resumeButton;
    [SerializeField] TextMeshProUGUI resumeButtonText;
    [SerializeField] Button quitButton;
    [SerializeField] Button creditsButton;

    [Header("Audio")]
    [SerializeField] AudioSource selectSFX;
    [SerializeField] AudioSource scrollSFX;
    [SerializeField] AudioSource backSFX;

    [Header("Credits File Path")]
    [SerializeField] string creditsFileName = "credits.txt";

    private bool gamePaused = false;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        AddButtonClickListener(resumeButton, ResumeGame);
        AddButtonClickListener(quitButton, QuitGame);
        AddButtonClickListener(creditsButton, OpenCreditsFile);

        AddButtonHoverSound(resumeButton);
        AddButtonHoverSound(quitButton);
        AddButtonHoverSound(creditsButton);

        CopyCreditsFileToPersistentPath();

        if (pauseMenuCanvas != null)
            pauseMenuCanvas.gameObject.SetActive(false);

        ShowGameplayUI(true);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!gamePaused)
                PauseGame();
            else
                ResumeGame();
        }
    }

    private void AddButtonClickListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;

        button.onClick.AddListener(() => PlaySelectSFX());
        button.onClick.AddListener(action);
    }

    private void AddButtonHoverSound(Button button)
    {
        if (button == null) return;

        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();

        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry pointerEnter = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };

        pointerEnter.callback.AddListener((eventData) => PlayScrollSFX());
        trigger.triggers.Add(pointerEnter);
    }

    void PlayScrollSFX()
    {
        if (scrollSFX != null)
            scrollSFX.Play();
    }

    void PlaySelectSFX()
    {
        if (selectSFX != null)
            selectSFX.Play();
    }

    void PlayBackSFX()
    {
        if (backSFX != null)
            backSFX.Play();
    }

    void PauseGame()
    {
        gamePaused = true;
        Time.timeScale = 0f;

        if (pauseText != null)
            pauseText.text = "PAUSED";

        if (pauseMenuCanvas != null)
            pauseMenuCanvas.gameObject.SetActive(true);

        ShowGameplayUI(false);

        if (resumeButtonText != null)
            resumeButtonText.text = "RESUME";

        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveAllListeners();
            resumeButton.onClick.AddListener(() => PlaySelectSFX());
            resumeButton.onClick.AddListener(ResumeGame);
        }

        PlayBackSFX();
    }

    void ResumeGame()
    {
        PlaySelectSFX();
        StartCoroutine(DelayedResume());
    }

    IEnumerator DelayedResume()
    {
        yield return new WaitForSecondsRealtime(0.2f);

        gamePaused = false;
        Time.timeScale = 1f;

        if (pauseMenuCanvas != null)
            pauseMenuCanvas.gameObject.SetActive(false);

        ShowGameplayUI(true);
    }

    public void OpenDeathMenu(string message = "GAME OVER")
    {
        gamePaused = true;
        Time.timeScale = 0f;

        if (pauseText != null)
            pauseText.text = message;

        if (pauseMenuCanvas != null)
            pauseMenuCanvas.gameObject.SetActive(true);

        ShowGameplayUI(false);

        if (resumeButtonText != null)
            resumeButtonText.text = "RESTART";

        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveAllListeners();
            resumeButton.onClick.AddListener(() => PlaySelectSFX());
            resumeButton.onClick.AddListener(RestartGame);
        }

        PlayBackSFX();
    }

    void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void ShowGameplayUI(bool show)
    {
        if (gameplayUICanvas == null)
            return;

        gameplayUICanvas.gameObject.SetActive(show);
    }

    void QuitGame()
    {
        Time.timeScale = 1f;

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void CopyCreditsFileToPersistentPath()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return;
#else
        string sourcePath = Path.Combine(Application.streamingAssetsPath, creditsFileName);
        string destinationPath = Path.Combine(Application.persistentDataPath, creditsFileName);

        if (!File.Exists(destinationPath) && File.Exists(sourcePath))
            File.Copy(sourcePath, destinationPath);
#endif
    }

    void OpenCreditsFile()
    {
#if UNITY_WEBGL
        string filePath = Path.Combine(Application.streamingAssetsPath, creditsFileName);
        Application.OpenURL(filePath);
#else
        string filePath = Path.Combine(Application.persistentDataPath, creditsFileName);

        if (File.Exists(filePath))
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    IEnumerator OpenCreditsFileWebGL()
    {
        string fileUrl = Application.streamingAssetsPath.TrimEnd('/') + "/" + creditsFileName;

        using (UnityWebRequest request = UnityWebRequest.Get(fileUrl))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogWarning($"Unable to load credits from {fileUrl}: {request.error}");
                yield break;
            }

            string text = request.downloadHandler.text;
            string encoded = UnityWebRequest.EscapeURL(text).Replace("+", "%20");
            Application.OpenURL("data:text/plain;charset=utf-8," + encoded);
        }
    }
#endif
}