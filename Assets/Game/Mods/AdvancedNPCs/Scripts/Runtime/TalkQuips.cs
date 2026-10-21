using System;
using System.Collections.Generic;
using System.Reflection;
using DaggerfallWorkshop.Game;

namespace AdvancedNPCs
{
    /// <summary>
    /// Wayside Denizens (Daggerfall Living World) closes most talk windows and shows a one-line quip instead, on every
    /// window change that leaves a talk window on top (also when a reply picker over it closes). An ANPC conversation is
    /// the whole point of clicking one, so its quips stay off from opening until the talk window is gone.
    /// </summary>
    static class TalkQuips
    {
        const string DenizensDirector = "WaysideDenizensMod.StandardNpcQuipDirector";

        class Muted
        {
            public UnityEngine.Object Director;
            public FieldInfo Field;
            public object Was;
        }

        static string extraDirector;     // the self-test's stand-in
        static bool searched;
        static readonly List<KeyValuePair<Type, FieldInfo>> directors = new List<KeyValuePair<Type, FieldInfo>>();
        static readonly List<Muted> muted = new List<Muted>();
        static bool watching;

        /// <summary>Also mutes this quip director type (the self-test's stand-in); null for none. Returns the previous one.</summary>
        internal static string Use(string typeName)
        {
            string was = extraDirector;
            extraDirector = typeName;
            searched = false;
            return was;
        }

        /// <summary>Opens a talk window with quips off; they come back once the talk window has closed.</summary>
        public static void Open(Action open)
        {
            Find();
            if (muted.Count == 0)
            {
                foreach (KeyValuePair<Type, FieldInfo> d in directors)
                {
                    UnityEngine.Object director = UnityEngine.Object.FindObjectOfType(d.Key);
                    if (director == null)
                        continue;
                    muted.Add(new Muted { Director = director, Field = d.Value, Was = d.Value.GetValue(director) });
                    d.Value.SetValue(director, false);
                }
            }
            try { open(); }
            finally
            {
                if (muted.Count > 0 && !watching)
                {
                    DaggerfallUI.UIManager.OnWindowChange += Watch;
                    watching = true;
                }
                if (!TalkOpen())
                    Restore();
            }
        }

        static void Watch(object sender, EventArgs e)
        {
            if (!TalkOpen())
                Restore();
        }

        static bool TalkOpen()
        {
            return DaggerfallUI.Instance.TalkWindow != null && DaggerfallUI.UIManager.ContainsWindow(DaggerfallUI.Instance.TalkWindow);
        }

        static void Restore()
        {
            foreach (Muted m in muted)
                if (m.Director != null)
                    m.Field.SetValue(m.Director, m.Was);
            muted.Clear();
            if (watching)
            {
                DaggerfallUI.UIManager.OnWindowChange -= Watch;
                watching = false;
            }
        }

        static void Find()
        {
            if (searched)
                return;
            searched = true;
            directors.Clear();
            foreach (string name in new[] { DenizensDirector, extraDirector })
            {
                if (string.IsNullOrEmpty(name))
                    continue;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type type = assembly.GetType(name, false);
                    if (type == null)
                        continue;
                    FieldInfo field = type.GetField("enableQuips", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null && field.FieldType == typeof(bool))
                        directors.Add(new KeyValuePair<Type, FieldInfo>(type, field));
                    if (name == DenizensDirector)
                        AdvancedNpcsMod.Log(field != null ? "Wayside Denizens found: its quips stay out of ANPC conversations."
                            : "Wayside Denizens found, but not its enableQuips switch: its quips may close ANPC talk windows.");
                    break;
                }
            }
        }
    }
}
