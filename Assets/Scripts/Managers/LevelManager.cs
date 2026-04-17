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
		if (Input.GetKeyDown(KeyCode.R))
		{
			RestartLevel();
		}

		if (isLevelComplete && Input.GetKeyDown(KeyCode.N))
		{
			LoadNextLevel();
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
			Debug.Log("No more levels. You escaped. (Returning to menu not yet implemented.)");
		}
	}
}