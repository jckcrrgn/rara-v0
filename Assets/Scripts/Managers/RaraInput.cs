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
//   A            pick up / set down (Day 214) · dismiss mutter
//   X            strike
//   Start        restart (LevelManager, Day 214)
//   Y            end card: open the owned channel (Day 215)
//
// PROMPTS (Day 215)
//   Buttons are read by position (south/west/...), so any pad the Input System knows
//   works: Xbox, PlayStation, Switch Pro, generic XInput/HID. Names above are Xbox.
//   Label(Button.X) gives the name on the player's pad ("X", "Square", "Y" on Switch).
//   Pad.usingPad = the last thing touched was the pad -> show pad prompts, else keys.
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
		public bool secondary;     // Y pressed this frame (Day 215: end card channel link)
		public bool usingPad;      // Day 215: last input came from the pad (prompts follow this)
	}

	/// <summary>Pad buttons by position, Xbox names. Label() gives the player's pad's name.</summary>
	public enum Button { A, B, X, Y, LB, RB, LT, RT, Start, Select }

	/// <summary>
	/// Day 215: what this button is called on the connected pad. "X" on Xbox, "Square" on
	/// PlayStation, "Y" on Switch Pro. Falls back to the Xbox name with no pad.
	/// </summary>
	public static string Label(Button b)
	{
#if ENABLE_INPUT_SYSTEM
		var gp = UnityEngine.InputSystem.Gamepad.current;
		if (gp == null) return b.ToString();
		UnityEngine.InputSystem.Controls.ButtonControl c = b switch
		{
			Button.A => gp.buttonSouth,
			Button.B => gp.buttonEast,
			Button.X => gp.buttonWest,
			Button.Y => gp.buttonNorth,
			Button.LB => gp.leftShoulder,
			Button.RB => gp.rightShoulder,
			Button.LT => gp.leftTrigger,
			Button.RT => gp.rightTrigger,
			Button.Start => gp.startButton,
			_ => gp.selectButton,
		};
		// Device layouts name the button ("Cross", "L1", "Options"); the generic layout's
		// names are long ("Button South", "Left Shoulder") and its short names are Xbox.
		string name = c.displayName;
		if (string.IsNullOrEmpty(name) || name.Contains(" ") || name.Length > 8)
			name = string.IsNullOrEmpty(c.shortDisplayName) ? b.ToString() : c.shortDisplayName;
		return name;
#else
		return b.ToString();
#endif
	}

	const float TriggerPress = 0.6f;
	const float TriggerRelease = 0.3f;

	static int cachedFrame = -1;
	static Pad cached;

#if ENABLE_INPUT_SYSTEM
	static bool usingPadInit, lastUsedPad;
#else
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
		if (gp == null) { usingPadInit = true; lastUsedPad = false; return p; }
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
		p.secondary = gp.buttonNorth.wasPressedThisFrame;

		// Day 215: who touched what last. Pad wins at start if one is plugged in.
		bool padTouched = p.pull || p.moveLayer || p.dpad >= 0 || p.hide || p.interact || p.strike ||
			p.restart || p.secondary || gp.buttonEast.wasPressedThisFrame ||
			p.stick.sqrMagnitude > 0.25f || gp.rightStick.ReadValue().sqrMagnitude > 0.25f;
		var kb = UnityEngine.InputSystem.Keyboard.current;
		var mouse = UnityEngine.InputSystem.Mouse.current;
		bool keysTouched = (kb != null && kb.anyKey.wasPressedThisFrame) ||
			(mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame));
		if (!usingPadInit) { usingPadInit = true; lastUsedPad = true; }
		if (padTouched) lastUsedPad = true;
		else if (keysTouched) lastUsedPad = false;
		p.usingPad = lastUsedPad;
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
		p.secondary = Input.GetKeyDown(KeyCode.JoystickButton3);   // Y
		p.usingPad = p.connected;
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
