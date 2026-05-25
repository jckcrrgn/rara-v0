using UnityEngine;

// Splinter of a broken chair. Spawned by ChairRestraint.OnSideMarkerHitGround
// when rocking tips the chair past its side-marker collider. Mechanically a
// PointTool (pointProgress=5 against rope bondStrength=25 = 5 Struggles to cut),
// same time-to-cut as the pen in the L6 drawer.
//
// The Point classification is intentional. The three L6 solve paths are tiered
// by RISK, not by time-to-cut once a tool is in hand:
//   - Patient (pen, Point):       no timer, requires back-scoot + alignment.
//   - Fast (chair shards, Point): timer starts on tip-crash, paid in bonds on
//                                 failure. Speed advantage is acquisition, not
//                                 cutting.
//   - Loud (lamp shards, Blade):  timer starts on smash, plus a floor-crawl
//                                 across the room. Highest commitment, fastest
//                                 cut (3 Struggles) as reward.
// Promoting ChairShard to Blade would flatten the patient path -- "fast" would
// dominate it in every dimension except noise. See rara-l6-spec-v2.md §7 and
// the Day 47 design note in chat history.
//
// Subclassed rather than attaching PointTool directly so that:
//   1. ChairRestraint can reference the type concretely when spawning.
//   2. Future identity hooks (chair-shard-specific pickup SFX, debug logs,
//      narrative interactions) have a place to land without retrofitting.
public class ChairShard : PointTool
{
}
