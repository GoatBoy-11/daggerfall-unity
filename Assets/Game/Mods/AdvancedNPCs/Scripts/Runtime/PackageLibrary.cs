using System;
using System.Collections.Generic;
using System.IO;
using AdvancedNPCs.Core;

namespace AdvancedNPCs
{
    /// <summary>Reusable package data. Replaced atomically on reload; no executable files are loaded.</summary>
    public class PackageLibrary
    {
        public const string FolderName = "_Packages";
        readonly Dictionary<string, AiPackage> packages = new Dictionary<string, AiPackage>(StringComparer.Ordinal);
        public int Count
        {
            get
            {
                int count = 0;
                foreach (AiPackage p in packages.Values) if (p != null) count++;
                return count;
            }
        }
        internal void AddForTest(AiPackage package) { packages[package.Id] = package; }
        internal void RemoveForTest(string id) { packages.Remove(id); }
        public AiPackage Find(string id)
        {
            AiPackage package;
            return id != null && packages.TryGetValue(id, out package) ? package : null;
        }

        public static PackageLibrary Load(string root)
        {
            PackageLibrary library = new PackageLibrary();
            string folder = Path.Combine(root, FolderName);
            if (!Directory.Exists(folder)) return library;
            try
            {
                string[] paths = Directory.GetFiles(folder);
                Array.Sort(paths, StringComparer.Ordinal);
                foreach (string path in paths)
                {
                    if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase)) continue;
                    string id = Path.GetFileNameWithoutExtension(path);
                    try
                    {
                        string problem;
                        AiPackage package = PackageParser.Parse(id, File.ReadAllText(path), out problem);
                        if (package == null) AdvancedNpcsMod.LogError(FolderName + "/" + Path.GetFileName(path) + ": " + problem);
                        else if (library.packages.ContainsKey(id))
                        {
                            library.packages[id] = null;
                            AdvancedNpcsMod.LogError(FolderName + "/" + id + ": duplicate package id; disabled");
                        }
                        else library.packages.Add(id, package);
                    }
                    catch (Exception e) { AdvancedNpcsMod.LogError(FolderName + "/" + Path.GetFileName(path) + ": " + e.Message); }
                }
            }
            catch (Exception e) { AdvancedNpcsMod.LogError(FolderName + ": " + e.Message); }
            return library;
        }

        public List<string> AnimationRequests(NpcDefinition definition)
        {
            List<string> names = new List<string>();
            if (definition.Schedule == null) return names;
            foreach (AiScheduleEntry entry in definition.Schedule)
            {
                string problem;
                PackagePlan plan = PackagePlan.Build(Find(entry.Package),entry.Parameters,Find,out problem);
                if (plan == null) continue;
                foreach (var step in plan.Steps)
                {
                    PackageSettings settings = step.Settings;
                    if (settings.Type != "play_animation" && settings.Type != "work_at") continue;
                    if (!names.Exists(delegate(string n) { return string.Equals(n, settings.Animation, StringComparison.OrdinalIgnoreCase); }))
                        names.Add(settings.Animation);
                }
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        public void Check(NpcDefinition definition)
        {
            if (definition.Schedule == null) return;
            for (int i = 0; i < definition.Schedule.Count; i++)
            {
                AiScheduleEntry entry = definition.Schedule[i];
                string problem;
                if (PackagePlan.Build(Find(entry.Package), entry.Parameters, Find, out problem) == null)
                    AdvancedNpcsMod.LogError(definition.SourceFile + ": schedule entry " + (i + 1) + " (" + entry.Package + "): " + problem + "; current AI will be used");
            }
        }
    }
}
