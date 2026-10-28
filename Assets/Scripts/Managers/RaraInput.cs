// RaraInput.cs — Day 202. One place that reads the pad, so BondStruggle, PlayerController
// and MutterSystem agree on what was pressed this frame. Keyboard stays where it was
// (each script reads its own keys); this file is pad only.
//
// Sampled once per frame and cached, so any number of callers can ask for "pressed this
// frame" without one of them eating the edge.
//
// PAD MAP (Day 202)
//   left stick   select bond            (LT held: move the chair instead)
//   RT           pull (struggle)
//   LT  (hold)   move layer
//   D-pad        untie (follow the knot)
//   LB           hide hands / feign
//   A            pick up · dismiss mutter
//   X            strike
//   Start        (reserved: restart)
//
// SETUP
//   Input System package installed (Active Input Handling = Both): nothing to do.
//   Legacy only — add these in the Input Manager, or triggers / D-pad stay dead:
//     "RightTrigger"  Joystick Axis, 10th axis, Joy Num all, Dead 0.05, Sens 1
//     "LeftTrigger"   Joystick Axis,  9th axis, Joy Num all, Dead 0.05, Sens 1
//     "DPadX"         Joystick Axis,  6th axis, Joy Num all, Dead 0.1,  Sens 1
//     "DPadY"         Joystick Axis,  7th axis, Joy Num all, Dead 0.1,  Sens 1
//   (Windows XInput numbering. Same as StruggleProto's header, plus LeftTrigger.)
//   Legacy left stick arrives through Horizontal/Vertical, which also carry WASD —
//   harmless: BondStruggle treats WASD and the stick as the same direction input.

using UnityEngine;

public static class RaraInput
{
	public struct Pad
	{
		public bool connected;
		public Vector2 stick;      // left stick, -1..1
		public bool pull;          // RT pressed this frame
		public bool moveLayer;     // LT held
		public int dpad;           // pressed this frame: -1 none, 0 up, 1 right, 2 down, 3 left
		public bool hide;          // LB pressed this frame
		public bool interact;      // A pressed this frame
		public bool interactHeld;  // A held (mutter release gate)
		public bool strike;        // X pressed this frame
		public bool restart;       // Start pressed this frame
	}

	const float TriggerPress = 0.6f;
	const float TriggerRelease = 0.3f;

	static int cachedFrame = -1;
	static Pad cached;

#if !ENABLE_INPUT_SYSTEM
	static bool axesChecked, hasRT, hasLT, hasDpad;
	static bool rtHeld, dpadHeld;
#endif

	public static Pad Read()
	{
		if (cachedFrame == Time.frameCount) return cached;
		cachedFrame = Time.frameCount;
		cached = Sample();
		return cached;
	}

	static Pad Sample()
	{
		var p = new Pad { dpad = -1 };
#if ENABLE_INPUT_SYSTEM
		var gp = UnityEngine.InputSystem.Gamepad.current;
		if (gp == null) return p;
		p.connected = true;
		p.stick = gp.leftStick.ReadValue();
		p.pull = gp.rightTrigger.wasPressedThisFrame;
		p.moveLayer = gp.leftTrigger.ReadValue() > TriggerPress;
		if (gp.dpad.up.wasPressedThisFrame) p.dpad = 0;
		else if (gp.dpad.right.wasPressedThisFrame) p.dpad = 1;
		else if (gp.dpad.down.wasPressedThisFrame) p.dpad = 2;
		else if (gp.dpad.left.wasPressedThisFrame) p.dpad = 3;
		p.hide = gp.leftShoulder.wasPressedThisFrame;
		p.interact = gp.buttonSouth.wasPressedThisFrame;
		p.interactHeld = gp.buttonSouth.isPressed;
		p.strike = gp.buttonWest.wasPressedThisFrame;
		p.restart = gp.startButton.wasPressedThisFrame;
#else
		if (!axesChecked)
		{
			axesChecked = true;
			hasRT = AxisExists("RightTrigger");
			hasLT = AxisExists("LeftTrigger");
			hasDpad = AxisExists("DPadX") && AxisExists("DPadY");
		}
		p.connected = Input.GetJoystickNames().Length > 0;
		p.stick = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

		if (hasRT)
		{
			float rt = Input.GetAxisRaw("RightTrigger");
			if (!rtHeld && rt > TriggerPress) { rtHeld = true; p.pull = true; }
			else if (rtHeld && rt < TriggerRelease) rtHeld = false;
		}
		if (hasLT) p.moveLayer = Input.GetAxisRaw("LeftTrigger") > TriggerPress;
		if (hasDpad) p.dpad = ReadDpad();

		p.interact = Input.GetKeyDown(KeyCode.JoystickButton0);    // A
		p.interactHeld = Input.GetKey(KeyCode.JoystickButton0);
		p.strike = Input.GetKeyDown(KeyCode.JoystickButton2);      // X
		p.hide = Input.GetKeyDown(KeyCode.JoystickButton4);        // LB
		p.restart = Input.GetKeyDown(KeyCode.JoystickButton7);     // Start
#endif
		return p;
	}

	/// <summary>Empty when the pad is fully usable; otherwise what's missing. HUD shows it.</summary>
	public static string Status()
	{
#if ENABLE_INPUT_SYSTEM
		return UnityEngine.InputSystem.Gamepad.current == null ? "PAD: none detected" : "";
#else
		Read();
		if (hasRT && hasLT && hasDpad) return "";
		return "PAD: " + (hasRT ? "" : "RT ") + (hasLT ? "" : "LT ") + (hasDpad ? "" : "D-pad ") +
			"not set up — see RaraInput.cs header";
#endif
	}

#if !ENABLE_INPUT_SYSTEM
	static bool AxisExists(string axis)
	{
		try { Input.GetAxisRaw(axis); return true; }
		catch (System.ArgumentException)
		{
			Debug.LogWarning($"RaraInput: no Input Manager axis '{axis}'. See RaraInput.cs header.");
			return false;
		}
	}

	static int ReadDpad()
	{
		float x = Input.GetAxisRaw("DPadX"), y = Input.GetAxisRaw("DPadY");
		if (dpadHeld)
		{
			if (Mathf.Abs(x) < 0.3f && Mathf.Abs(y) < 0.3f) dpadHeld = false;
			return -1;
		}
		int dir = -1;
		if (y > 0.5f) dir = 0; else if (x > 0.5f) dir = 1;
		else if (y < -0.5f) dir = 2; else if (x < -0.5f) dir = 3;
		if (dir >= 0) dpadHeld = true;
		return dir;
	}
#endif
}
