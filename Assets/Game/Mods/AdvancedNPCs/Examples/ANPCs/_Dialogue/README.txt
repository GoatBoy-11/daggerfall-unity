Dialogue types: "Tell Me About" topics that many ANPCs can share.
Each .json file here is one type; its file name (without .json) is the type's name:
  _Dialogue/tavern_wench.json   ->   "dialogue": "tavern_wench"   in an ANPC's npc.json

Smallest file:
  { "topics": [ { "caption": "The house ale", "answers": ["Two coppers a mug."] } ] }

tavern_wench.json shows every feature (greetings, tone answers, follow-ups, flags, conditions).
After editing, type anpc_reload_dialogue in the console and talk again; anpc_topics shows why a topic is hidden.
Full guide: the mod's README, section "Dialogue topics". Problems are listed in Player.log as [AdvancedNPCs] lines.
