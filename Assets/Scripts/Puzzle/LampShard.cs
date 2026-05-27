using UnityEngine;

// Splinter of a broken lamp. Spawned by LampSmashTrigger.OnCollisionEnter
// when the lamp impacts at sufficient velocity. Mechanically a BladeTool
// (bladeProgress against rope bondStrength — fastest cut in the level,
// 3 Struggles to break wrists).
//
// The Blade classification is the cap on L6's three-tier solve risk ladder:
//   - Patient (pen, Point):       5 Struggles to cut, no timer pressure.
//   - Fast (chair shards, Point): 5 Struggles to cut, timer + bond cost on fail.
//   - Loud (lamp shards, Blade):  3 Struggles to cut, timer + floor-crawl
//                                 across the room + highest commitment.
// The lamp's risk/reward placement is the fastest cut as payoff for the
// loudest, most-committal trigger. See rara-l6-spec-v2.md §7 ("Three viable
// escape paths") and ChairShard.cs for the Point-tier counterpart rationale.
//
// Subclassed rather than attaching BladeTool directly so that:
//   1. LampSmashTrigger can reference the type concretely when spawning.
//   2. Future identity hooks (lamp-shard-specific pickup SFX, glass-shimmer
//      effects, narrative interactions) have a place to land without
//      retrofitting.
public class LampShard : BladeTool
{
}
