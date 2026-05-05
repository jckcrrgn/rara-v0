using System;

/// <summary>
/// One row of the control hints UI: a verb label and the key that triggers it.
/// Lives in the model layer so restraints can build hint lists without knowing
/// anything about UI.
///
/// `conditional` flags hints that exist in the current restraint but aren't
/// usable in the current mode/state — e.g. F (kick) in FloorRestraint inch mode.
/// The UI greys these and may append a parenthetical suffix. Showing them with
/// conditional text (rather than hiding them) teaches the player the relationship
/// between modes and verbs without requiring them to discover it through trial.
/// </summary>
[Serializable]
public struct ControlHint
{
	public string verb;       // "Crawl", "Kick", "Struggle"
	public string key;        // "W (hold)", "F", "Space"
	public bool conditional;  // true = greyed/suffixed in UI
	public string conditionalSuffix; // optional: e.g. "(flip first)"

	public ControlHint(string verb, string key, bool conditional = false, string conditionalSuffix = null)
	{
		this.verb = verb;
		this.key = key;
		this.conditional = conditional;
		this.conditionalSuffix = conditionalSuffix;
	}
}
