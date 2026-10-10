Custom name lists for ANPCs. Put one list per .json file here and use it from an npc.json:
  "nameList": "pirates"          -> _Namelists/pirates.json
Without "nameList" a person gets a name from the vanilla list of its race. Vanilla lists can also be chosen
by name: default_breton, default_redguard, default_nord, default_darkelf, default_highelf, default_woodelf,
default_khajiit, default_imperial.

Two formats:

1. Vanilla format: one bank copied from DFU's NameGen.txt plus the style that glues the parts.
   breton/darkelf/highelf/woodelf/khajiit/imperial need 6 sets, nord 4 (surname = sets 0+1 + "sen"), redguard 5.
   {
     "style": "nord",
     "sets": [
       { "parts": ["Bjorn", "Ulf"] }, { "parts": ["ar", "rik"] },
       { "parts": ["Astr", "Sig"] }, { "parts": ["id", "run"] }
     ]
   }

2. Simple format: whole names. A missing gender list uses the other one; surnames are optional.
   {
     "male": ["Ragnar", "Ulf"],
     "female": ["Astrid", "Sigrid"],
     "surnames": ["Stormborn", "Ironhand"]
   }
   Optional "maleSurnamePrefix" / "femaleSurnamePrefix" go in front of the surname by gender, e.g. Orsimer
   "gro-" (son of) and "gra-" (daughter of): "Gharol gro-Rugdush", "Shel gra-Rugdush".

Ready-made lists in this folder: orsimer.json ("nameList": "orsimer"), argonian.json ("nameList": "argonian"), and dunmer_morrowind.json ("nameList": "dunmer_morrowind").
The Dunmer list supplies Morrowind-style male/female given names and shared family names.

Names starting with default_ are reserved. Problems are reported in Player.log.

The expanded Dunmer list includes shorter and harsher given names, names with Ll-/Hl-
clusters, and a wider range of family-name endings. Existing entries are retained.

Breton Oblivion/Skyrim-style list: breton_oblivion_skyrim.json. Use
  "nameList": "breton_oblivion_skyrim"
157 male names, 159 female names and 212 shared surnames.
