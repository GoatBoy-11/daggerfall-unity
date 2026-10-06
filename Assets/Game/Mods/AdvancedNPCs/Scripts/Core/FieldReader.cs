using System;
using System.Collections.Generic;
using System.Globalization;

namespace AdvancedNPCs.Core
{
    /// <summary>
    /// Typed readers for objects produced by Json.Parse. A missing or null key yields the fallback (or null);
    /// a value of the wrong type yields a problem text, otherwise the readers return null.
    /// </summary>
    public static class FieldReader
    {
        public static string Text(Dictionary<string, object> o, string key, string fallback, out string value)
        {
            object raw;
            value = fallback;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return null;
            value = raw as string;
            return value == null ? "must be text" : null;
        }

        public static string Number(Dictionary<string, object> o, string key, double fallback, out double value)
        {
            object raw;
            value = fallback;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return null;
            if (!(raw is double))
                return "must be a number";
            value = (double)raw;
            return null;
        }

        public static string Bool(Dictionary<string, object> o, string key, bool fallback, out bool value)
        {
            object raw;
            value = fallback;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return null;
            if (!(raw is bool))
                return "must be true or false";
            value = (bool)raw;
            return null;
        }

        /// <summary>False if present but not an array of numbers; values is null when the key is missing.</summary>
        public static bool Numbers(Dictionary<string, object> o, string key, out double[] values)
        {
            object raw;
            values = null;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return true;
            List<object> list = raw as List<object>;
            if (list == null)
                return false;
            values = new double[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                if (!(list[i] is double))
                {
                    values = null;
                    return false;
                }
                values[i] = (double)list[i];
            }
            return true;
        }

        /// <summary>A list of non-empty texts; values is null when the key is missing.</summary>
        public static string Texts(Dictionary<string, object> o, string key, out List<string> values)
        {
            object raw;
            values = null;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return null;
            List<object> list = raw as List<object>;
            if (list == null)
                return "must be a list of texts";
            List<string> texts = new List<string>();
            foreach (object item in list)
            {
                string s = item as string;
                if (s == null || s.Trim().Length == 0)
                    return "must be a list of non-empty texts";
                texts.Add(s);
            }
            values = texts;
            return null;
        }

        /// <summary>An object; value is null when the key is missing.</summary>
        public static string Object(Dictionary<string, object> o, string key, out Dictionary<string, object> value)
        {
            object raw;
            value = null;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return null;
            value = raw as Dictionary<string, object>;
            return value == null ? "must be an object" : null;
        }

        /// <summary>A list of objects; values is null when the key is missing.</summary>
        public static string Objects(Dictionary<string, object> o, string key, out List<Dictionary<string, object>> values)
        {
            object raw;
            values = null;
            if (!o.TryGetValue(key, out raw) || raw == null)
                return null;
            List<object> list = raw as List<object>;
            if (list == null)
                return "must be a list of objects";
            List<Dictionary<string, object>> objects = new List<Dictionary<string, object>>();
            foreach (object item in list)
            {
                Dictionary<string, object> d = item as Dictionary<string, object>;
                if (d == null)
                    return "must be a list of objects";
                objects.Add(d);
            }
            values = objects;
            return null;
        }

        /// <summary>Keys of o that are not in known, sorted.</summary>
        public static List<string> UnknownKeys(Dictionary<string, object> o, ICollection<string> known)
        {
            List<string> unknown = new List<string>();
            foreach (string key in o.Keys)
            {
                if (!known.Contains(key))
                    unknown.Add(key);
            }
            unknown.Sort(StringComparer.Ordinal);
            return unknown;
        }

        public static string Num(double f)
        {
            return f.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
