// BondStruggleHUD.cs — Day 202. TEMPORARY SCAFFOLDING, same status as BondMeterUI.
// The StruggleProto greybox readout, pointed at the real BondStruggle + GuardController.
// Replace with diegetic feedback (bonds on the model, the guard's head turn) — see the
// Day 201 note: "connect the rhythm to a visual presentation of the bonds."
//
// Drop on any always-active scene object. Finds Cassie's BondStruggle and the guard.
// Tab / pad Back toggles the controls card.

using UnityEngine;

public class BondStruggleHUD : MonoBehaviour
{
	[SerializeField] private BondStruggle struggle;
	[Range(1, 4)] [SerializeField] private float uiScale = 2f;
	[Tooltip("Show the controls card on start (cold playtest).")]
	[SerializeField] private bool showControlsCard = true;

	void Start()
	{
		if (struggle == null) struggle = FindFirstObjectByType<BondStruggle>();
		if (struggle == null) Debug.LogWarning("[BondStruggleHUD] No BondStruggle in scene.");
	}

	void Update()
	{
		bool back = false;
#if ENABLE_INPUT_SYSTEM
		var gp = UnityEngine.InputSystem.Gamepad.current;
		back = gp != null && gp.selectButton.wasPressedThisFrame;
#else
		back = Input.GetKeyDown(KeyCode.JoystickButton6);
#endif
		if (Input.GetKeyDown(KeyCode.Tab) || back) showControlsCard = !showControlsCard;
	}

	void OnGUI()
	{
		if (struggle == null) return;
		GuardController guard = GuardController.Instance;

		GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
		float sw = Screen.width / uiScale;
		var prev = GUI.color;

		// Day 202 rev: no pulse bar. The beat lives in Cassie (CassieStruggleDriver
		// sways on it) — read her, not the HUD.
		string padStatus = RaraInput.Status();
		if (padStatus.Length > 0)
		{
			GUI.color = Color.yellow;
			GUI.Label(new Rect(20, 22, 520, 22), padStatus);
			GUI.color = prev;
		}

		// --- bonds ---
		float y = 50;
		for (int i = 0; i < struggle.bonds.Length; i++)
		{
			var b = struggle.bonds[i];
			if (!b.active) continue;   // reserve bonds (elbows before the first catch)
			string mark = i == struggle.Selected ? "> " : "  ";
			string bar = new string('|', Mathf.RoundToInt(b.progress * 30));
			string state = b.progress >= 1f ? "  FREE" : "";
			string tight = b.tightness > 1f ? $"  {1f / b.tightness:P0} per pull" : "";
			string untie = struggle.CanUntie(i) ? "  [untie]" : "";
			GUI.Label(new Rect(20, y, 600, 22), $"{mark}{b.name,-8} {bar}{state}{tight}{untie}");
			y += 22;
		}

		// --- hands + knot ---
		string hands;
		if (!struggle.HandsFree) hands = "bound";
		else if (struggle.IsHidingHands) hands = "FREE — hiding...";
		else if (struggle.HandsOut) hands = "FREE — OUT";
		else hands = "FREE — hidden";
		if (struggle.HandsOut) GUI.color = Color.red;
		GUI.Label(new Rect(20, y + 6, 500, 22), $"HANDS  {hands}{(struggle.MoveLayerHeld ? "    [MOVING CHAIR]" : "")}");
		GUI.color = prev;

		if (struggle.CanUntie(struggle.Selected))
		{
			var k = struggle.bonds[struggle.Selected].knot;
			string seq = "";
			for (int j = 0; j < k.Count; j++)
				seq += j == 0 ? $"[{BondStruggle.ArrowFor(k[j])}]" : $"  {BondStruggle.ArrowFor(k[j])}";
			GUI.Label(new Rect(20, y + 28, 500, 22), $"KNOT   {seq}");
		}

		// --- noise bar --- noise only calls him (never catches her). Yellow ticks:
		// call-out / comes in.
		float barX = 20f, barY = y + 58, barW = 300f;
		float noise = struggle.Noise;
		bool looking = guard != null && guard.Attention > 0f;
		GUI.Box(new Rect(barX, barY, barW, 18), "");
		if (noise > 0.01f) GUI.Box(new Rect(barX, barY, barW * noise, 18), "");
		GUI.Label(new Rect(barX + barW + 8, barY, 80, 20), "NOISE");
		if (guard != null)
		{
			GUI.color = new Color(1f, 0.85f, 0f, 0.8f);
			GUI.Box(new Rect(barX + barW * guard.GlanceNoise - 1, barY - 2, 2, 22), "");
			GUI.Box(new Rect(barX + barW * guard.InspectNoise - 1, barY - 2, 2, 22), "");
			GUI.color = prev;
		}

		// --- captor ---
		if (guard != null)
		{
			string captor = guard.CurrentState switch
			{
				GuardController.GuardState.Offstage => guard.IsAlerted ? "LISTENING" : "offstage",
				GuardController.GuardState.Approaching => looking ? "LOOKING IN..." : "footsteps...",
				GuardController.GuardState.AtDoor => "WATCHING",
				GuardController.GuardState.LeanIn => "IN YOUR FACE",
				GuardController.GuardState.Leaving => looking ? "turning away" : "walking off",
				GuardController.GuardState.Caught => "CAUGHT",
				GuardController.GuardState.Downed => "down",
				_ => "",
			};
			if (looking && struggle.HandsOut) GUI.color = Color.red;   // the only danger
			else if (looking || guard.IsAlerted) GUI.color = Color.yellow;
			GUI.Label(new Rect(20, barY + 30, 600, 22),
				$"CAPTOR  {captor}  (attention {guard.Attention:P0})   {guard.ListenStatus}");
			GUI.color = prev;
		}

		GUI.Label(new Rect(20, barY + 54, 600, 22), struggle.LastResult);
		if (struggle.LastBoost.Length > 0 && !struggle.HandsFree)
		{
			GUI.color = new Color(0.6f, 1f, 0.6f);
			GUI.Label(new Rect(340, barY + 54, 300, 22), $"BOOST  {struggle.LastBoost}");
			GUI.color = prev;
		}
		GUI.Label(new Rect(20, barY + 76, 500, 22), $"catches {struggle.Catches}/{struggle.maxCatches}");

		// --- controls card ---
		if (showControlsCard)
		{
			const float cw = 340f, ch = 250f;
			string card =
				"Get every bond free — or get the bottle.\n" +
				"Don't get caught.\n\n" +
				"Choose bond      L-stick  /  WASD\n" +
				"Struggle         RT  /  Space   (with her rhythm)\n" +
				"Back up to the table edge: wrists go faster\n" +
				"Untie            D-pad  /  IJKL   (hands free)\n" +
				"Hide hands       LB  /  G\n" +
				"Move chair       hold LT  /  Shift\n" +
				"Pick up          A  /  E\n" +
				"Strike           X  /  H\n\n" +
				"Back / Tab: hide this card";
			GUI.Box(new Rect(sw - cw - 20, 20, cw, ch), "");
			var cs = new GUIStyle(GUI.skin.label) { wordWrap = false };
			GUI.Label(new Rect(sw - cw - 10, 28, cw - 20, ch - 16), card, cs);
		}
	}
}
