using System;
using System.Collections.Generic;
using System.Globalization;

namespace AdvancedNPCs.Core
{
    [Serializable]
    public class NpcVariableValue
    {
        public bool isBoolean;
        public bool booleanValue;
        public int integerValue;
        public NpcVariableValue() { }
        public NpcVariableValue(bool value) { isBoolean = true; booleanValue = value; }
        public NpcVariableValue(int value) { integerValue = value; }
        public NpcVariableValue Clone() { return isBoolean ? new NpcVariableValue(booleanValue) : new NpcVariableValue(integerValue); }
        public override string ToString() { return isBoolean ? (booleanValue ? "true" : "false") : integerValue.ToString(CultureInfo.InvariantCulture); }
        public static NpcVariableValue Parse(object raw)
        {
            if (raw is bool) return new NpcVariableValue((bool)raw);
            if (!(raw is double)) return null;
            double n = (double)raw;
            return n == Math.Floor(n) && n >= int.MinValue && n <= int.MaxValue ? new NpcVariableValue((int)n) : null;
        }
    }

    public interface INpcVariableFacts { bool TryGetVariable(string name, out NpcVariableValue value); }
    public interface INpcVariableActions
    {
        void SetVariable(string name, NpcVariableValue value);
        void AddVariable(string name, int amount);
    }

    /// <summary>Definition defaults plus explicit saved overrides belonging to one instance key.</summary>
    public static class NpcVariables
    {
        public const int MaxCount = 64;
        public static bool ValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 64 || name[0] < 'a' || name[0] > 'z') return false;
            foreach (char c in name) if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_') return false;
            return true;
        }
        public static Dictionary<string, NpcVariableValue> Read(object raw, bool integersOnly, out string error)
        {
            error = null;
            var o = raw as Dictionary<string, object>;
            if (o == null || o.Count > MaxCount) { error = "must be an object of at most 64 named values"; return null; }
            var values = new Dictionary<string, NpcVariableValue>(StringComparer.Ordinal);
            foreach (var pair in o)
            {
                if (!ValidName(pair.Key)) { error = "invalid variable name " + pair.Key + " (lowercase letters, digits and underscores; letter first, max 64 characters)"; return null; }
                NpcVariableValue value = NpcVariableValue.Parse(pair.Value);
                if (value == null || (integersOnly && value.isBoolean))
                { error = pair.Key + ": must be " + (integersOnly ? "a signed 32-bit integer" : "a boolean or signed 32-bit integer"); return null; }
                values.Add(pair.Key, value);
            }
            return values;
        }
        public static bool TryGet(NpcDefinition def, NpcState state, string name, out NpcVariableValue value)
        {
            value = null;
            NpcVariableValue initial;
            if (!ValidName(name) || def == null || def.Variables == null || !def.Variables.TryGetValue(name, out initial)) return false;
            NpcVariableValue saved;
            if (state != null && state.variables != null && state.variables.TryGetValue(name, out saved))
            {
                if (saved == null || saved.isBoolean != initial.isBoolean) return false;
                value = saved;
            }
            else value = initial;
            return true;
        }
        public static bool Set(NpcDefinition def, NpcState state, string name, NpcVariableValue value, out string error)
        {
            error = null;
            NpcVariableValue initial;
            if (!ValidName(name) || state == null || def == null || def.Variables == null || !def.Variables.TryGetValue(name, out initial))
            { error = "unknown NPC variable " + name; return false; }
            if (value == null || value.isBoolean != initial.isBoolean)
            { error = name + ": expected " + (initial.isBoolean ? "boolean" : "integer"); return false; }
            if (state.variables == null) state.variables = new Dictionary<string, NpcVariableValue>(StringComparer.Ordinal);
            state.variables[name] = value.Clone();
            return true;
        }
        public static bool Add(NpcDefinition def, NpcState state, string name, int amount, out string error)
        {
            error = null;
            NpcVariableValue current;
            if (!TryGet(def, state, name, out current)) { error = "unknown or incompatible NPC variable " + name; return false; }
            if (current.isBoolean) { error = name + ": can only add to an integer"; return false; }
            long next = (long)current.integerValue + amount;
            if (next < int.MinValue || next > int.MaxValue) { error = name + ": integer overflow; value unchanged"; return false; }
            return Set(def, state, name, new NpcVariableValue((int)next), out error);
        }
        public static Dictionary<string, NpcVariableValue> Clone(Dictionary<string, NpcVariableValue> values)
        {
            if (values == null) return null;
            var copy = new Dictionary<string, NpcVariableValue>(StringComparer.Ordinal);
            foreach (var p in values) if (p.Key != null) copy[p.Key] = p.Value != null ? p.Value.Clone() : null;
            return copy;
        }
    }

    public class NpcVariableTest
    {
        public string Name, Operator;
        public NpcVariableValue Expected;
    }

    /// <summary>All named tests must hold. Missing variables and wrong types never match, including ne.</summary>
    public class NpcVariableCondition
    {
        public static readonly string[] Operators = { "eq", "ne", "gt", "gte", "lt", "lte" };
        public readonly List<NpcVariableTest> Tests = new List<NpcVariableTest>();
        public static NpcVariableCondition Read(object raw, out string error)
        {
            error = null;
            var values = raw as Dictionary<string, object>;
            if (values == null || values.Count == 0 || values.Count > NpcVariables.MaxCount)
            { error = "variables must be an object of 1-64 named tests"; return null; }
            var condition = new NpcVariableCondition();
            foreach (var p in values)
            {
                if (!NpcVariables.ValidName(p.Key)) { error = "invalid variable name " + p.Key; return null; }
                var comparisons = p.Value as Dictionary<string, object>;
                if (comparisons == null)
                {
                    var value = NpcVariableValue.Parse(p.Value);
                    if (value == null) { error = p.Key + ": expected boolean, integer or comparison object"; return null; }
                    condition.Tests.Add(new NpcVariableTest {Name=p.Key, Operator="eq", Expected=value});
                }
                else
                {
                    if (comparisons.Count == 0) { error = p.Key + ": empty comparison"; return null; }
                    foreach (var comparison in comparisons)
                    {
                        var value = NpcVariableValue.Parse(comparison.Value);
                        if (Array.IndexOf(Operators, comparison.Key) < 0 || value == null ||
                            (value.isBoolean && comparison.Key != "eq" && comparison.Key != "ne"))
                        { error = p.Key + ": invalid " + comparison.Key + " comparison"; return null; }
                        condition.Tests.Add(new NpcVariableTest {Name=p.Key, Operator=comparison.Key, Expected=value});
                    }
                }
            }
            return condition;
        }
        public string CheckDefinition(NpcDefinition def)
        {
            foreach (var t in Tests)
            {
                NpcVariableValue initial;
                if (!def.Variables.TryGetValue(t.Name, out initial)) return "unknown variable " + t.Name;
                if (initial.isBoolean != t.Expected.isBoolean) return t.Name + ": condition type differs from declaration";
            }
            return null;
        }
        public bool Holds(INpcVariableFacts facts)
        {
            if (facts == null) return false;
            foreach (var t in Tests)
            {
                NpcVariableValue value;
                if (!facts.TryGetVariable(t.Name, out value) || value == null || value.isBoolean != t.Expected.isBoolean) return false;
                int c = value.isBoolean ? value.booleanValue.CompareTo(t.Expected.booleanValue) : value.integerValue.CompareTo(t.Expected.integerValue);
                switch (t.Operator)
                {
                    case "eq": if (c != 0) return false; break;
                    case "ne": if (c == 0) return false; break;
                    case "gt": if (c <= 0) return false; break;
                    case "gte": if (c < 0) return false; break;
                    case "lt": if (c >= 0) return false; break;
                    case "lte": if (c > 0) return false; break;
                    default: return false;
                }
            }
            return true;
        }
    }
    /// <summary>Validate each composed dialogue against its own NPC declarations, including nested replies.</summary>
    public static class NpcDialogueVariables
    {
        public static List<string> Check(ComposedDialogue dialogue, NpcDefinition def)
        {
            var messages = new List<string>();
            foreach (var topic in dialogue.Topics)
            {
                Condition(topic.When, def, messages);
                Actions(topic.Actions, def, messages);
                foreach (var a in topic.Answers) Answer(a, def, messages);
                foreach (var r in topic.Replies) Reply(r, def, messages);
            }
            foreach (var a in dialogue.Greetings) Answer(a, def, messages);
            return messages;
        }
        static void Condition(Condition c, NpcDefinition d, List<string> messages)
        {
            if (c == null) return;
            foreach (var tests in c.VariableConditions)
            {
                string error = tests.CheckDefinition(d);
                if (error != null) Report(d, "condition: " + error, messages);
            }
        }
        static void Actions(DialogueActions a, NpcDefinition d, List<string> messages)
        {
            foreach (var p in a.SetVariables) Action(p.Key, p.Value, false, d, messages);
            foreach (var p in a.AddVariables) Action(p.Key, p.Value, true, d, messages);
        }
        static void Action(string name, NpcVariableValue value, bool add, NpcDefinition d, List<string> messages)
        {
            NpcVariableValue initial;
            if (!d.Variables.TryGetValue(name, out initial)) Report(d, "action: unknown variable " + name, messages);
            else if (initial.isBoolean != value.isBoolean || (add && initial.isBoolean)) Report(d, "action: wrong type for " + name, messages);
        }
        static void Answer(DialogueAnswer a, NpcDefinition d, List<string> messages)
        {
            Condition(a.When, d, messages); Actions(a.Actions, d, messages);
            foreach (var r in a.Replies) Reply(r, d, messages);
        }
        static void Reply(DialogueReply r, NpcDefinition d, List<string> messages)
        {
            Condition(r.When, d, messages); Actions(r.Actions, d, messages);
            foreach (var a in r.Answers) Answer(a, d, messages);
            foreach (var next in r.Replies) Reply(next, d, messages);
        }
        static void Report(NpcDefinition d, string text, List<string> messages)
        {
            string m = d.Id + ": dialogue variable " + text;
            if (!messages.Contains(m)) messages.Add(m);
        }
    }

}
