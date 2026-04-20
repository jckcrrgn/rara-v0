using UnityEngine;

public class AudioManager : MonoBehaviour
{
	public static AudioManager Instance { get; private set; }

	[Header("Pool")]
	[SerializeField] private int poolSize = 6;
	[SerializeField] private AudioSource sourcePrefab; // optional — leave null to auto-create

	private AudioSource[] pool;
	private int nextIndex = 0;

	void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;
		DontDestroyOnLoad(gameObject);

		pool = new AudioSource[poolSize];
		for (int i = 0; i < poolSize; i++)
		{
			GameObject go = new GameObject($"SFXSource_{i}");
			go.transform.SetParent(transform);
			AudioSource src = go.AddComponent<AudioSource>();
			src.playOnAwake = false;
			src.spatialBlend = 0f; // 2D by default; override per-call if you want positional later
			pool[i] = src;
		}
	}

	public void PlaySFX(AudioClip clip, float volume = 1f)
	{
		if (clip == null) return;

		AudioSource src = pool[nextIndex];
		nextIndex = (nextIndex + 1) % pool.Length;

		src.clip = clip;
		src.volume = volume;
		src.Play();
	}
}