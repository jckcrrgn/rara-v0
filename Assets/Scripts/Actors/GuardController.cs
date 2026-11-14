using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Scripted guard actor for the Vertical Slice ("The Turnaround").
///
/// NOT patrol AI — this is a deterministic state machine keyed to a
/// recurring check-in clock. The guard cycles offstage and returns at
/// fixed intervals; each return follows the same beat sequence. The
/// player's job is to enter the Feign pose before the guard reaches the
/// door sightline.
///
/// STATE MACHINE
/// -------------
/// Offstage → Approaching → AtDoor → LeanIn → Downed (strike lands)
///                                          ↘ Leaving → (back to Offstage)
///                                 ↘ Caught (not feigning at inspection)
///
/// On a PASSED inspection the guard walks straight in to Cassie and gloats in
/// her face (LeanIn). He does this EVERY check-in — it's his habit, the sadist
/// who can't resist getting close. There is no player verb to summon him; the
/// approach is unconditional. On the routine (unarmed) check-ins this is pure
/// threat — she endures him up close, unable to act. The turnaround happens on
/// whichever check-in she is finally armed: the same smug lean-in he's done
/// every time, except this time her hands come around swinging. The escalation
/// lives in HER state, not his — which is the whole engine of the dramatic
/// irony. (This replaces the earlier lure/"Call Out" verb, cut Day 62: in a
/// one-guard scripted slice, a summon-the-guard verb was agency theater. It
/// belongs to the AI levels, parked in ideas.md.)
///
/// Downed is terminal — StrikeableGuard calls OnGuardDowned() which stops all
/// coroutines and sets the state. No further cycle runs after Downed.
///
/// CLOCK
/// -----
/// The offstage phase runs for `offstageDuration` seconds. When it expires
/// the approach phase begins automatically. This is the check-in interval —
/// tune it so the player has enough time to make meaningful escape progress
/// between visits without the rhythm feeling absent.
///
/// FEIGN WINDOW
/// ------------
/// Opens when the guard enters Approaching (approach audio starts).
/// Closes when he reaches AtDoor. The player must press G during this window.
/// At AtDoor, IsFeigning is sampled once — pass → walk in (LeanIn); fail → Caught.
///
/// CLOSE-IN (the gloat)
/// --------------------
/// After passing inspection the guard enters LeanIn and physically walks to
/// Cassie's position, then taunts her up close. This is the strike window. He
/// does it on every passed inspection, armed or not. If she doesn't strike (no
/// weapon, or the player hesitates), he straightens after leanInDuration and
/// leaves — the loop continues and the next check-in is another chance.
///
/// His movement is SPEED-based (guardMoveSpeed, m/s), not duration-based: the
/// distance from the door to wherever Cassie sits varies, so a fixed duration
/// would make him crawl when she's near the door and lunge when she's far. A
/// constant speed reads as the same deliberate walk regardless of distance.
/// (The door APPROACH, by contrast, stays duration-based — that duration IS the
/// feign window, a gameplay clock, not a movement to be normalized.)
///
/// CAUGHT BRANCH
/// -------------
/// Not feigning at inspection → Caught state → re-cinch sequence (spec §10
/// default: re-cinch/escalate). Reuses the FailureLoopController fade pattern
/// conceptually, but lives here as a lighter version — the VS doesn't carry
/// the full failure loop apparatus (no Chair B, no LevelTimer, no lamp).
/// Re-cinch: fade to black → bond escalation → mutter → fade in.
///
/// MOVEMENT
/// --------
/// The guard's visible body (guardBody) physically lerps between positions:
/// offstage (out of sight) and door (inspection/gloat) are fixed anchors; the
/// lean-in target is computed live from Cassie's position (he leans into
/// wherever she actually is, stopping leanInStandoff short of her). Movement
/// rides the existing phase durations, so timing is unchanged. It is purely
/// presentational — strike validity is the LeanIn STATE, never the body's
/// distance from the player (see StrikeableGuard and PlayerController). If
/// anchors aren't wired, movement is skipped and the slice still runs on the
/// abstract state machine alone.
///
/// HEIGHT (Day 171): with lockBodyHeight on, every move and snap keeps the
/// guard at his authored scene Y (captured at Start). Anchor and waypoint Y
/// is ignored, so a mis-placed anchor can no longer sink or float him.
///
/// SINGLETON
/// ---------
/// Same pattern as LevelTimer and MutterSystem. Wire one instance per VS scene.
/// </summary>
public class GuardController : MonoBehaviour
{
	public static GuardController Instance { get; private set; }

	// -------------------------------------------------------------------------
	// State
	// -------------------------------------------------------------------------

	public enum GuardState
	{
		Offstage,
		Approaching,
		AtDoor,
		Leaving,
		Caught,
		LeanIn,
		Downed,
		Checking,   // Day 211: walked in on suspicion, examining her bonds
	}

	public GuardState CurrentState { get; private set; } = GuardState.Offstage;

	/// <summary>
	/// Fires on every state transition. Argument is the new state.
	/// UI and other systems subscribe to react to guard presence.
	/// </summary>
	public System.Action<GuardState> OnStateChanged;

	// -------------------------------------------------------------------------
	// Inspector — Timing
	// -------------------------------------------------------------------------

	[Header("Timing")]
	[Tooltip("How long the guard stays offstage between check-ins, in seconds. " +
		"This is the player's free-work window — enough to make meaningful escape " +
		"progress, short enough that the rhythm stays present. 45–60s is a " +
		"reasonable starting range; tune via playtesting.")]
	[SerializeField] private float offstageDuration = 50f;

	[Tooltip("How long the approach phase lasts — the time between 'footsteps " +
		"start' and 'guard is at the door.' This is the feign window duration. " +
		"Long enough to react even on first exposure (4–6s), short enough to " +
		"create real tension. The approach audio clip length should match or " +
		"be slightly shorter than this value.")]
	[SerializeField] private float approachDuration = 5f;

	[Tooltip("How long the guard stays at the door during inspection before the " +
		"outcome fires (pass → walk in / LeanIn, fail → Caught). A beat of held " +
		"tension " +
		"before the branch. 1–2s is enough to let the pause land.")]
	[SerializeField] private float inspectionHoldDuration = 1.5f;

	[Tooltip("How long the guard stays in LeanIn after he's finished closing the " +
		"distance — the strike window. His close-up gloat plays at the start of it. " +
		"If the player doesn't strike within this window (no weapon, or hesitation), " +
		"he straightens up and leaves; the next check-in is another chance. No hard " +
		"QTE — the window closes naturally when the timer expires. 4–6s gives enough " +
		"time to act without feeling infinite. Note: total time in-close = the " +
		"speed-based walk to Cassie PLUS this window.")]
	[SerializeField] private float leanInDuration = 5f;

	[Tooltip("Fallback duration for the leaving phase, used ONLY when no guardBody " +
		"or offstageAnchor is wired (abstract-testing mode). When anchors are " +
		"present the recede is speed-based (guardMoveSpeed). Keeps the abstract " +
		"state machine paced before the movement anchors are placed.")]
	[SerializeField] private float leavingDuration = 3f;

	// -------------------------------------------------------------------------
	// Inspector — Movement
	// -------------------------------------------------------------------------

	[Header("Movement")]
	[Tooltip("The guard's visible body — the Transform that physically moves " +
		"between anchors (in the graybox, the guard cube; later, the character " +
		"model root). If left unwired, falls back to the StrikeableGuard's " +
		"transform found in the scene. If neither resolves, movement is skipped " +
		"and phases just wait out their durations — the slice still runs.")]
	[SerializeField] private Transform guardBody;

	[Tooltip("Where the guard sits while Offstage — out of the player's sightline, " +
		"down the hallway past the door. He snaps here at the start of each " +
		"Offstage phase and lerps back here when Leaving.")]
	[SerializeField] private Transform offstageAnchor;

	[Tooltip("The doorway / inspection position. The guard lerps here during " +
		"Approaching and holds here through AtDoor; on a pass he walks on from here " +
		"to Cassie for the close-in.")]
	[SerializeField] private Transform doorAnchor;

	[Tooltip("How close the guard stops in FRONT of Cassie when he leans in, in " +
		"metres. He targets her live position (offset back toward the door so he " +
		"doesn't overlap her), not a fixed spot — he leans into wherever she " +
		"actually is. ~0.7–0.9 reads as in-her-face without clipping. Strike " +
		"validity is the LeanIn state, so this is purely how the lean reads.")]
	[SerializeField] private float leanInStandoff = 0.8f;

	[Tooltip("How fast the guard's body moves, in metres per second, for the " +
		"variable-distance walks: the lean-in to Cassie and the recede back " +
		"offstage. Speed-based (not duration-based) so the walk reads the same " +
		"whether she's sitting near the door or across the room. ~1.2–1.8 reads " +
		"as a deliberate, unhurried walk. For a seamless pace, set this near " +
		"(offstage→door distance / approachDuration) so the close-in matches the " +
		"door approach; set it deliberately different if you want the close-in to " +
		"feel like a distinct change of intent. The door approach itself is NOT " +
		"governed by this — its speed is set by approachDuration (the feign window).")]
	[SerializeField] private float guardMoveSpeed = 1.5f;

	[Tooltip("Ordered intermediate waypoints the guard walks THROUGH on his way " +
		"in to Cassie (and in reverse when leaving), routing around walls / " +
		"furniture that a straight door→Cassie lerp would clip through. " +
		"Hand-placed — this is a scripted actor on a fixed floorplan, not a " +
		"NavMesh agent. Leave empty for a clear straight shot (the guard then " +
		"walks door→Cassie directly, as before). The FINAL leg (last waypoint → " +
		"Cassie's live lean-in point) is still computed live, so place the " +
		"waypoint(s) in the OPEN part of the room — south of the bath partition — " +
		"and let that last leg cover wherever she actually hopped to. In this " +
		"room a single waypoint clears it: the partition is the only interior " +
		"obstacle, so one point south of it gives full line-of-sight to the rest.")]
	[SerializeField] private Transform[] leanInWaypoints;

	[Tooltip("How fast the guard turns to face where he's walking, in degrees " +
		"per second. He turns WHILE he walks, so this only shapes how the turn " +
		"reads, never the walk timing. ~360 reads as a deliberate pivot. 0 = snap.")]
	[SerializeField] private float turnSpeed = 360f;

	[Tooltip("Keep the guard at his authored scene height on every move, ignoring " +
		"anchor/waypoint Y. Off = anchors' Y drives his height (old behaviour).")]
	[SerializeField] private bool lockBodyHeight = true;

	// -------------------------------------------------------------------------
	// Inspector — Lean Pose
	// -------------------------------------------------------------------------

	[Header("Lean Pose")]
	[Tooltip("The upright guard model (Guard_D133). Active everywhere except the " +
		"in-her-face gloat.")]
	[SerializeField] private GameObject standingModel;

	[Tooltip("The leaning guard model (Guard_D133_Lean). Swapped in once he has " +
		"closed the distance and turned to face Cassie; swapped back out the " +
		"moment he starts Leaving. Leave unwired to skip the swap entirely.")]
	[SerializeField] private GameObject leanModel;

	// -------------------------------------------------------------------------
	// Inspector — Audio
	// -------------------------------------------------------------------------

	[Header("Audio")]
	[Tooltip("Footsteps approaching — plays at the start of the Approaching state. " +
		"Length should be at or under approachDuration. This is the feign-window " +
		"telegraph: when the player hears this, they know to press G.")]
	[SerializeField] private AudioClip approachFootstepsClip;

	[Tooltip("Footsteps receding — plays at the start of the Leaving state.")]
	[SerializeField] private AudioClip leaveFootstepsClip;

	[Tooltip("Volume for all guard footstep audio. Diegetic — comes from offstage " +
		"so should be audible but not overpowering.")]
	[Range(0f, 1f)]
	[SerializeField] private float footstepsVolume = 0.85f;

	// -------------------------------------------------------------------------
	// Inspector — Mutter Content
	// -------------------------------------------------------------------------

	[Header("Mutter Content — Close Gloat (Guard)")]
	[Tooltip("Guard's taunt lines, spoken up close once he's leaned in. Played in " +
		"order, index 0 = first check-in, clamps to the last entry once exhausted. " +
		"Speaker: Guard. NOTE: he says the same kind of thing every check-in — he " +
		"can't perceive that she's armed, so there is deliberately no special " +
		"'climactic' line. The difference on the turnaround is entirely on her side. " +
		"Author these as in-her-face taunts, not door-distance glances.")]
	[TextArea(2, 4)]
	[SerializeField] private string[] routineGloatLines =
	{
		"Comfortable? Good. Don't go anywhere.",
		"Still with us? Wonderful.",
	};

	[Header("Mutter Content — Close Reaction (Cassie)")]
	[Tooltip("Cassie's internal reaction lines, queued behind each routine gloat. " +
		"Index matches routineGloatLines. Speaker: Cassie.")]
	[TextArea(2, 4)]
	[SerializeField] private string[] routineReactionLines =
	{
		"Keep walking.",
		"That's it. Trust the knots.",
	};

	[Header("Mutter Content — Caught")]
	[Tooltip("Guard's line when he catches Cassie not feigning. Plays over the " +
		"cut-to-black. Speaker: Guard.")]
	[TextArea(2, 4)]
	[SerializeField] private string caughtGuardLine =
		"I knew it. Let's try that again — tighter this time.";

	[Tooltip("Cassie's reaction after being caught and re-cinched. Plays after " +
		"fade-in. Speaker: Cassie.")]
	[TextArea(2, 4)]
	[SerializeField] private string caughtCassieReaction =
		"Back to square one. Think, Cassie.";

	// -------------------------------------------------------------------------
	// Inspector — Caught Re-Cinch
	// -------------------------------------------------------------------------

	[Header("Caught — Re-Cinch")]
	[Tooltip("Full-screen black CanvasGroup. Same pattern as FailureLoopController. " +
		"Alpha 0 = gameplay visible; alpha 1 = fully black. Required for the " +
		"caught sequence to read correctly — without it the state reset is instant " +
		"and jarring.")]
	[SerializeField] private CanvasGroup fadeOverlay;

	[Tooltip("Fade duration for the cut-to-black and fade-in on a caught event.")]
	[SerializeField] private float caughtFadeDuration = 0.5f;

	[Tooltip("Ordered SFX sequence played during the caught blackout — rope cinch, " +
		"Cassie grunt, etc. Same pattern as FailureLoopController.rebindSfxSequence.")]
	[SerializeField] private AudioClip[] caughtRebindSfx;

	[Tooltip("Volume for caught rebind SFX.")]
	[Range(0f, 1f)]
	[SerializeField] private float caughtRebindSfxVolume = 1f;

	[Tooltip("The bond added when Cassie is caught. Applied once per catch. " +
		"Spec default: escalate (Ankles first, then Elbows, etc.) — but for " +
		"v1 we apply a single fixed bond rather than a full escalation ladder. " +
		"Set to None (0) to skip bond escalation on catch (soft re-cinch).")]
	[SerializeField] private BoundLimbs caughtBondToAdd = BoundLimbs.None;

	[Tooltip("Respawn transform for Cassie after a caught reset. If null, " +
		"position is not changed — useful during early testing.")]
	[SerializeField] private Transform caughtRespawnPoint;

	[Tooltip("UnityEvent fired during the caught state mutation (same role as " +
		"FailureLoopController.onStateReset). Wire scene-specific resets here.")]
	[SerializeField] private UnityEvent onCaughtReset;

	// -------------------------------------------------------------------------
	// Inspector — LeanIn Scene Hook
	// -------------------------------------------------------------------------

	[Header("LeanIn — Scene Hook")]
	[Tooltip("UnityEvent fired at the start of the close-in (LeanIn) — the moment " +
		"the guard commits to walking in on Cassie. Wire scene-specific responses " +
		"here, e.g. a camera nudge, or showing a contextual 'Strike (H)' hint card " +
		"while she's armed and he's in range.")]
	[SerializeField] private UnityEngine.Events.UnityEvent onLeanInEntered;

	// -------------------------------------------------------------------------
	// Inspector — Listening (Day 202, BondStruggle levels only)
	// -------------------------------------------------------------------------
	// The StruggleProto captor, mapped onto the check-in loop. Ignored entirely when
	// the player has no BondStruggle (legacy evidence-at-the-door inspection runs).
	//
	//   Day 202 rev: noise never catches her. Noise decides how often he looks.
	//   Day 211 rev: three states — not paying attention / watching / checking.
	//   While he's WATCHING: mashing amuses him (taunt, he lingers, no catch). On-beat
	//   pulls are squirming. MISSES are the tell: each one builds suspicion, a tool miss
	//   (sawing the table edge) builds a lot. Full = he walks in and CHECKS her bonds.
	//   Evidence decides: a bond past checkEvidence = light catch; otherwise "Still
	//   tight." and he cinches it. Free hands seen = instant heavy catch, as before.
	//
	//   Offstage            "on his phone". Noise draws him:
	//                         noise >= glanceNoise  -> he CALLS OUT (alert window)
	//                         noise >= inspectNoise, or glanceNoise again while
	//                         alerted               -> he comes NOW (hurried approach)
	//                       The sound that drew him is spent.
	//   Approaching         footsteps = the telegraph. Attention 0 until the last
	//                       turnTime seconds, then ramps to 1 (the turn is the tell).
	//   AtDoor / LeanIn     watching, attention 1. Struggle on the beat all she likes.
	//                       Misses build suspicion, only within missNoticeRange (Day 214:
	//                       not from the door). Free hands out = caught.
	//   Checking            walks in, examines (Day 211). Attention 1.
	//   Leaving             attention ramps 1 -> 0 over turnAwayTime, then he's
	//                       listening again on his way out (and can turn back).
	//   Rule carried from the proto: randomise WHEN he looks, never HOW FAST he turns.

	[Header("Listening — BondStruggle (Day 202)")]
	[Tooltip("If y > 0, each offstage wait is random in [x, y] seconds and offstageDuration " +
		"is ignored. Proto ran 3–10 s in-room; offstage plus a walk wants longer. Try 10–22.")]
	[SerializeField] private Vector2 offstageRange = Vector2.zero;
	[Tooltip("Caught: put Cassie back exactly where she started the level (captured at " +
		"Start), ignoring caughtRespawnPoint. On by default for BondStruggle levels.")]
	[SerializeField] private bool respawnAtStartPosition = true;
	[Tooltip("Noise that makes him call out while offstage.")]
	[SerializeField] private float glanceNoise = 0.3f;
	[Tooltip("Noise that brings him straight in. Also: glanceNoise again during the alert window.")]
	[SerializeField] private float inspectNoise = 0.6f;
	[Tooltip("How long a call-out leaves him listening hard. More glance-level noise in this window brings him in.")]
	[SerializeField] private float alertWindow = 1.6f;
	[Tooltip("Attention ramp at the end of a normal approach — the turn. Fixed on purpose.")]
	[SerializeField] private float turnTime = 1.4f;
	[Tooltip("Approach length when he heard something. Shorter than approachDuration: noise costs you warning time.")]
	[SerializeField] private float heardApproachDuration = 2.5f;
	[Tooltip("Attention ramp at the end of a hurried approach. Faster — he's reacting, not idly checking.")]
	[SerializeField] private float heardTurnTime = 0.8f;
	[Tooltip("Attention ramp down when he starts leaving.")]
	[SerializeField] private float turnAwayTime = 0.8f;
	[Tooltip("Attention at which he sees free hands that are out (or a bottle she hasn't hidden).")]
	[Range(0f, 1f)]
	[SerializeField] private float seenAttention = 0.75f;
	[Tooltip("Optional SFX for a call-out. The line below plays as a background (non-blocking) mutter.")]
	[SerializeField] private AudioClip calloutClip;
	[SerializeField] private string[] calloutLines = { "Hm?", "You say something?", "Quiet in there." };

	[Header("Watching — suspicion and bond check (Day 211)")]
	[Tooltip("Suspicion from one missed pull while he's watching. 1 = he checks.")]
	[SerializeField] private float suspicionPerMiss = 0.35f;
	[Tooltip("Suspicion from a missed pull with a tool engaged (table edge, held blade).")]
	[SerializeField] private float suspicionPerToolMiss = 0.7f;
	[Tooltip("Day 214: how close (metres, flat) he has to be to her to read a miss. From the " +
		"door (~6 m) a miss looks like any other squirm; on the walk in and at the lean-in " +
		"(leanInStandoff 1 m) he can tell. Free hands and mashing still read from the door.")]
	[SerializeField] private float missNoticeRange = 2.5f;
	[Tooltip("Suspicion at which he says the tell line, once per visit.")]
	[SerializeField] private float suspicionTellAt = 0.5f;
	[SerializeField] private string[] suspicionTellLines = { "...Hm.", "What are you doing back there?" };
	[Tooltip("Day 214: chance he notices something she set down off the table. One roll per " +
		"visit, made when he first comes within strayNoticeRange of it while watching.")]
	[Range(0f, 1f)]
	[SerializeField] private float strayNoticeChance = 0.4f;
	[Tooltip("Day 214: how close (metres, flat) his body has to get to it before he can notice. " +
		"From the door he can't; on the walk in he can. Tune against the lean-in point.")]
	[SerializeField] private float strayNoticeRange = 2f;
	[Tooltip("Suspicion when he notices it. Above the tell line on its own; one miss after it = he checks.")]
	[SerializeField] private float suspicionPerStray = 0.65f;
	[SerializeField] private string[] strayNoticeLines = { "...Was that bottle on the floor before?", "Where'd that come from?" };
	[Tooltip("Bond progress he'll notice when he checks. Any bond at or past this = caught (light).")]
	[Range(0f, 1f)]
	[SerializeField] private float checkEvidence = 0.5f;
	[Tooltip("How long he examines her bonds once he's at her.")]
	[SerializeField] private float checkDuration = 1.2f;
	[Tooltip("Nothing found: he tugs the most loosened bond back by this much.")]
	[SerializeField] private float cinchSetback = 0.15f;
	[SerializeField] private string[] checkClearLines = { "Still tight.", "Not going anywhere." };
	[Tooltip("Guard line when the check finds evidence. Plays over black.")]
	[TextArea(2, 4)]
	[SerializeField] private string caughtCheckGuardLine = "Thought so. Let's tighten these.";

	[Header("Watching — mashing amuses him (Day 211)")]
	[SerializeField] private string[] tauntLines =
		{ "Putting on a show? Nobody can hear you.", "Go on. Wear yourself out.", "Nobody's coming." };
	[Tooltip("Min seconds between taunts.")]
	[SerializeField] private float tauntCooldown = 4f;
	[Tooltip("Each taunt keeps him watching this much longer...")]
	[SerializeField] private float tauntLinger = 2f;
	[Tooltip("...up to this much extra per visit.")]
	[SerializeField] private float maxLingerPerVisit = 6f;

	[Header("Listening — Caught lines (Day 202)")]
	[TextArea(2, 4)]
	[SerializeField] private string caughtSeenGuardLine = "Busy hands. Let's fix that.";
	[Tooltip("Cassie's reaction when the catch tied on a NEW bond (elbows on the first catch). " +
		"Falls back to caughtCassieReaction when empty or nothing was added.")]
	[TextArea(2, 4)]
	[SerializeField] private string escalationCassieReaction = "My ELBOWS? Really?";
	[Tooltip("Day 218: added to his line on the catch before the last one, so the player hears " +
		"the stakes from him (the HUD no longer shows 'caught 2 of 3'). Empty = no warning.")]
	[TextArea(2, 4)]
	[SerializeField] private string lastWarningGuardLine = "Once more and you're coming with me.";
	[Tooltip("The last catch. Plays over black, then the scene reloads.")]
	[TextArea(2, 4)]
	[SerializeField] private string takenGuardLine = "Enough. You're coming with me.";
	[SerializeField] private float takenHoldTime = 1.5f;

	// -------------------------------------------------------------------------
	// Inspector — Debug
	// -------------------------------------------------------------------------

	[Header("Debug")]
	[SerializeField] private bool verboseLogging = true;

	// -------------------------------------------------------------------------
	// Runtime State
	// -------------------------------------------------------------------------

	// How many check-ins have completed. Used to index the close-gloat mutter
	// lines. Incremented once at the start of LeanInPhase.
	private int checkInCount = 0;

	// Re-entry guard for the caught sequence (same pattern as FailureLoopController).
	private bool caughtSequenceRunning = false;

	// Player reference — resolved once at Start.
	private PlayerController player;

	// The model's facing correction, captured from guardBody's authored LOCAL
	// rotation at Start. Guard_Pivot's forward is the direction he's authored to
	// face (in VS_Demo it points at Cassie's chair, ~14.9° yaw); Guard's local
	// 146.358° is the correction that makes the D133 model's front line up with
	// it. So "face direction d" = LookRotation(d) * facingOffset. Derived from
	// the scene rather than hardcoded, so re-authoring the pivot just works.
	private Quaternion facingOffset = Quaternion.identity;

	// The guard's authored world Y, captured at Start before anything moves him.
	// With lockBodyHeight on, every move/snap target is flattened to this height.
	private float bodyY;

	// Day 202 — listening state. Null struggle = legacy level, none of this runs.
	private BondStruggle struggle;
	private float attention;
	private float alertUntil;
	private float approachStart, approachLen, approachTurn;
	private float leaveStart;
	private bool pendingSeen;
	private bool pendingCheck;      // Day 211: this catch came from a bond check
	// Day 211: per-visit watch state, reset when he starts an approach.
	private float suspicion;
	private bool tellShown;
	private bool strayRolled;  // Day 214: this visit's notice roll is spent
	private bool checkPending;
	private float lastTauntAt = -99f;
	private float lingerAdded;
	private float watchExtendUntil;
	private BondStruggle.CatchResult lastCatch;
	private bool stoodDown;     // slipped-out stub: cycle stopped for good
	private bool frozen;        // strike landed: StrikeableGuard owns the body now
	private string listenStatus = "";

	// Where Cassie sat at Start — the caught respawn on BondStruggle levels.
	private Vector3 playerStartPos;
	private Quaternion playerStartRot;

	/// <summary>0 = not looking, 1 = watching her. HUD reads it.</summary>
	public float Attention => attention;
	public float GlanceNoise => glanceNoise;
	public float InspectNoise => inspectNoise;
	public float SeenAttention => seenAttention;
	public bool IsAlerted => Time.time < alertUntil;
	/// <summary>Day 211: 0..1 this visit. 1 = he checks her bonds. HUD reads it.</summary>
	public float Suspicion => suspicion;
	/// <summary>Last thing he did in response to noise. HUD reads it.</summary>
	public string ListenStatus => listenStatus;

	// -------------------------------------------------------------------------
	// Unity Lifecycle
	// -------------------------------------------------------------------------

	void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Debug.LogWarning("[GuardController] Duplicate instance — destroying.");
			Destroy(gameObject);
			return;
		}
		Instance = this;

		if (fadeOverlay != null)
		{
			fadeOverlay.alpha = 0f;
			fadeOverlay.blocksRaycasts = false;
		}
	}

	void OnDestroy()
	{
		if (Instance == this) Instance = null;
		if (struggle != null) struggle.OnPullResolved -= OnPullResolved;
	}

	void Start()
	{
		player = FindFirstObjectByType<PlayerController>();
		if (player == null)
		{
			Debug.LogWarning("[GuardController] No PlayerController found. " +
				"Inspection outcomes will not fire correctly.");
		}

		// Fall back to the scene's StrikeableGuard transform if no explicit
		// body is wired — the cube/model carrying StrikeableGuard is the thing
		// that should move.
		if (guardBody == null)
		{
			StrikeableGuard sg = FindFirstObjectByType<StrikeableGuard>();
			if (sg != null) guardBody = sg.transform;
		}

		if (guardBody != null) bodyY = guardBody.position.y;

		if (guardBody != null && guardBody.parent != null)
			facingOffset = guardBody.localRotation;

		SetLean(false);

		if (player != null)
		{
			struggle = player.Struggle;
			if (struggle != null) struggle.OnPullResolved += OnPullResolved;   // Day 211
			playerStartPos = player.transform.position;
			playerStartRot = player.transform.rotation;
		}
		if (struggle != null) Log("BondStruggle found — listening captor ON, door evidence check OFF.");

		StartCoroutine(OffstagePhase());
	}

	// -------------------------------------------------------------------------
	// Day 202 — Listening captor
	// -------------------------------------------------------------------------

	void Update()
	{
		if (struggle == null || stoodDown || frozen) return;

		if (caughtSequenceRunning || CurrentState == GuardState.Caught || CurrentState == GuardState.Downed)
		{
			attention = 0f;
			return;
		}

		attention = ComputeAttention();

		// Day 214: a bottle left out gets one roll per visit, once he's close to it.
		if (!strayRolled && attention >= seenAttention) CheckStray();

		// Mid-swing, her hands are out by definition. The strike resolves the beat.
		if (player != null && player.IsStriking) return;

		if (attention > 0f)
		{
			// The only catch: he sees her getting free. Noise in front of him is fine.
			if (struggle.HandsOut && attention >= seenAttention) TriggerCaught(true);
			return;
		}

		Listen();
	}

	private float ComputeAttention()
	{
		switch (CurrentState)
		{
			case GuardState.Approaching:
				float el = Time.time - approachStart;
				float rampStart = approachLen - approachTurn;
				if (el <= rampStart) return 0f;
				return Mathf.Clamp01((el - rampStart) / Mathf.Max(0.01f, approachTurn));
			case GuardState.AtDoor:
			case GuardState.LeanIn:
			case GuardState.Checking:
				return 1f;
			case GuardState.Leaving:
				return Mathf.Clamp01(1f - (Time.time - leaveStart) / Mathf.Max(0.01f, turnAwayTime));
			default:
				return 0f;
		}
	}

	// -------------------------------------------------------------------------
	// Day 211 — Watching: misses build suspicion, mashing amuses him
	// -------------------------------------------------------------------------

	private void OnPullResolved(BondStruggle.PullKind kind, bool tool)
	{
		if (struggle == null || stoodDown || frozen || caughtSequenceRunning) return;
		if (CurrentState == GuardState.Checking || CurrentState == GuardState.Caught
			|| CurrentState == GuardState.Downed) return;
		if (attention <= 0f) return;   // not looking: noise is all he gets (Listen)

		switch (kind)
		{
			case BondStruggle.PullKind.Spam:
				Taunt();
				break;
			case BondStruggle.PullKind.Miss:
				if (guardBody != null && player != null)
				{
					Vector3 d = player.transform.position - guardBody.position;
					d.y = 0f;
					if (d.magnitude > missNoticeRange)
					{
						Log($"Miss at {d.magnitude:F1} m — too far to tell.");
						break;
					}
				}
				suspicion = Mathf.Min(1f, suspicion + (tool ? suspicionPerToolMiss : suspicionPerMiss));
				Log($"Miss in his sight{(tool ? " (tool)" : "")} — suspicion {suspicion:F2}.");
				if (suspicion >= 1f) { BeginCheck(); break; }
				if (!tellShown && suspicion >= suspicionTellAt)
				{
					tellShown = true;
					listenStatus = "suspicious";
					SayGuard(suspicionTellLines);
				}
				break;
		}
	}

	/// <summary>
	/// Day 214: if she left something off the table and he's watching from within
	/// strayNoticeRange of it, he gets one roll this visit (strayNoticeChance). Noticing
	/// raises suspicion past the tell; a miss after that sends him in to check (and he puts
	/// it back while he's there).
	/// </summary>
	private void CheckStray()
	{
		if (CurrentState == GuardState.Checking || player == null || guardBody == null) return;
		Pickupable stray = player.StrayItem;
		if (stray == null) return;
		Vector3 d = stray.transform.position - guardBody.position;
		d.y = 0f;
		if (d.magnitude > strayNoticeRange) return;

		strayRolled = true;
		if (Random.value >= strayNoticeChance)
		{
			Log($"{stray.ItemName} on the floor, {d.magnitude:F1} m — didn't notice.");
			return;
		}
		suspicion = Mathf.Min(1f, suspicion + suspicionPerStray);
		Log($"Noticed {stray.ItemName} on the floor, {d.magnitude:F1} m — suspicion {suspicion:F2}.");
		tellShown = true;
		listenStatus = "noticed the bottle";
		SayGuard(strayNoticeLines);
		if (suspicion >= 1f) BeginCheck();
	}

	private void Taunt()
	{
		if (Time.time - lastTauntAt < tauntCooldown) return;
		lastTauntAt = Time.time;
		float add = Mathf.Min(tauntLinger, maxLingerPerVisit - lingerAdded);
		if (add > 0f)
		{
			lingerAdded += add;
			watchExtendUntil = Mathf.Max(watchExtendUntil, Time.time) + add;
		}
		listenStatus = "amused";
		Log($"Mashing in his sight — taunt. Lingering +{add:F1}s.");
		SayGuard(tauntLines);
	}

	// Day 218: his reactions (taunt, tell, notice, "still tight") are ambient. They used to
	// queue as narrative lines, stack up behind each other, and hold up the caught / taken
	// lines that came after.
	private void SayGuard(string[] lines)
	{
		if (MutterSystem.Instance == null || lines == null || lines.Length == 0) return;
		MutterSystem.Instance.PlayAmbient(lines[Random.Range(0, lines.Length)], MutterSystem.Speaker.Guard);
	}

	// Taunts keep him watching. Waited on at the end of AtDoor and LeanIn.
	private IEnumerator HoldWhileLingering()
	{
		while (Time.time < watchExtendUntil) yield return null;
	}

	private void BeginCheck()
	{
		listenStatus = "CHECKING YOUR BONDS";
		// Still walking up: finish the approach, AtDoor turns it into the check.
		if (CurrentState == GuardState.Approaching)
		{
			checkPending = true;
			Log("Suspicion full on the approach — will check on arrival.");
			return;
		}
		if (checkPending) return;
		bool fromDoor = CurrentState == GuardState.AtDoor;
		Log("Suspicion full — checking her bonds.");
		StopAllCoroutines();
		SetLean(false);
		StartCoroutine(CheckBondsPhase(fromDoor));
	}

	/// <summary>
	/// Day 211: he walks in (same route as the lean-in) and examines her bonds. Evidence
	/// decides. Free hands out on the way in are caught by Update, as always.
	/// </summary>
	/// <param name="fromDoor">Route in through the lean-in waypoints. From inside the room
	/// (lean-in, the start of leaving) he goes straight to her.</param>
	private IEnumerator CheckBondsPhase(bool fromDoor)
	{
		checkPending = false;
		SetState(GuardState.Checking);

		if (fromDoor && leanInWaypoints != null)
		{
			foreach (Transform wp in leanInWaypoints)
			{
				if (wp == null) continue;
				yield return MoveBodyAtSpeed(wp.position, guardMoveSpeed);
			}
		}
		yield return MoveBodyAtSpeed(ComputeLeanInPoint(), guardMoveSpeed);
		if (player != null) yield return TurnToFace(player.transform.position);
		SetLean(true);

		yield return new WaitForSeconds(checkDuration);

		// Day 214: whatever she left on the floor goes back on the table.
		if (player != null) player.ReturnStrayItemHome();

		int i = struggle.MostLoosened(out float found);
		if (i >= 0 && found >= checkEvidence)
		{
			Log($"Check: {struggle.bonds[i].name} at {found:P0} — caught.");
			TriggerCaught(false, byCheck: true);
			yield break;
		}

		if (i >= 0) struggle.Cinch(i, cinchSetback);
		Log($"Check: nothing ({(i >= 0 ? $"{struggle.bonds[i].name} {found:P0}" : "clean")}). Cinched.");
		listenStatus = "still tight";
		SayGuard(checkClearLines);
		suspicion = 0f;

		yield return new WaitForSeconds(1f);
		StartCoroutine(LeavingPhase());
	}

	// Noise while he's NOT looking draws him in. While he IS looking, misses and
	// mashing are read in OnPullResolved (Day 211).
	private void Listen()
	{
		if (CurrentState != GuardState.Offstage && CurrentState != GuardState.Leaving) return;

		float n = struggle.Noise;
		bool alerted = IsAlerted;

		if (n >= inspectNoise || (alerted && n >= glanceNoise))
		{
			struggle.SpendNoise();
			alertUntil = 0f;
			listenStatus = "HE HEARD THAT — coming";
			Log($"Heard noise {n:F2} — hurried approach.");
			StopAllCoroutines();
			SetLean(false);
			StartCoroutine(ApproachPhase(true));
		}
		else if (!alerted && n >= glanceNoise)
		{
			struggle.SpendNoise();
			alertUntil = Time.time + alertWindow;
			listenStatus = "he calls out";
			Log($"Heard noise {n:F2} — calls out. Alert {alertWindow}s.");
			if (AudioManager.Instance != null && calloutClip != null)
				AudioManager.Instance.PlaySFX(calloutClip, footstepsVolume, 1f);
			if (MutterSystem.Instance != null && calloutLines != null && calloutLines.Length > 0)
				MutterSystem.Instance.PlayAsGuardBackground(calloutLines[Random.Range(0, calloutLines.Length)]);
		}
	}

	private void TriggerCaught(bool seen, bool byCheck = false)
	{
		if (caughtSequenceRunning) return;

		pendingSeen = seen;
		pendingCheck = byCheck && !seen;
		bool taken = struggle.Catches + 1 >= struggle.maxCatches;
		listenStatus = seen ? "SAW YOUR HANDS" : byCheck ? "FOUND LOOSE BONDS" : "CAUGHT";
		Log($"CAUGHT ({(seen ? "seen free hands" : byCheck ? "bond check" : "other")}) " +
			$"at attention {attention:F2}, state {CurrentState}.{(taken ? " TAKEN." : "")}");

		StopAllCoroutines();
		SetLean(false);
		StartCoroutine(taken ? TakenPhase() : CaughtPhase());
	}

	/// <summary>
	/// Day 202 slipped-out stub: every bond is free and she's leaving. Stop the cycle for
	/// good and put him out of sight. Not Downed — that's the strike's ending, and
	/// SliceCoordinator would play the strike's victory line.
	/// </summary>
	public void StandDown()
	{
		stoodDown = true;
		attention = 0f;
		StopAllCoroutines();
		SetLean(false);
		if (guardBody != null && offstageAnchor != null)
			guardBody.position = OnFloor(offstageAnchor.position);
		Log("Stood down — she slipped out.");
	}

	private IEnumerator TakenPhase()
	{
		caughtSequenceRunning = true;
		SetState(GuardState.Caught);
		Log("=== TAKEN — last catch ===");

		if (player != null) player.enabled = false;
		yield return FadeOverlay(0f, 1f, caughtFadeDuration);
		yield return PlayCaughtSfxSequence();

		if (struggle != null) lastCatch = struggle.RegisterCatch(pendingSeen);

		if (MutterSystem.Instance != null && !string.IsNullOrEmpty(takenGuardLine))
		{
			MutterSystem.Instance.PlayNow(takenGuardLine, MutterSystem.Speaker.Guard);   // Day 218: cuts in
			yield return null;
			yield return new WaitWhile(() => MutterSystem.Instance != null && MutterSystem.Instance.IsActive);
		}

		yield return new WaitForSeconds(takenHoldTime);

		// Day 215: the end card, over the black. Start / R plays again (LevelManager).
		if (EndCard.Instance != null)
		{
			EndCard.Instance.Show(EndCard.Outcome.Taken);
			yield break;
		}

		// Loss. Somewhere worse is a later build; for now, from the top.
		var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
		UnityEngine.SceneManagement.SceneManager.LoadScene(scene.name);
	}

	// -------------------------------------------------------------------------
	// Public API
	// -------------------------------------------------------------------------

	/// <summary>
	/// Hard-stop the guard cycle and move to Downed. Called by StrikeableGuard
	/// when the strike lands. StopAllCoroutines prevents any pending LeanIn /
	/// Leaving logic from running after the guard is KO'd.
	/// </summary>
	public void OnGuardDowned()
	{
		StopAllCoroutines();
		SetState(GuardState.Downed);
		Log("Guard DOWNED. Cycle stopped.");
	}

	/// <summary>
	/// Stop all guard movement and turning WITHOUT changing state. Called by
	/// StrikeableGuard the instant the strike lands, so the procedural crumple
	/// owns the body transform from frame one — otherwise a mid-walk strike
	/// leaves MoveBodyAtSpeed / StepTurn fighting the crumple until Downed.
	/// State stays LeanIn until OnGuardDowned(); Downed timing is unchanged.
	/// (Day 169)
	/// </summary>
	public void FreezeMovement()
	{
		frozen = true;
		StopAllCoroutines();
		Log("Guard movement frozen (strike landed).");
	}

	// -------------------------------------------------------------------------
	// State Machine Phases
	// -------------------------------------------------------------------------

	private IEnumerator OffstagePhase()
	{
		SetState(GuardState.Offstage);

		// Day 202: randomise WHEN he comes back, never how fast he turns.
		float wait = (struggle != null && offstageRange.y > 0f)
			? Random.Range(offstageRange.x, offstageRange.y)
			: offstageDuration;
		Log($"Offstage phase — waiting {wait:F1}s.");

		// Snap the body to its offstage anchor — he's out of sight between
		// check-ins. No-op if no body/anchor wired.
		if (guardBody != null && offstageAnchor != null)
		{
			guardBody.position = OnFloor(offstageAnchor.position);
			if (doorAnchor != null)
				guardBody.rotation = FacingFor(doorAnchor.position - offstageAnchor.position);
		}
		SetLean(false);

		// Auto-release feign at the start of the offstage window. The guard
		// has left; there's no reason to hold the pose. Covers the case where
		// the player forgets to toggle off manually.
		// Day 202: not on BondStruggle levels — hidden hands are her choice, and
		// cancelling the feign there would put them OUT (CancelFeign routes to
		// BondStruggle.BringHandsOut).
		if (struggle == null && player != null && player.IsFeigning)
			player.CancelFeign();

		yield return new WaitForSeconds(wait);
		StartCoroutine(ApproachPhase());
	}

	/// <param name="heard">Day 202: he heard something. Shorter approach, faster turn.</param>
	private IEnumerator ApproachPhase(bool heard = false)
	{
		float duration = heard ? heardApproachDuration : approachDuration;
		approachStart = Time.time;
		approachLen = duration;
		approachTurn = Mathf.Min(heard ? heardTurnTime : turnTime, duration);

		// Day 211: a new visit. Watch state starts clean.
		suspicion = 0f;
		tellShown = false;
		strayRolled = false;
		checkPending = false;
		lingerAdded = 0f;
		watchExtendUntil = 0f;

		SetState(GuardState.Approaching);
		Log(heard ? "Guard HEARD something — hurried approach." : "Guard approaching — feign window OPEN.");
		if (!heard) listenStatus = "footsteps";

		if (AudioManager.Instance != null && approachFootstepsClip != null)
			AudioManager.Instance.PlaySFX(approachFootstepsClip, footstepsVolume, heard ? 1.25f : 1f);

		// Walk to the door over the approach window — this IS the telegraph the
		// player reacts to. Move duration == approachDuration, so the feign
		// window timing is unchanged from the audio-only version.
		yield return MoveBody(doorAnchor, duration);
		StartCoroutine(AtDoorPhase());
	}

	private IEnumerator AtDoorPhase()
	{
		SetState(GuardState.AtDoor);
		Log("Guard at door — feign window CLOSED. Inspecting...");

		// Hold beat before sampling — lets the tension land. He turns to look at
		// her inside the hold, so the total hold time is unchanged.
		float holdStart = Time.time;
		if (player != null)
			yield return TurnToFace(player.transform.position);
		float remaining = inspectionHoldDuration - (Time.time - holdStart);
		if (remaining > 0f)
			yield return new WaitForSeconds(remaining);

		// Day 202: on BondStruggle levels the inspection is continuous — Update has
		// been judging noise and hands since his attention started rising. Getting
		// here means she held still. He walks in.
		if (struggle != null)
		{
			if (checkPending) { StartCoroutine(CheckBondsPhase(true)); yield break; }
			yield return HoldWhileLingering();
			if (checkPending) { StartCoroutine(CheckBondsPhase(true)); yield break; }
			Log("Inspection (listening): she held still. Walking in.");
			StartCoroutine(LeanInPhase());
			yield break;
		}

		// Inspection outcome. Feign is only load-bearing once Cassie has visible
		// escape evidence to hide. No evidence + not feigning = a bound prisoner
		// sitting there — nothing to catch — so it routes to the same lean-in/gloat
		// as a pass. Caught requires BOTH: evidence AND not feigning.
		bool feigning = player != null && player.IsFeigning;
		bool evidence = HasEscapeEvidence();
		bool caught = evidence && !feigning;
		Log($"Inspection: feigning={feigning}, evidence={evidence} -> {(caught ? "CAUGHT" : "SAFE (lean-in)")}");

		if (caught)
			StartCoroutine(CaughtPhase());
		else
			StartCoroutine(LeanInPhase());
	}

	/// <summary>
	/// The close-in: on a passed inspection the guard walks in to Cassie and
	/// gloats in her face. This is the strike window. He does it every check-in,
	/// armed or not — it's his habit. The player has the duration of the walk
	/// plus leanInDuration to press H (Strike); a strike only succeeds if Cassie
	/// is armed (PlayerController.CanStrikeNow gates on the held weapon), so the
	/// unarmed early check-ins are pure threat — he leans in, taunts, leaves.
	///
	/// GuardController does NOT poll for the strike here — PlayerController calls
	/// StrikeableGuard.OnStruck(), which calls OnGuardDowned(), which calls
	/// StopAllCoroutines() on this component. That stops this coroutine
	/// mid-execution: once the guard is down, none of the remaining LeanIn /
	/// Leaving logic should run.
	/// </summary>
	private IEnumerator LeanInPhase()
	{
		SetState(GuardState.LeanIn);

		// Index the close-gloat line, then increment. Index 0 = first check-in;
		// clamps to the last line once exhausted.
		int idx = Mathf.Min(checkInCount, routineGloatLines.Length - 1);
		checkInCount++;

		Log($"Guard CLOSE-IN (check-in #{checkInCount}) — strike window open.");

		// Scene hook — fire anything wired to the close-in moment (camera nudge,
		// strike-verb hint prompt, etc.).
		onLeanInEntered?.Invoke();

		// Walk the routing waypoints first (door → mid-point(s)), THEN the live
		// final leg to Cassie. The waypoints route him around the bath partition
		// (and anything else) that a naive straight door→Cassie lerp would clip
		// through. He's already in LeanIn for all of it, but CanStrikeNow's
		// proximity gate keeps the strike — and the Strike (H) prompt — closed
		// until the final leg brings him within strikeRange, so being in-state
		// during the far waypoint legs is harmless.
		if (leanInWaypoints != null)
		{
			foreach (Transform wp in leanInWaypoints)
			{
				if (wp == null) continue;
				yield return MoveBodyAtSpeed(wp.position, guardMoveSpeed);
			}
		}

		// Final leg: close on wherever Cassie actually is — computed from her live
		// position (she's feigning, so frozen for the duration of the walk). State
		// is already LeanIn, so a strike landing mid-walk is valid (she swings as
		// he closes); if it lands, OnGuardDowned's StopAllCoroutines freezes him here.
		yield return MoveBodyAtSpeed(ComputeLeanInPoint(), guardMoveSpeed);

		// Square up to her, THEN lean. The last leg ends pointed along the walk
		// (waypoint → standoff point), which is close to her but not at her.
		if (player != null)
			yield return TurnToFace(player.transform.position);
		SetLean(true);

		// In her face now — the taunt. He says the same kind of thing every time;
		// he can't tell this one's different. The player can cut him off by
		// striking (H dismisses the mutter and swings — handled in PlayerController).
		if (MutterSystem.Instance != null)
		{
			// Day 218: he's in her face, he talks now. The gloat cuts in over anything still
			// showing; her reaction queues behind it.
			if (routineGloatLines.Length > 0)
				MutterSystem.Instance.PlayNow(routineGloatLines[idx], MutterSystem.Speaker.Guard);
			if (routineReactionLines.Length > idx)
				MutterSystem.Instance.Play(routineReactionLines[idx], MutterSystem.Speaker.Cassie);
		}

		// Hold the strike window. If a strike lands, OnGuardDowned fires
		// StopAllCoroutines — this yield never returns.
		yield return new WaitForSeconds(leanInDuration);
		if (struggle != null) yield return HoldWhileLingering();

		// Window lapsed (no weapon, or the player held off). Guard straightens
		// and leaves; the next check-in is another chance.
		Log("Strike window lapsed. Guard leaving without incident.");
		StartCoroutine(LeavingPhase());
	}

	private IEnumerator LeavingPhase()
	{
		leaveStart = Time.time;
		SetState(GuardState.Leaving);
		Log("Guard leaving — receding to offstage.");

		// Straighten up before he turns away.
		SetLean(false);

		if (AudioManager.Instance != null && leaveFootstepsClip != null)
			AudioManager.Instance.PlaySFX(leaveFootstepsClip, footstepsVolume, 1f);

		// Recede to the offstage anchor at walking speed. Speed-based for the
		// same reason as the lean-in: he now leaves from wherever he leaned in
		// (next to Cassie), so the distance varies — a fixed duration would make
		// his exit pace lurch. If no offstage anchor is wired, fall back to a
		// fixed beat so the abstract state machine still paces.
		// Leave by reversing the approach exactly: Cassie → waypoint(s) → door →
		// offstage. The approach ran offstage → door → waypoint(s) → Cassie, so
		// the recede retraces it. The door leg is NOT optional polish: waypoint0 →
		// offstage is not a clear straight line (it clips the partition and the
		// north wall), but routing back out through the doorway the same way he
		// came in is. All legs are speed-based — the door here is just a routing
		// point on the way out, not the feign-window telegraph it is on approach.
		// Body-gated; the abstract fallback below still paces the state machine
		// before any anchors/waypoints are placed.
		if (guardBody != null && leanInWaypoints != null)
		{
			for (int i = leanInWaypoints.Length - 1; i >= 0; i--)
			{
				if (leanInWaypoints[i] == null) continue;
				yield return MoveBodyAtSpeed(leanInWaypoints[i].position, guardMoveSpeed);
			}
		}

		// Back out through the doorway before receding offstage.
		if (guardBody != null && doorAnchor != null)
			yield return MoveBodyAtSpeed(doorAnchor.position, guardMoveSpeed);

		if (guardBody != null && offstageAnchor != null)
			yield return MoveBodyAtSpeed(offstageAnchor.position, guardMoveSpeed);
		else
			yield return new WaitForSeconds(leavingDuration);

		StartCoroutine(OffstagePhase());
	}

	// -------------------------------------------------------------------------
	// Caught Sequence
	// -------------------------------------------------------------------------

	private IEnumerator CaughtPhase()
	{
		if (caughtSequenceRunning)
		{
			Debug.LogWarning("[GuardController] CaughtPhase called while already running. Ignoring.");
			yield break;
		}
		caughtSequenceRunning = true;
		SetState(GuardState.Caught);
		Log("=== CAUGHT sequence START ===");

		// Step 1: Lock player input.
		if (player != null) player.enabled = false;

		// Step 2: Fade to black.
		yield return FadeOverlay(0f, 1f, caughtFadeDuration);

		// Step 3: Rebind SFX.
		yield return PlayCaughtSfxSequence();

		// Step 4: State mutation — bond escalation + position reset.
		MutateCaughtState();

		// Step 5: Guard caught line (over black).
		// Day 202: seen-free-hands and escalation catches get their own lines.
		string guardLine = caughtGuardLine;
		string cassieLine = caughtCassieReaction;
		if (struggle != null)
		{
			if (lastCatch.seen && !string.IsNullOrEmpty(caughtSeenGuardLine))
				guardLine = caughtSeenGuardLine;
			else if (pendingCheck && !string.IsNullOrEmpty(caughtCheckGuardLine))
				guardLine = caughtCheckGuardLine;
			if (lastCatch.addedBond != null && !string.IsNullOrEmpty(escalationCassieReaction))
				cassieLine = escalationCassieReaction;

			// Day 218: the catch before the last. One line, not a second Play: the sequence
			// waits on his first line only, and a second would play after he's snapped offstage.
			if (lastCatch.catches == struggle.maxCatches - 1 && !string.IsNullOrEmpty(lastWarningGuardLine))
				guardLine = string.IsNullOrEmpty(guardLine) ? lastWarningGuardLine : guardLine + " " + lastWarningGuardLine;
		}

		if (MutterSystem.Instance != null)
		{
			// Day 218: his line cuts in (PlayNow). Step 6 waits on the next dismiss: with an
			// older line still showing, that was the older line's, and the fade came early.
			// With no guard line there's nothing to cut in with: clear the way for hers.
			if (!string.IsNullOrEmpty(guardLine))
				MutterSystem.Instance.PlayNow(guardLine, MutterSystem.Speaker.Guard);
			else
				MutterSystem.Instance.ForceDismissAndClear();
			if (!string.IsNullOrEmpty(cassieLine))
				MutterSystem.Instance.Play(cassieLine, MutterSystem.Speaker.Cassie);
		}

		// Step 6: Wait for guard line dismiss, then fade in.
		yield return null;
		yield return new WaitUntil(() =>
			MutterSystem.Instance == null || MutterSystem.Instance.WasJustDismissed);

		// Snap the guard back offstage while the screen is still black, so he isn't
		// seen popping from the door after fade-in. After his line (Day 216), so the
		// line reads from where he caught her, not from the hall.
		if (guardBody != null && offstageAnchor != null)
			guardBody.position = OnFloor(offstageAnchor.position);

		// Step 7: Fade in.
		yield return FadeOverlay(1f, 0f, caughtFadeDuration);

		// Step 8: Wait for Cassie reaction to finish.
		yield return new WaitWhile(() =>
			MutterSystem.Instance != null && MutterSystem.Instance.IsActive);

		// Step 9: Release input and restart cycle.
		if (player != null) player.enabled = true;

		caughtSequenceRunning = false;
		Log("=== CAUGHT sequence END. Restarting cycle. ===");

		StartCoroutine(OffstagePhase());
	}

	private void MutateCaughtState()
	{
		if (player == null) return;

		if (struggle != null)
		{
			// Day 202: proto catch rules, applied under the fade. Setback + tighten on
			// every bond, wrists from scratch if he saw her hands, reserve bonds tied
			// on (elbows, first catch). BondStruggle re-syncs BoundLimbs + WristsFree.
			lastCatch = struggle.RegisterCatch(pendingSeen);
			Log($"Caught {lastCatch.catches}/{struggle.maxCatches}" +
				$"{(lastCatch.seen ? " (seen free hands)" : "")}" +
				$"{(lastCatch.addedBond != null ? $", tied on {lastCatch.addedBond}" : "")}.");
		}
		else
		{
			// Bond escalation — add the configured bond (if any).
			if (caughtBondToAdd != 0 && player.CurrentRestraint != null)
			{
				player.CurrentRestraint.AddBondState(caughtBondToAdd);
				Log($"Caught bond escalation: added {caughtBondToAdd}. " +
					$"New BoundLimbs = {player.CurrentRestraint.BoundLimbs}");
			}

			// Reset bond cut progress — the guard re-ties her. ResetBondProgress
			// also clears PlayerController.wristsFree so phase 1 restarts correctly
			// if she was caught after having freed her wrists.
			player.ResetBondProgress();
		}

		// Disarm on re-bind: the guard takes what's in her hands. Whether it comes
		// back is the item's policy (Pickupable.returnedOnDisarm) — the bottle is
		// returned to the table because it's the only weapon and the only win path,
		// and it reads as his hubris: he keeps leaving it within her reach.
		player.DisarmHeldItem();

		// Position reset. Day 202: BondStruggle levels go back to where she started.
		if (struggle != null && respawnAtStartPosition)
		{
			player.transform.SetPositionAndRotation(playerStartPos, playerStartRot);
			if (player.Rb != null)
			{
				player.Rb.position = playerStartPos;
				player.Rb.rotation = playerStartRot;
				player.Rb.linearVelocity = Vector3.zero;
				player.Rb.angularVelocity = Vector3.zero;
			}
			Log($"Position reset to level start {playerStartPos}.");
		}
		else if (caughtRespawnPoint != null)
		{
			player.transform.position = caughtRespawnPoint.position;
			player.transform.rotation = caughtRespawnPoint.rotation;
			Log($"Position reset to {caughtRespawnPoint.position}.");
		}

		// Scene-specific resets.
		onCaughtReset?.Invoke();

		// Day 216: the snap offstage moved to CaughtPhase, after his line. His floating
		// text hangs over his head, so snapping here sent the caught line to the hall.
	}

	// -------------------------------------------------------------------------
	// Helpers
	// -------------------------------------------------------------------------

	private IEnumerator PlayCaughtSfxSequence()
	{
		if (caughtRebindSfx == null || caughtRebindSfx.Length == 0) yield break;

		foreach (AudioClip clip in caughtRebindSfx)
		{
			if (clip == null) continue;
			if (AudioManager.Instance != null)
				AudioManager.Instance.PlaySFX(clip, caughtRebindSfxVolume, 1f);
			yield return new WaitForSeconds(clip.length);
		}
	}

	/// <summary>
	/// Lerp the guard body to a target anchor over `duration` seconds. Used ONLY
	/// for the door approach, where the duration IS the feign window — a gameplay
	/// clock, deliberately fixed regardless of distance. (Distance-varying walks —
	/// lean-in, leaving — use MoveBodyAtSpeed instead.) Degrades gracefully: if no
	/// body or target is wired it just waits out the duration so the feign-window
	/// timing is preserved even before the movement anchors are placed.
	/// </summary>
	private IEnumerator MoveBody(Transform target, float duration)
	{
		if (guardBody == null || target == null)
		{
			yield return new WaitForSeconds(duration);
			yield break;
		}

		Vector3 start = guardBody.position;
		Vector3 end = OnFloor(target.position);
		Quaternion face = FacingFor(end - start);
		float t = 0f;
		while (t < duration)
		{
			t += Time.deltaTime;
			guardBody.position = Vector3.Lerp(start, end, Mathf.Clamp01(t / duration));
			StepTurn(face);
			yield return null;
		}
		guardBody.position = end;
	}

	/// <summary>
	/// Move the guard body to an explicit world point at a constant `speed`
	/// (metres/second), so a longer walk simply takes longer rather than moving
	/// faster. Used for the variable-distance walks — the lean-in to Cassie and
	/// the recede offstage. Falls back to an immediate return if there's no body
	/// (the strike window / offstage wait provides pacing in abstract mode).
	/// </summary>
	private IEnumerator MoveBodyAtSpeed(Vector3 end, float speed)
	{
		if (guardBody == null) yield break;

		end = OnFloor(end);

		Vector3 start = guardBody.position;
		float distance = Vector3.Distance(start, end);

		// Degenerate cases: zero distance, or a non-positive speed that would
		// divide-by-zero / never arrive. Snap and bail.
		if (distance < 0.0001f || speed <= 0f)
		{
			guardBody.position = end;
			yield break;
		}

		float duration = distance / speed;
		Quaternion face = FacingFor(end - start);
		float t = 0f;
		while (t < duration)
		{
			t += Time.deltaTime;
			guardBody.position = Vector3.Lerp(start, end, Mathf.Clamp01(t / duration));
			StepTurn(face);
			yield return null;
		}
		guardBody.position = end;
	}

	/// <summary>
	/// Body rotation that points the MODEL's front along a world direction
	/// (yaw only). Zero-length direction keeps the current rotation.
	/// </summary>
	private Quaternion FacingFor(Vector3 direction)
	{
		if (guardBody == null) return Quaternion.identity;
		direction.y = 0f;
		if (direction.sqrMagnitude < 0.0001f) return guardBody.rotation;
		return Quaternion.LookRotation(direction.normalized, Vector3.up) * facingOffset;
	}

	/// <summary>
	/// Flatten a move/snap target to the guard's authored height when
	/// lockBodyHeight is on. Anchor/waypoint Y is ignored. (Day 171)
	/// </summary>
	private Vector3 OnFloor(Vector3 p)
	{
		if (lockBodyHeight && guardBody != null) p.y = bodyY;
		return p;
	}

	/// <summary>One frame of turning toward `target` at turnSpeed. 0 = snap.</summary>
	private void StepTurn(Quaternion target)
	{
		if (guardBody == null) return;
		guardBody.rotation = turnSpeed <= 0f
			? target
			: Quaternion.RotateTowards(guardBody.rotation, target, turnSpeed * Time.deltaTime);
	}

	/// <summary>Turn in place until facing a world point. Used at the door and
	/// at the end of the lean-in walk.</summary>
	private IEnumerator TurnToFace(Vector3 worldPoint)
	{
		if (guardBody == null) yield break;
		Quaternion target = FacingFor(worldPoint - guardBody.position);
		while (Quaternion.Angle(guardBody.rotation, target) > 0.5f)
		{
			StepTurn(target);
			yield return null;
		}
		guardBody.rotation = target;
	}

	/// <summary>
	/// Swap between the upright and leaning guard models. No-op if the lean
	/// model isn't wired, so an unwired scene keeps the upright guard.
	/// </summary>
	private void SetLean(bool lean)
	{
		if (leanModel == null) return;
		if (standingModel != null) standingModel.SetActive(!lean);
		leanModel.SetActive(lean);
	}

	/// <summary>
	/// The world point the guard steps to when he leans in: leanInStandoff
	/// metres in front of Cassie, on the side he's approaching from (his
	/// current position, i.e. the door), so he stops at her face without
	/// overlapping her. Computed at the moment of lean-in — she's feigning and
	/// therefore stationary, so a one-shot snapshot is accurate. Keeps his own
	/// height so he doesn't sink into the floor.
	/// </summary>
	private Vector3 ComputeLeanInPoint()
	{
		if (guardBody == null) return Vector3.zero;
		if (player == null) return guardBody.position;

		Vector3 cassie = player.transform.position;
		Vector3 fromCassieToGuard = guardBody.position - cassie;
		fromCassieToGuard.y = 0f;

		// Degenerate case (guard already on top of her): just hold position.
		if (fromCassieToGuard.sqrMagnitude < 0.0001f) return guardBody.position;

		Vector3 point = cassie + fromCassieToGuard.normalized * leanInStandoff;
		point.y = guardBody.position.y;
		return point;
	}

	private IEnumerator FadeOverlay(float from, float to, float duration)
	{
		if (fadeOverlay == null) yield break;

		if (to > from) fadeOverlay.blocksRaycasts = true;

		float t = 0f;
		while (t < duration)
		{
			t += Time.deltaTime;
			fadeOverlay.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
			yield return null;
		}
		fadeOverlay.alpha = to;

		if (to <= 0f) fadeOverlay.blocksRaycasts = false;
	}

	private void SetState(GuardState newState)
	{
		CurrentState = newState;
		OnStateChanged?.Invoke(newState);
		Log($"State → {newState}");
	}

	private void Log(string msg)
	{
		if (verboseLogging) Debug.Log($"[Guard] {msg}");
	}

	/// <summary>
	/// True when Cassie is visibly mid-escape — the state a feign conceals.
	/// Wrists already free, any bond-cut progress, or a tool in hand. All three
	/// read from existing PlayerController surface. Moved-from-spawn intentionally
	/// excluded: noisy under struggle jitter, redundant in the VS.
	/// </summary>
	private bool HasEscapeEvidence()
	{
		if (player == null) return false;
		return player.WristsFree
			|| player.StruggleProgress > 0
			|| player.GetHeldItem() != null;
	}
}
