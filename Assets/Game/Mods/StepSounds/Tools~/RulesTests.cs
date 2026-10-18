using System;
using StepSounds;

/// <summary>Checks FootstepRules without Unity: test-rules.sh compiles it with FootstepRules.cs and runs it.</summary>
static class RulesTests
{
    static int failures;

    static void Check(bool ok, string what)
    {
        if (!ok)
        {
            failures++;
            Console.WriteLine("FAIL: " + what);
        }
    }

    static int Main()
    {
        // Footfalls: first and middle frame of the cycle.
        Check(FootstepRules.IsFootfall(0, 4) && FootstepRules.IsFootfall(2, 4), "4 frames: 0 and 2");
        Check(!FootstepRules.IsFootfall(1, 4) && !FootstepRules.IsFootfall(3, 4), "4 frames: not 1 or 3");
        Check(FootstepRules.IsFootfall(0, 5) && FootstepRules.IsFootfall(2, 5) && !FootstepRules.IsFootfall(4, 5), "5 frames: 0 and 2");
        Check(FootstepRules.IsFootfall(0, 1) && !FootstepRules.IsFootfall(1, 1), "1 frame: only 0");
        Check(FootstepRules.IsFootfall(0, 0), "no frame count: frame 0");

        // Classes and profiles.
        Check(FootstepRules.IsClass(128) && FootstepRules.IsClass(146) && !FootstepRules.IsClass(42) && !FootstepRules.IsClass(147), "class ids 128-146");
        Check(FootstepRules.ForEnemy(128) == FootstepRules.Person, "mage walks like a person");
        Check(FootstepRules.ForEnemy(146) == FootstepRules.Armoured, "city watch is armoured");
        Check(FootstepRules.ForEnemy(0).Volume < FootstepRules.Person.Volume && FootstepRules.ForEnemy(0).Pitch > 1f, "rat: quiet, high");
        Check(FootstepRules.ForEnemy(16).Pitch < 1f && FootstepRules.ForEnemy(16).Volume >= FootstepRules.Person.Volume, "giant: deep, loud");
        Check(FootstepRules.ForEnemy(7) == FootstepRules.Creature, "orc: default creature");
        Check(FootstepRules.Person.Volume == 0.85f, "people as loud as ANPC's default steps");

        // Volume and pitch.
        Check(Math.Abs(FootstepRules.Volume(FootstepRules.Person, 100) - 0.85f) < 1e-5f, "volume 100%");
        Check(FootstepRules.Volume(FootstepRules.Person, 0) == 0f && FootstepRules.Volume(FootstepRules.Person, -5) == 0f, "volume 0% and below");
        Check(Math.Abs(FootstepRules.Volume(FootstepRules.Person, 250) - 0.85f) < 1e-5f, "volume clamped to 100%");
        Check(Math.Abs(FootstepRules.Pitch(FootstepRules.Person, 0) - (1f - FootstepRules.PitchVariation)) < 1e-5f, "lowest pitch");
        Check(Math.Abs(FootstepRules.Pitch(FootstepRules.Person, 1) - (1f + FootstepRules.PitchVariation)) < 1e-5f, "highest pitch");
        Check(FootstepRules.Pitch(new StepProfile(1f, 0f), 0) == FootstepRules.MinPitch, "pitch floor");

        // Walking.
        Check(FootstepRules.IsWalking(1.3f / 60f, 1f / 60f), "townsperson speed walks");
        Check(!FootstepRules.IsWalking(0.001f, 1f / 60f), "nudge is not walking");
        Check(!FootstepRules.IsWalking(0.1f, 0f), "paused frame is not walking");
        Check(!FootstepRules.IsWalking(40f, 1f / 60f), "jump to a new place is not walking");

        // Step files.
        Check(FootstepRules.IsStepFile("step_1.ogg") && FootstepRules.IsStepFile("Step_12.WAV") && FootstepRules.IsStepFile("step.mp3"), "step files");
        Check(!FootstepRules.IsStepFile("step_1.ogg.meta") && !FootstepRules.IsStepFile("step_.ogg") && !FootstepRules.IsStepFile("step2.ogg") &&
              !FootstepRules.IsStepFile("step_a.ogg") && !FootstepRules.IsStepFile("greet.ogg") && !FootstepRules.IsStepFile(null), "not step files");
        Check(FootstepRules.IsStepFile("assets/game/mods/stepsounds/sounds/step_1.ogg".Substring(35)), "bundle asset file name");

        // Wings.
        Check(FootstepRules.IsWingFile("wing_1.ogg") && FootstepRules.IsWingFile("WING.wav") && !FootstepRules.IsWingFile("step_1.ogg"), "wing files");
        Check(!FootstepRules.IsStepFile("wing_1.ogg") && !FootstepRules.IsWingFile("wings_1.ogg") && !FootstepRules.IsWingFile("wing_.ogg"), "pools kept apart");
        Check(FootstepRules.IsWingBeat(0) && !FootstepRules.IsWingBeat(2), "a beat per loop, on frame 0");
        Check(FootstepRules.ForFlyer(3).Volume < FootstepRules.ForFlyer(34).Volume && FootstepRules.ForFlyer(3).Pitch > FootstepRules.ForFlyer(34).Pitch, "bat softer and higher than dragonling");
        Check(FootstepRules.ForFlyer(300) == FootstepRules.Flyer && FootstepRules.ForFlyer(40).Pitch == FootstepRules.ForFlyer(34).Pitch, "unknown flyer default; both dragonlings alike");
        Check(FootstepRules.ForEnemy(300) == FootstepRules.Creature, "a mod's enemy id gets the default profile");

        Console.WriteLine(failures == 0 ? "rules tests: OK" : "rules tests: " + failures + " failed");
        return failures == 0 ? 0 : 1;
    }
}
