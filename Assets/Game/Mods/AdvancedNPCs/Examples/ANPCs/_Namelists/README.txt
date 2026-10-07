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

Names starting with default_ are reserved. Problems are reported in Player.log.
