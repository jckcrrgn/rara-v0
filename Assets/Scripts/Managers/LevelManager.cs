using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
	public static LevelManager Instance { get; private set; }

	[Header("Level State")]
	[SerializeField] private bool isLevelComplete = false;

	[Header("UI References")]
	[Tooltip("Shown on normal level complete. Text should read just 'LEVEL COMPLETE' " +
			 "(or similar) — no keypress hints; auto-advance carries the player.")]
	[SerializeField] private GameObject levelCompleteUI;
	[Tooltip("Shown instead of levelCompleteUI when this is the final scene in build settings. " +
			 "Optional — if null, levelCompleteUI is shown and auto-advance is suppressed.")]
	[SerializeField] private GameObject gameCompleteUI;

	[Header("Advance Behavior")]
	[Tooltip("If true, auto-loads next scene after autoAdvanceDelay. If false, waits for N key.")]
	[SerializeField] private bool autoAdvance = true;
	[Tooltip("Seconds the level-complete UI is shown before auto-advancing.")]
	[SerializeField] private float autoAdvanceDelay = 1.75f;

	[Header("Entry Mutter")]
	[Tooltip("Mutter line that fires on level start. Leave empty for no entry mutter. " +
		"TextArea so longer lines wrap nicely in the inspector. Per-level so each " +
		"scene's LevelManager owns its own opener.")]
	[TextArea(2, 4)]
	[SerializeField] private string entryMutter;

	private bool isFinalLevel;

	void Awake()
	{
		if (Instance == null) Instance = this;
		else Destroy(gameObject);
	}

	void Start()
	{
		if (levelCompleteUI != null) levelCompleteUI.SetActive(false);
		if (gameCompleteUI != null) gameCompleteUI.SetActive(false);

		// Final level = no scene after this one in build settings.
		int currentIndex = SceneManager.GetActiveScene().buildIndex;
		isFinalLevel = (currentIndex + 1 >= SceneManager.sceneCountInBuildSettings);

		if (!string.IsNullOrEmpty(entryMutter) && MutterSystem.Instance != null)
		{
			MutterSystem.Instance.Play(entryMutter);
		}
	}

	void Update()
	{
		if (Input.GetKeyDown(KeyCode.R))
		{
			RestartLevel();
		}

		// N still works as a manual fallback for autoAdvance==false levels.
		if (isLevelComplete && Input.GetKeyDown(KeyCode.N) && !isFinalLevel)
		{
			LoadNextLevel();
		}
	}

	public void CompleteLevel()
	{
		if (isLevelComplete) return;

		isLevelComplete = true;
		Debug.Log(isFinalLevel ? "Game Complete!" : "Level Complete!");

		// Pick which panel to show.
		GameObject panel = (isFinalLevel && gameCompleteUI != null) ? gameCompleteUI : levelCompleteUI;
		if (panel != null) panel.SetActive(true);

		PlayerController player = FindFirstObjectByType<PlayerController>();
		if (player != null) player.enabled = false;

		// Only auto-advance if we have somewhere to go.
		if (autoAdvance && !isFinalLevel)
		{
			StartCoroutine(AutoAdvanceRoutine());
		}
	}

	private IEnumerator AutoAdvanceRoutine()
	{
		yield return new WaitForSeconds(autoAdvanceDelay);
		if (isLevelComplete) LoadNextLevel();
	}

	public void RestartLevel()
	{
		SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
	}

	public void LoadNextLevel()
	{
		int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
		if (nextIndex < SceneManager.sceneCountInBuildSettings)
		{
			SceneManager.LoadScene(nextIndex);
		}
		else
		{
			Debug.Log("No more levels — already on final scene.");
		}
	}
}
