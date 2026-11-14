// QuitOnEscape.cs — Day 219. Esc twice quits the build.
//
// First Esc shows "Esc again to quit" at the top of the screen for confirmWindow seconds;
// a second Esc inside that window quits. One stray press never ends a run. Nothing pauses.
//
// Installs itself on the first scene load (no scene object, nothing to wire) and lives
// across reloads. Keyboard only: on a pad, Start restarts and the card is on Back.
// Off in WebGL builds: Application.Quit does nothing in a browser and Esc there already
// leaves fullscreen. In the editor the second Esc stops Play mode.

using UnityEngine;

public class QuitOnEscape : MonoBehaviour
{
	const float confirmWindow = 2f;
	const float uiScale = 2f;   // same as BondStruggleHUD's default
	const string prompt = "Esc again to quit";

	static QuitOnEscape instance;
	float armedUntil = -1f;
	GUIStyle style;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	static void Install()
	{
#if !UNITY_WEBGL
		if (instance != null) return;
		var go = new GameObject("QuitOnEscape");
		DontDestroyOnLoad(go);
		instance = go.AddComponent<QuitOnEscape>();
#endif
	}

	bool Armed => Time.unscaledTime < armedUntil;

	void Update()
	{
		if (!Input.GetKeyDown(KeyCode.Escape)) return;
		if (Armed) Quit();
		else armedUntil = Time.unscaledTime + confirmWindow;
	}

	void Quit()
	{
		armedUntil = -1f;
#if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
#else
		Application.Quit();
#endif
	}

	void OnGUI()
	{
		if (!Armed) return;
		GUI.depth = -1000;   // over the HUD and the end card's OnGUI, if any
		GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
		float sw = Screen.width / uiScale;
		if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, wordWrap = false };
		var prev = GUI.color;
		GUI.color = new Color(1f, 1f, 1f, 0.85f);
		GUI.Label(new Rect(0f, 12f, sw, 22f), prompt, style);
		GUI.color = prev;
		GUI.matrix = Matrix4x4.identity;
	}
}
