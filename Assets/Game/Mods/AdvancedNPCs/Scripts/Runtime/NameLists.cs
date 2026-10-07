using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop.Game.Utility;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>
    /// All name lists (spec v2.1 §5): DFU's own banks as default_&lt;race&gt; and the author's ANPCs/_Namelists/*.json.
    /// Names are built by the Core generator from each person's seed, so they repeat exactly in same-people mode.
    /// </summary>
    public class NameLists : INameSource
    {
        public const string FolderName = "_Namelists";
        const string DefaultPrefix = "default_";

        readonly Dictionary<string, NameList> lists = new Dictionary<string, NameList>(StringComparer.Ordinal);
        readonly HashSet<string> warned = new HashSet<string>();
        string nordSuffix;

        public static NameLists Load(string folder)
        {
            NameLists names = new NameLists();
            names.LoadVanilla();
            names.LoadCustom(folder);
            return names;
        }

        /// <summary>Adds or replaces a list (self-test).</summary>
        public void Add(NameList list)
        {
            if (list != null)
                lists[list.Name] = list;
        }

        public string Generate(string listName, string race, string gender, uint seed)
        {
            NameList list = Find(listName, race);
            if (list == null)
                return "Stranger";
            return NameGenerator.Generate(list, gender, new SeededRandom(seed), NordSuffix());
        }

        NameList Find(string listName, string race)
        {
            NameList list;
            if (lists.TryGetValue(listName, out list))
                return list;
            string fallback = DefaultPrefix + (race == null ? "breton" : race.ToLowerInvariant());
            if (warned.Add(listName))
                AdvancedNpcsMod.Log("nameList \"" + listName + "\": not found; using " + fallback + ".");
            if (lists.TryGetValue(fallback, out list) || lists.TryGetValue(DefaultPrefix + "breton", out list))
                return list;
            return null;
        }

        string NordSuffix()
        {
            if (nordSuffix == null)
                nordSuffix = TextManager.Instance != null ? TextManager.Instance.GetLocalizedText("nordSurnameImmutableSuffix") : "sen";
            return nordSuffix;
        }

        /// <summary>DFU's NameGen.txt, read the way NameHelper reads it (the file is not strict JSON).</summary>
        void LoadVanilla()
        {
            try
            {
                string text = null;
                TextAsset asset = Resources.Load<TextAsset>("NameGen");
                if (asset != null)
                    text = asset.text;
                string replacement = Path.Combine(Path.Combine(Application.streamingAssetsPath, "Text"), "NameGen.txt");
                if (File.Exists(replacement))
                    text = File.ReadAllText(replacement);
                if (text == null)
                {
                    AdvancedNpcsMod.LogError("NameGen.txt not found; only custom name lists are available.");
                    return;
                }

                Dictionary<NameHelper.BankTypes, NameHelper.NameBank> banks = SaveLoadManager.Deserialize(
                    typeof(Dictionary<NameHelper.BankTypes, NameHelper.NameBank>), text) as Dictionary<NameHelper.BankTypes, NameHelper.NameBank>;
                if (banks == null)
                {
                    AdvancedNpcsMod.LogError("NameGen.txt could not be read; only custom name lists are available.");
                    return;
                }
                List<string> vanilla = new List<string>();
                foreach (KeyValuePair<NameHelper.BankTypes, NameHelper.NameBank> bank in banks)
                {
                    string style = bank.Key.ToString().ToLowerInvariant();
                    if (NameListParser.RequiredSets(style) == 0 || bank.Value.sets == null)
                        continue; // monster banks
                    NameList list = new NameList();
                    list.Name = DefaultPrefix + style;
                    foreach (NameHelper.NameSet set in bank.Value.sets)
                        list.Sets.Add(set.parts ?? new string[0]);
                    if (list.Sets.Count < NameListParser.RequiredSets(style))
                        continue;
                    list.Style = style;
                    lists[list.Name] = list;
                    vanilla.Add(list.Name);
                }
                AdvancedNpcsMod.Log("Loaded " + vanilla.Count + " vanilla name list(s): " + string.Join(", ", vanilla.ToArray()) + ".");
            }
            catch (Exception e)
            {
                AdvancedNpcsMod.LogError("NameGen.txt could not be read (" + e.Message + "); only custom name lists are available.");
            }
        }

        void LoadCustom(string folder)
        {
            if (!Directory.Exists(folder))
                return;
            int count = 0;
            foreach (string path in Directory.GetFiles(folder, "*.json"))
            {
                string name = NameListParser.Normalize(Path.GetFileName(path));
                if (name.StartsWith(DefaultPrefix, StringComparison.Ordinal))
                {
                    AdvancedNpcsMod.Log(FolderName + "/" + Path.GetFileName(path) + ": file: names starting with default_ are reserved, ignored");
                    continue;
                }
                try
                {
                    NameListResult result = NameListParser.Parse(name, File.ReadAllText(path));
                    if (result.Error != null)
                    {
                        AdvancedNpcsMod.LogError(result.Error);
                        continue;
                    }
                    lists[name] = result.List;
                    count++;
                }
                catch (Exception e)
                {
                    AdvancedNpcsMod.LogError(FolderName + "/" + Path.GetFileName(path) + ": file: could not read (" + e.Message + ")");
                }
            }
            AdvancedNpcsMod.Log("Loaded " + count + " custom name list(s).");
        }
    }
}
