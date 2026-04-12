using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
	public static LevelManager Instance { get; private set; }

	[Header("Level State")]
	[SerializeField] private bool isLevelComplete = false;

	[Header("UI References")]
	[SerializeField] private GameObject levelCompleteUI;

	void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else
		{
			Destroy(gameObject);
		}
	}

	void Start()
	{
		if (levelCompleteUI != null)
		{
			levelCompleteUI.SetActive(false);
		}
	}

	void Update()
	{
		// Press R anytime to restart the current level
		if (Input.GetKeyDown(KeyCode.R))
		{
			RestartLevel();
		}
	}

	public void CompleteLevel()
	{
		if (isLevelComplete) return;

		isLevelComplete = true;
		Debug.Log("Level Complete!");

		if (levelCompleteUI != null)
		{
			levelCompleteUI.SetActive(true);
		}

		PlayerController player = FindFirstObjectByType<PlayerController>();
		if (player != null)
		{
			player.enabled = false;
		}
	}

	public void RestartLevel()
	{
		// Reloads the currently active scene, resetting everything
		SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
	}
}