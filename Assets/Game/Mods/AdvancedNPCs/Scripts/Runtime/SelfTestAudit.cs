using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;

namespace AdvancedNPCs
{
    public partial class SelfTest
    {
        public const string AuditSuite = "audit";
        static readonly BindingFlags AuditHidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        static List<Texture2D> AuditNewTextures(HashSet<int> before)
        {
            var found = new List<Texture2D>();
            foreach (var texture in Resources.FindObjectsOfTypeAll<Texture2D>())
                if (!before.Contains(texture.GetInstanceID())) found.Add(texture);
            return found;
        }
        static HashSet<int> AuditTextures()
        {
            var found = new HashSet<int>();
            foreach (var texture in Resources.FindObjectsOfTypeAll<Texture2D>()) found.Add(texture.GetInstanceID());
            return found;
        }
        static int AuditAlive(List<Texture2D> textures)
        {
            int count = 0;
            foreach (var texture in textures) if (texture != null) count++;
            return count;
        }
        static int AuditHandlers(object control, string name, object owner)
        {
            for (Type type = control.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, AuditHidden | BindingFlags.DeclaredOnly);
                if (field == null) continue;
                var handlers = field.GetValue(control) as Delegate;
                int count = 0;
                if (handlers != null) foreach (Delegate handler in handlers.GetInvocationList()) if (handler.Target == owner) count++;
                return count;
            }
            return -1;
        }
        IEnumerator AuditSteps(DaggerfallLocation location, Transform player)
        {
            string root = Path.Combine(Application.temporaryCachePath, "anpc_audit_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var owned = new List<Texture2D>();
            GameObject host = null;
            AudioClip clip = null;
            try
            {
                var sample = TestPortrait(); owned.Add(sample);
                byte[] png = sample.EncodeToPNG();
                // Warm Unity's PNG decoder before measuring native texture ownership.
                var warm = new Texture2D(2, 2); warm.LoadImage(png); UnityEngine.Object.Destroy(warm);
                yield return null;
                string portraits = Path.Combine(root, "portraits"); Directory.CreateDirectory(portraits);
                string bad = Path.Combine(portraits, "bad.png"); File.WriteAllBytes(bad, new byte[] { 1, 2, 3, 4 });
                var before = AuditTextures();
                var library = PortraitLibrary.Load(portraits);
                var created = AuditNewTextures(before); owned.AddRange(created);
                yield return null; yield return null;
                Check("audit invalid portrait is rejected without native texture leak", library.Count == 0 && AuditAlive(created) == 0, "alive=" + AuditAlive(created));
                byte[] truncated = new byte[24]; Array.Copy(png, truncated, truncated.Length); File.WriteAllBytes(bad, truncated);
                before = AuditTextures(); library = PortraitLibrary.Load(portraits);
                created = AuditNewTextures(before); owned.AddRange(created);
                yield return null; yield return null;
                Check("audit truncated portrait is rejected without leak", library.Count == 0 && AuditAlive(created) == 0, "alive=" + AuditAlive(created));
                File.Delete(bad);
                string valid = Path.Combine(portraits, "hero.png"); File.WriteAllBytes(valid, png);
                before = AuditTextures();
                using (var locked = new FileStream(valid, FileMode.Open, FileAccess.Read, FileShare.None)) library = PortraitLibrary.Load(portraits);
                created = AuditNewTextures(before); owned.AddRange(created);
                yield return null; yield return null;
                Check("audit unreadable portrait leaves no native texture", library.Count == 0 && AuditAlive(created) == 0, "alive=" + AuditAlive(created));
                File.WriteAllBytes(Path.Combine(portraits, " hero.png"), png);
                before = AuditTextures(); library = PortraitLibrary.Load(portraits);
                created = AuditNewTextures(before); owned.AddRange(created);
                yield return null; yield return null;
                Check("audit normalized portrait duplicates retain one texture", library.Count == 1 && AuditAlive(created) == 1, "alive=" + AuditAlive(created));
                var portrait = library.Get("HERO.PNG");
                Check("audit valid portrait keeps pixels and point filter", portrait != null && portrait.width == sample.width && portrait.filterMode == FilterMode.Point, "portrait=" + portrait);

                var load = typeof(SpriteLibrary).GetMethod("LoadPng", BindingFlags.Static | BindingFlags.NonPublic);
                before = AuditTextures();
                Texture2D sheet;
                using (var locked = new FileStream(valid, FileMode.Open, FileAccess.Read, FileShare.None)) sheet = (Texture2D)load.Invoke(null, new object[] { "audit locked sheet", valid, true });
                created = AuditNewTextures(before); owned.AddRange(created);
                yield return null; yield return null;
                Check("audit unreadable sprite leaves no native texture", sheet == null && AuditAlive(created) == 0, "alive=" + AuditAlive(created));
                File.WriteAllBytes(bad, new byte[] { 1, 2, 3, 4 }); before = AuditTextures();
                sheet = (Texture2D)load.Invoke(null, new object[] { "audit invalid sheet", bad, true });
                created = AuditNewTextures(before); owned.AddRange(created);
                yield return null; yield return null;
                Check("audit invalid sprite is rejected without leak", sheet == null && AuditAlive(created) == 0, "alive=" + AuditAlive(created));
                File.WriteAllBytes(bad, truncated); before = AuditTextures();
                sheet = (Texture2D)load.Invoke(null, new object[] { "audit truncated sheet", bad, true });
                created = AuditNewTextures(before); owned.AddRange(created);
                yield return null; yield return null;
                Check("audit truncated sprite is rejected without leak", sheet == null && AuditAlive(created) == 0, "alive=" + AuditAlive(created));
                sheet = (Texture2D)load.Invoke(null, new object[] { "audit valid sheet", valid, true }); owned.Add(sheet);
                yield return null;
                Check("audit valid sprite remains alive for its owner", sheet != null && sheet.width == sample.width && sheet.name == "audit valid sheet", "sheet=" + sheet);

                string audio = Path.Combine(root, "voice # 50% ü.wav"); File.WriteAllBytes(audio, ToneWav(.1f));
                string problem;
                clip = SoundLibrary.LoadClip(audio, out problem);
                Check("audit audio paths encode spaces fragments percent and Unicode", clip != null && clip.length > 0, problem);
                var unknown = SoundLibrary.LoadClip(Path.Combine(root, "not-audio.txt"), out problem);
                Check("audit unsupported audio is rejected", unknown == null && !string.IsNullOrEmpty(problem), problem);

                // Two cached windows expose duplicate delegates without relying on pixel coordinates.
                host = new GameObject("ANPC audit topic lifetime"); var injector = host.AddComponent<TopicInjector>();
                var a = new DaggerfallTalkWindow(DaggerfallUI.UIManager); var b = new DaggerfallTalkWindow(DaggerfallUI.UIManager);
                var listA = new ListBox(); var listB = new ListBox(); var okA = new Button(); var okB = new Button();
                var listField = typeof(DaggerfallTalkWindow).GetField("listboxTopic", AuditHidden);
                var okField = typeof(DaggerfallTalkWindow).GetField("buttonOkay", AuditHidden);
                listField.SetValue(a, listA); listField.SetValue(b, listB); okField.SetValue(a, okA); okField.SetValue(b, okB);
                var current = typeof(TopicInjector).GetField("window", AuditHidden);
                var hook = typeof(TopicInjector).GetMethod("Hook", AuditHidden);
                current.SetValue(injector, a); hook.Invoke(injector, null);
                Check("audit active dialogue has one callback per control", AuditHandlers(listA, "OnUseSelectedItem", injector) == 1 && AuditHandlers(okA, "OnMouseClick", injector) == 1, "initial hooks");
                current.SetValue(injector, b); hook.Invoke(injector, null);
                Check("audit switching dialogue releases previous controls", AuditHandlers(listA, "OnUseSelectedItem", injector) == 0 && AuditHandlers(okA, "OnMouseClick", injector) == 0, "old hooks");
                current.SetValue(injector, a); hook.Invoke(injector, null);
                Check("audit revisiting cached dialogue never duplicates actions", AuditHandlers(listA, "OnUseSelectedItem", injector) == 1 && AuditHandlers(okA, "OnMouseClick", injector) == 1, "revisit hooks");
                injector.End();
                Check("audit ending dialogue releases every cached control", AuditHandlers(listA, "OnUseSelectedItem", injector) == 0 && AuditHandlers(listB, "OnUseSelectedItem", injector) == 0 && AuditHandlers(okA, "OnMouseClick", injector) == 0 && AuditHandlers(okB, "OnMouseClick", injector) == 0, "ended hooks");
                current.SetValue(injector, a); hook.Invoke(injector, null); injector.enabled = false;
                Check("audit disabling injector ends dialogue and detaches controls", !injector.Active && AuditHandlers(listA, "OnUseSelectedItem", injector) == 0 && AuditHandlers(okA, "OnMouseClick", injector) == 0, "disabled hooks");
            }
            finally
            {
                if (host != null) { host.GetComponent<TopicInjector>().End(); UnityEngine.Object.Destroy(host); }
                foreach (var texture in owned) if (texture != null) UnityEngine.Object.Destroy(texture);
                if (clip != null) UnityEngine.Object.Destroy(clip);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
