# Rara demo: line sheet (Day 222)

Every line a player can see or hear in VS_Demo. Jack rewrites them all by hand before
the demo ships, so it ships with his own words. Write your line under each one, or put
"keep" if a line is already yours. Claude wires them in on one branch (scene values, plus the
code default where the scene doesn't override it). Where a slot holds several lines, it picks
one at random, so you can write more or fewer.

Rules from the page copy: lead with competence, not peril. She's been in worse.

---

## Cassie

**Opening line** — as the scene starts (`LevelManager.entryMutter`)
- Chair. Rope. Mid-range hotel room. Cute...
- → Tied to a chair in a sketchy back room. Real smooth, Cassie.

**Close reaction** — after each routine gloat from him, under her breath (`GuardController.routineReactionLines`)
- Keep walking.
- That's it. Trust the knots.
- → Hmph. / Ooo, a tough guy. / Grrrr. / Oh please.

**Hint, barehand** — after 8 good barehand pulls on the wrists (`EdgeHintMutters.barehandLine`)
- Could do this all night. Rather not.
- → This could take a while...

**Hint, edge** — inside the table edge's reach, wrists bound (`EdgeHintMutters.edgeLine`)
- That edge'll do.
- → The table edge...

**Caught** — after he catches her and re-ties (`GuardController.caughtCassieReaction`)
- Back to square one. Think, Cassie.
- → Pretty sharp for a henchman. Ow, this is even tighter.

**Elbows tied on** — first catch, when he adds the elbows (`GuardController.escalationCassieReaction`)
- My ELBOWS? Really?
- → keep

**Strike blocked** — tries to strike with elbows bound (`PlayerController.elbowsStrikeLine`, code default)
- Not with my elbows tied.
- → Gotta free my elbows first.

**Slipped out** — every bond free (`BondStruggle.slippedOutLine`)
- Rookie knots.
- → Pathetic ropework. Not exactly a Boy Scout, is he?

**Bottle win** — after the strike lands (`SliceCoordinator.victoryMutterLine`)
- I've had worse. He hasn't.
- → keep

---

## The guard

**Routine gloat** — leaning in on a routine check-in (`routineGloatLines`)
- Comfortable? Good. Don't go anywhere.
- Still with us? Wonderful.
- → How's our snooping detective? / If you need anything, just 'mmph'. / Comfy? Good. Don't go anywhere.

**Call-out** — offstage, noise reaches him (`calloutLines`)
- Hm?
- You say something?
- Quiet in there.
- → What was that? / Huh? / Keep it down! / How are you STILL so noisy? / Don't make me come in there.

**Taunt** — she's mashing in front of him (`tauntLines`)
- Putting on a show? Nobody can hear you.
- Go on. Wear yourself out.
- Nobody's coming.
- → Putting on a show, eh? / Go ahead. Wear yourself out. / Hey hey hey! Why so worked up, princess?

**Suspicion tell** — the warning before he checks (`suspicionTellLines`)
- ...Hm.
- What are you doing back there?
- → What the hell is she doing? / Hey, what's going on in there? / What the hell...?

**Stray bottle** — he notices the bottle on the floor (`strayNoticeLines`)
- ...Was that bottle on the floor before?
- Where'd that come from?
- → How'd that bottle get there? / Did you put that there?

**Check, clear** — he checks, finds nothing, cinches a bond (`checkClearLines`)
- Still tight.
- Not going anywhere.
- → keep

**Caught (default)** (`caughtGuardLine`)
- I knew it. Let's try that again — tighter this time.
- → keep (fallback; no demo path plays it)

**Caught on a check** (`caughtCheckGuardLine`)
- Thought so. Let's tighten these.
- → I knew it. Let's just tighten these.

**Caught, saw her hands** (`caughtSeenGuardLine`)
- Busy hands. Let's fix that.
- → keep

**Last warning** — added to his caught line on the catch before the last (`lastWarningGuardLine`, code default)
- Once more and you're coming with me.
- → Sit tight. We'll be leaving soon.

**Taken** — third catch, over black (`takenGuardLine`)
- Enough. You're coming with me.
- → That's it. You're coming with me.

---

## End card (`EndCard`)

**Slipped out** (`slippedOutLine`)
- Gone before he thought to look.
- → The escape artiste can't be tied down!

**Bottle** (`strikeLine`)
- He was watching the wrong thing.
- → He should really cut back.

**Taken** (`takenLine`)
- He's moving her somewhere worse.
- → How bad could a second location be?

**Thanks** (`thanksLine`)
- Thanks for playing the demo.
- → keep

---

## Optional: tutorial prompts (`TutorialPrompts`)

These are instructions, not dialogue. `{0}` is the button, filled in for pad or keyboard.
Rewrite them if you want every word to be yours.

- {0}: struggle. Follow her rhythm. →
- {0}: choose a bond →
- Hold {0}: move the chair →
- {0}: untie. Follow the loop. →
- {0}: hide your hands →
- {0}: pick up →

---

## Also on the itch page

The demo section of the description (from "She's tied to a chair in a back room..."
down to the Discord line) started as a Claude draft. The Sep 19 opener is yours.
To uncheck "Text" in the AI disclosure, rewrite that section too, or leave "Text" checked.
