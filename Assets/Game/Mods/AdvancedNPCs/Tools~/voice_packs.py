"""Builds the shared voice packs (ANPCs/_Sounds/va_*) from downloaded voice packs.

Every clip: first audio stream only (cover art breaks DFU's .ogg decoding), mono 22 kHz, silence trimmed, peak at -1 dB,
OGG Vorbis. Files of one pool are numbered <pool>_1.ogg, <pool>_2.ogg, ... in the order listed; a source file is used
once per pack (first pool that lists it); at most 16 per pool.

    python voice_packs.py [pack ...]          # all packs, or only the named ones
    VOICE_SRC=<folder of extracted packs>  ANPC_LOCAL_SOUNDS=<overlay root>

Packs marked "local" (licenses that forbid sharing the raw files) go to ANPC_LOCAL_SOUNDS/_Sounds, never into git.
"""
import fnmatch, os, re, shutil, subprocess, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.environ.get("VOICE_SRC", "F:/Godot/assets/Voice Packs")
GIT_OUT = os.path.join(HERE, "..", "Examples", "ANPCs", "_Sounds")
LOCAL_OUT = os.path.join(os.environ.get("ANPC_LOCAL_SOUNDS", "F:/_Projects/Dagerfall/anpc_local_sounds"), "_Sounds")
RATE = 22050
MAX_PER_POOL = 16

# name: (source folder under SRC, "git" or "local", [(pool, [patterns]), ...]); patterns are fnmatch globs on the path
# relative to the source folder, case-insensitive. "split:" before a pattern cuts each file at its pauses.
PACKS = {
    "va_warrior_male": ("Warrior Voice Pack/Warrior Pack", "git", [
        ("greet", ["hello*", "welcome*"]),
        ("talk", ["inquisitive*", "sceptical*", "surprised*", "thankful*", "satisfied*"]),
        ("attack", ["attack*", "lightattack*", "heavyattack*"]),
        ("hit", ["hit*", "crit*"]),
        ("death", ["death*"]),
        ("cm_bark", ["amused*", "laugh*", "sleepy*"]),
        ("cm_hurt", ["bleeding*", "poisoned*", "tummyhurt*"]),
        ("cm_recruit", ["yesconfirm*", "yes*"]),
        ("cm_follow", ["here*"]),
        ("cm_dismiss", ["bye*"]),
        ("cm_combat", ["angry*"]),
        ("cm_victory", ["joyous*"]),
        ("cm_push", ["irritated*"]),
    ]),
    "va_orc_male": ("Orc Voice Pack/Orc Pack", "git", [
        ("talk", ["inquisitive*", "sceptical*", "surprised*", "thankful*", "satisfied*", "sorry*"]),
        ("attack", ["attack*", "lightattack*", "heavyattack*"]),
        ("hit", ["hit*", "crit*"]),
        ("death", ["death*"]),
        ("cm_bark", ["amused*", "laugh*", "burp*", "sleep*"]),
        ("cm_hurt", ["bleeding*", "poisoned*", "tumhurty*"]),
        ("cm_recruit", ["yescommand*", "yes[0-9]*", "yeah.wav"]),
        ("cm_combat", ["angry*"]),
        ("cm_victory", ["joyous*", "excited*"]),
        ("cm_push", ["irritated*"]),
    ]),
    "va_orc_warlord_male": ("ALL ORC COMMANDER AUDIO FILES", "git", [
        ("attack", ["ORC FIGHT*", "DIE.wav", "ORC GROWL*"]),
        ("hit", ["ORC GRUNT*"]),
        ("death", ["ORC DIES*"]),
        ("talk", ["KEEP CHARGING*", "LEAVE NO*", "MERCY IS*", "ORCS RETREAT*", "ORCS WAIT*", "orcs we march*",
                  "TOMORROW IS*"]),
        ("cm_combat", ["BATTLE HAS*", "DIE HUMAN*", "IS THAT ALL*"]),
        ("cm_victory", ["THE VICTORY*", "ORC LAUGH*", "I EXPECTED*"]),     # "LOOK AT THE HUMANS RUN AWAY": 11 s, left out
    ]),
    "va_hunter_male": ("Ash's Epic Voices Volume 05 Bountiful Hunter/Ash - Bountiful Hunter - WAV", "git", [
        ("greet", ["*- Hello 0*", "*- Welcome 0*", "*- Good Morning 0*"]),
        ("hawk", ["*- Seller 0*"]),
        ("talk", ["*- I see 0*", "*- Oh Really 0*", "*- Maybe 0*", "*- I Don't Know 0*", "*- Hmm 0*", "*- How.wav",
                  "*- Why.wav", "*- Where.wav", "*- When.wav"]),
        ("attack", ["*- Attack.wav", "*- Grunt 0*", "*- Charge Up 0*"]),
        ("hit", ["*- Get Hit 0*", "*- Ah 0*"]),
        ("death", ["*- Groan 0*"]),
        ("cast", ["*- Magic.wav"]),
        ("cm_inn", ["*- Innkeep 0*", "*- Tavern 0*"]),
        ("cm_recruit", ["*- Okay.wav", "*- Got It.wav"]),
        ("cm_follow", ["*- Let's Go 0*", "*- Understood.wav"]),
        ("cm_dismiss", ["*- Goodbye 0*", "*- Good Night 0*"]),
        ("cm_combat", ["*- Watch Out 0*", "*- Be Careful 0*", "*- Come On 0*"]),
        ("cm_victory", ["*- I Win 0*", "*- You Lose 0*", "*- Laugh 0*"]),
        ("cm_dungeon", ["*- I Found Something.wav", "*- This Looks Important.wav", "*- It's Locked.wav",
                        "*- I Need a Key.wav"]),
        ("cm_bark", ["*- Humming 0*", "*- Oh 0*"]),
    ]),
    "va_adventurer_male": ("Male protagonist/Male protagonist", "git", [
        ("greet", ["Hello *", "Hey *", "Hi [0-9]*", "Howdy*", "Oh Helllo*"]),
        ("talk", ["Is That So*", "Now Then*", "What Should I do*", "What To Do*", "Alrighty*", "Thank You*", "Thanks*"]),
        ("attack", ["Hiyah*", "Punch*", "Kick*", "Grunt Ra*"]),
        ("hit", ["Ouch*", "Ow*", "Grunt Oof*", "Grunt Ah*", "Grunt Oh*"]),
        ("death", ["Pained Death*"]),
        ("cm_recruit", ["OK*"]),
        ("cm_dismiss", ["Bye *", "Good Bye*", "See Ya*", "See You*", "Fair Well*", "Another Time*", "When We Next Meet*",
                        "Bye Bye*"]),
        ("cm_push", ["No [0-9]*", "No No No*"]),
    ]),
    "va_adventurer_female": ("Female Voice Clips/Female Voice Clips/Audio", "git", [
        ("greet", ["Hello.wav", "Hey.wav", "Hi.wav", "Yo.wav", "Whats up.wav"]),
        ("talk", ["Definitely.wav", "Great.wav", "Hm.wav", "Huh.wav", "Never.wav", "No way.wav", "Not happening.wav",
                  "Ok.wav", "Sigh.wav", "Wow.wav", "Yeah.wav", "Yes.wav", "What.wav", "Im sorry.wav", "Sorry.wav",
                  "Laugh.wav"]),
        ("attack", ["split:Effort sounds.wav"]),
        ("hit", ["split:Damage sounds.wav"]),
        ("cm_recruit", ["You got it.wav", "Ready.wav", "On my way.wav"]),
        ("cm_follow", ["Lets go.wav", "Come on.wav"]),
        ("cm_dismiss", ["Bye.wav", "Cya.wav", "Goodbye.wav", "Later.wav", "Take Care.wav", "Good luck.wav"]),
        ("cm_victory", ["All Done!.wav"]),
        ("cm_push", ["No.wav"]),
    ]),
    "va_townswoman_female": ("Voice Pack by Elise Astoria/Voice Pack/OGG", "git", [
        ("greet", ["Hello.ogg", "Hey there.ogg", "Hi.ogg", "Good Morning*", "Good Afternoon*", "Mornin.ogg",
                   "Morning.ogg", "how are you.ogg", "Excuse me*"]),
        ("talk", ["Alright.ogg", "Do you think so.ogg", "For real.ogg", "Maybe.ogg", "Perhaps.ogg", "True.ogg",
                  "Well.ogg", "Mhm.ogg", "To tell you the truth.ogg", "Thats the jist of it.ogg",
                  "So that's how it is.ogg", "What do ya think.ogg", "Let me explain.ogg", "I'm heading home.ogg",
                  "Is something wrong.ogg", "Um.ogg"]),
        ("hit", ["small scream*"]),
        ("cm_recruit", ["OK.ogg", "Let's Do it.ogg", "Il give it a shot.ogg", "you got it.ogg"]),
        ("cm_follow", ["Are you ready.ogg", "come on.ogg"]),
        ("cm_dismiss", ["Bye.ogg", "Byee.ogg", "Good Bye.ogg", "See you later.ogg"]),
        ("cm_dungeon", ["this seems dangerous.ogg", "Is no one here.ogg", "What was that.ogg"]),
        ("cm_hurt", ["I can't take it anymore.ogg", "Oh i can't take it anymore.ogg"]),
        ("cm_bark", ["sigh*", "Laugh*", "Wanna look around.ogg", "Why don't we try.ogg"]),
        ("cm_push", ["How dare you.ogg", "what is wrong with you.ogg", "whatever.ogg"]),
    ]),
    "va_commoner_female": ("video_game_voices/female_generic_yvette", "git", [
        ("greet", ["greetings/*"]),
        ("halt", ["rally/stop*", "rally/wait*"]),
        ("cm_recruit", ["affirmative/ok*", "affirmative/yes*"]),
        ("talk", ["affirmative/*", "negative/*", "questioning/*", "other/mm*"]),
        ("hit", ["other/gasp*"]),
        ("cm_follow", ["rally/come_here*", "rally/go*", "rally/over_there*", "rally/now*"]),
        ("cm_dismiss", ["farewells/*"]),
        ("cm_hurt", ["pleading/*"]),
        ("cm_dungeon", ["other/oh_no*", "other/oops*"]),
        ("cm_bark", ["other/laugh*"]),
    ]),
    "va_commoner_male": ("video_game_voices/male_generic_peter", "git", [
        ("greet", ["greetings/*"]),
        ("cm_recruit", ["affirmative/*my_lord*", "affirmative/*sire*"]),
        ("talk", ["affirmative/*", "negative/*"]),
        ("cm_dismiss", ["farewells/*"]),
    ]),
    "va_cheerful_female": ("Rheia Voice Pack_MP3/Rheia Voice Pack_MP3", "git", [
        ("greet", ["happy/hello*", "happy/hey*", "happy/hi.*", "happy/hiya*"]),
        ("talk", ["indifferent/maybe*", "indifferent/no.*", "indifferent/yes*", "indifferent/yep*", "indifferent/yup*",
                  "indifferent/what*", "indifferent/when*", "indifferent/who*", "indifferent/why*", "indifferent/huh*",
                  "indifferent/hmm*", "indifferent/uh*", "indifferent/aha*", "indifferent/great*", "indifferent/wow*",
                  "indifferent/whew*"]),
        ("attack", ["indifferent/yah*", "happy/ha.*", "indifferent/ha.*"]),
        ("hit", ["indifferent/ouch*", "happy/doh*", "indifferent/doh*"]),
        ("cm_bark", ["random/singing*", "random/mumble*", "random/laugh*"]),
        ("cm_follow", ["happy/come_on*", "happy/come.*", "happy/hurry*", "happy/go_on*", "happy/go.*"]),
        ("cm_wait", ["indifferent/stay*"]),
        ("cm_dismiss", ["happy/bye*", "indifferent/bubye*", "indifferent/bye*"]),
        ("cm_push", ["indifferent/watch_it*", "indifferent/go_away*", "indifferent/nonono*", "indifferent/no_way*"]),
        ("cm_combat", ["encouragements/be careful*", "encouragements/we got this*", "encouragements/lets go*",
                       "encouragements/you can do it*"]),
        ("cm_victory", ["encouragements/we made it*", "encouragements/you did great*",
                        "encouragements/i knew you could*", "happy/awesome*"]),
        ("cm_dungeon", ["encouragements/we are so close*", "encouragements/were almost there*"]),
    ]),
    "va_fighter_female": ("RPG_Voice_Pack_2.0/Better Audio - ReRecorded version", "git", [
        ("attack", ["Battle_Grunt*"]),
        ("hit", ["Damage_Grunt*"]),
        ("death", ["Death*"]),
    ]),
    "va_brawler_male": ("GenericMale_VoicePack(1)", "local", [
        ("attack", ["Hiyah!*", "Hwah!*", "Yah!*", "Heavy Swing*"]),
        ("hit", ["Take Impact*", "Impact*"]),
        ("talk", ["Hmm*", "Huh*", "Uh huh*", "Umm*", "Ermm*", "Acknowledgement*", "Yeah_positive*", "Curious*",
                  "Hmpf*", "Dissapointment*"]),
        ("cm_bark", ["Clears Throat*", "Cough*", "Phew*", "Sigh of relief*"]),
        ("cm_hurt", ["Groaning*"]),
        ("cm_victory", ["Ha ha ha*", "Ha haa!*"]),
        ("cm_push", ["Frustra*"]),
    ]),
}

# Daniel Gooding's Unity Asset Store packs (bought; Asset Store EULA: never in git). One local pack per character of
# "Action RPG Characters" (combat, spells, companion lines by the file name's category), plus that actor's townsfolk
# greetings and merchant calls from "Merchants Enemies And Townsfolk".
ARPG = "Action RPG Characters/Assets/Action RPG Characters/Vocal Files"
MET = "Merchants Enemies And Townsfolk/Assets/Merchants_Enemies_Townsfolk"
GOODING = [  # (pack, ARPG character folder, merchants-pack actor folder, gender)
    ("va_clifford_male", "Clifford", "Clifford", "male"), ("va_edwyn_male", "Edwyn", "Edwyn", "male"),
    ("va_nile_male", "Nile", "Nile", "male"), ("va_samuel_male", "Samuel", "Samuel", "male"),
    ("va_jamaal_dark_male", "Jamaal/Dark", "Jamaal", "male"), ("va_jamaal_methodical_male", "Jamaal/Methodical", "Jamaal", "male"),
    ("va_jamaal_old_male", "Jamaal/Old", "Jamaal", "male"), ("va_jamaal_stout_male", "Jamaal/Stout", "Jamaal", "male"),
    ("va_jamaal_wimp_male", "Jamaal/Wimp", "Jamaal", "male"), ("va_jamaal_young_male", "Jamaal/Young", "Jamaal", "male"),
    ("va_patrick_cocky_male", "Patrick/Cocky", "Patrick", "male"), ("va_patrick_methodical_male", "Patrick/Methodical", "Patrick", "male"),
    ("va_karenk_female", "KarenK", "KarenK", "female"), ("va_kimlinh_female", "Kimlinh", "Kimlinh", "female"),
    ("va_rina_female", "Rina_Chan", "Rina", "female"),
    ("va_karenh_regal_female", "KarenH/Regal", "KarenH", "female"), ("va_karenh_young_female", "KarenH/Young", "KarenH", "female"),
]
# What each "Merchants Enemies And Townsfolk" clip says (Tools: faster-whisper, F:/_Projects/Dagerfall/tools/transcribe.py).
SAID = {}
_tsv = os.path.join(SRC, "Merchants Enemies And Townsfolk", "transcripts.tsv")
if os.path.exists(_tsv):
    with open(_tsv, encoding="utf-8") as _f:
        for _line in _f:
            _rel, _, _text = _line.rstrip("\n").partition("\t")
            SAID[MET + "/" + _rel] = _text
GREETING = re.compile(r"\b(hello|hi|hey|greetings|good (day|morning|evening)|salutations|welcome|how's it going|how are you|"
                      r"fine day|find day|passing through|well met)\b", re.I)
SWEAR = re.compile(r"fuck|shit|bitch", re.I)
NOT_HAWK = re.compile(r"off your hands|you've got there|part with|supplier|purchase|thank|come again|bye", re.I)


def said(prefix, keep):
    """Exact paths (as patterns) of transcribed clips under prefix whose text passes keep(text)."""
    return sorted(k.replace("[", "[[]") for k, t in SAID.items()
                  if k.startswith(prefix) and t.strip() and not SWEAR.search(t) and keep(t))


for _name, _char, _actor, _g in GOODING:
    c = ARPG + "/" + _char + "/*_"
    town = MET + "/Townsfolk/#General/Townsfolk/" + _actor + "/"
    chat = [MET + "/Townsfolk/#General/" + kind + "/" + _actor + "/" for kind in ("Guards", "Snobs", "Shady_Distrusting")]
    regional = sorted({k[:k.index("/" + _actor + "/") + len(_actor) + 2] for k in SAID
                       if k.startswith(MET + "/Townsfolk/") and "/#General/" not in k and "/" + _actor + "/" in k})
    talk = said(town, lambda t: not GREETING.search(t))
    for prefix in chat + regional:
        talk += [p for p in said(prefix, lambda t: True) if "GuardChild" not in p]
    PACKS[_name] = (".", "local", [
        ("greet", said(town, lambda t: GREETING.search(t))),
        ("talk", talk + ["dup:" + c + "Idle*"]),
        ("hawk", said(MET + "/Merchants/", lambda t: not NOT_HAWK.search(t)) and
         [p for p in said(MET + "/Merchants/", lambda t: not NOT_HAWK.search(t)) if "/" + _actor + "/" in p]),
        ("attack", [c + "Attack*", c + "CriticalHit*"]),
        ("hit", [c + "Hurt*"]),
        ("death", [c + "Death*", MET + "/Enemies/Bandits/" + _actor + "/*BanditDeath*"]),
        ("cast", [c + "ChargeUp*", c + "Mage*"]),
        ("heal", [c + "Healer*"]),
        ("cm_bark", ["dup:" + c + "Idle*", c + "Discovering*", c + "Happy*"]),
        ("cm_recruit", [c + "JoinParty*"]),
        ("cm_combat", [c + "BattleStart*", c + "EnteringDangerArea*", c + "Rage*"]),
        ("cm_victory", [c + "BattleWon*", c + "BattleOver*", c + "DungeonCleared*", c + "ClearingDungeon*", c + "Laughter*"]),
        ("cm_dungeon", [c + "EnteringDungeon*", c + "EnteringADungeon*", c + "EnterDungeon*"]),
        ("cm_hurt", [c + "Distraught*", c + "Sad*", c + "Confused*", c + "DissapointedAngry*", c + "Poison*",
                     c + "Blind*", c + "Fright*"]),
    ])

AUDIO = (".wav", ".ogg", ".mp3")


def decode(path):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-map", "0:a:0", "-ac", "1", "-ar", str(RATE),
                          "-f", "f32le", "-"], capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).copy()


def trim(x):
    """Silence at both ends cut (below -40 dB of the peak), 40 ms kept, 10 ms fades; None when silent."""
    peak = np.abs(x).max() if len(x) else 0
    if peak < 1e-4:
        return None
    loud = np.nonzero(np.abs(x) > peak * 0.01)[0]
    pad = int(0.04 * RATE)
    x = x[max(0, loud[0] - pad):min(len(x), loud[-1] + pad)].copy()
    fade = min(int(0.01 * RATE), len(x) // 4)
    if fade > 0:
        ramp = np.linspace(0, 1, fade, dtype=np.float32)
        x[:fade] *= ramp
        x[-fade:] *= ramp[::-1]
    return x


def split(x):
    """Pieces between pauses of 250 ms or more (below -35 dB of the peak); pieces under 120 ms dropped."""
    peak = np.abs(x).max()
    win = int(0.01 * RATE)
    frames = len(x) // win
    loud = np.abs(x[:frames * win]).reshape(frames, win).max(axis=1) > peak * 0.018
    pieces, start, quiet = [], None, 0
    for i, on in enumerate(loud):
        if on:
            if start is None:
                start = i
            quiet = 0
        elif start is not None:
            quiet += 1
            if quiet >= 25:
                pieces.append((start, i - quiet + 1))
                start, quiet = None, 0
    if start is not None:
        pieces.append((start, frames))
    return [x[a * win:b * win] for a, b in pieces if (b - a) >= 12]


def encode(x, out):
    x = x * (0.89 / np.abs(x).max())
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(RATE), "-ac", "1", "-i", "-",
                    "-c:a", "libvorbis", "-q:a", "4", out], input=x.astype(np.float32).tobytes(), check=True)


def build(name):
    folder, where, pools = PACKS[name]
    src = os.path.join(SRC, folder)
    walk = [src] if folder != "." else [os.path.join(SRC, ARPG), os.path.join(SRC, MET)]      # not every pack
    files = sorted(os.path.relpath(os.path.join(d, f), src).replace("\\", "/")
                   for w in walk for d, _, fs in os.walk(w) for f in fs if f.lower().endswith(AUDIO))
    out = os.path.join(GIT_OUT if where == "git" else LOCAL_OUT, name)
    shutil.rmtree(out, ignore_errors=True)
    os.makedirs(out)
    used, summary = set(), []
    for pool, patterns in pools:
        clips = []
        for pattern in patterns:
            cut = pattern.startswith("split:")
            dup = pattern.startswith("dup:")     # may also be in another pool
            pattern = pattern[6:] if cut else pattern[4:] if dup else pattern
            hits = [f for f in files if (dup or f not in used) and fnmatch.fnmatch(f.lower(), pattern.lower())]
            if not hits:
                print("  %s: %s matches nothing" % (pool, pattern))
            for f in hits:
                if not dup:
                    used.add(f)
                x = decode(os.path.join(src, f))
                clips.extend(split(x) if cut else [x])
        n = 0
        for x in clips:
            x = trim(x)
            if x is None or n >= MAX_PER_POOL:
                continue
            n += 1
            encode(x, os.path.join(out, "%s_%d.ogg" % (pool, n)))
        summary.append("%s %d" % (pool, n))
    print("%s (%s): %s" % (name, where, ", ".join(summary)))


if __name__ == "__main__":
    for name in sys.argv[1:] or sorted(PACKS):
        build(name)
