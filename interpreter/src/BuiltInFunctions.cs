using System;
using System.Collections.Generic;
using System.Linq;
using TclInterpreter.Typing;

namespace TclInterpreter.Execution
{
    /// <summary>
    /// Implementation of built-in functions for the DSL.
    /// </summary>
    public static class BuiltInFunctions
    {
        // Collection operations
        public static DslValue Map(DslValue collection, Func<DslValue, DslValue> transform)
        {
            if (collection is not ObjectValue ov)
                throw new ExecutionException("map() requires an object");

            var result = new ObjectValue();
            foreach (var (key, value) in ov.Fields)
            {
                result.Fields[key] = transform(value);
            }
            return result;
        }

        public static DslValue Filter(DslValue collection, Func<DslValue, bool> predicate)
        {
            if (collection is not ObjectValue ov)
                throw new ExecutionException("filter() requires an object");

            var result = new ObjectValue();
            foreach (var (key, value) in ov.Fields)
            {
                if (predicate(value))
                    result.Fields[key] = value;
            }
            return result;
        }

        public static DslValue Keys(DslValue collection)
        {
            if (collection is not ObjectValue ov)
                throw new ExecutionException("keys() requires an object");

            var result = new ObjectValue();
            foreach (var key in ov.Fields.Keys)
            {
                // keys() returns a map of key -> key
                result.Fields[key] = ConvertKeyToValue(key);
            }
            return result;
        }

        public static DslValue First(DslValue collection)
        {
            if (collection is not ObjectValue ov)
                throw new ExecutionException("first() requires an object");

            if (ov.Fields.Count == 0)
                throw new ExecutionException("first() called on empty object");

            var firstKvp = ov.Fields.First();
            return firstKvp.Value;
        }

        public static DslValue Count(DslValue collection)
        {
            if (collection is not ObjectValue ov)
                throw new ExecutionException("count() requires an object");

            return new NumericValue(ov.Fields.Count, "int");
        }

        public static DslValue Sum(DslValue collection)
        {
            if (collection is not ObjectValue ov)
                throw new ExecutionException("sum() requires an object");

            var total = decimal.Zero;
            foreach (var value in ov.Fields.Values)
            {
                if (value is NumericValue nv)
                    total += nv.Value;
                else
                    throw new ExecutionException("sum() requires numeric values");
            }

            return new NumericValue(total, "real");
        }

        // Aggregation operations
        public static DslValue Merge(params DslValue[] objects)
        {
            var result = new ObjectValue();

            foreach (var obj in objects)
            {
                if (obj is not ObjectValue ov)
                    throw new ExecutionException("merge() requires objects");

                foreach (var (key, value) in ov.Fields)
                {
                    if (result.Fields.ContainsKey(key))
                        throw new ExecutionException($"merge() conflict on key: {key}");

                    result.Fields[key] = value;
                }
            }

            return result;
        }

        public static DslValue Concat(params DslValue[] objects)
        {
            if (objects.Length == 0)
                return new ObjectValue();

            var result = new ObjectValue();
            object? maxKey = null;

            foreach (var obj in objects)
            {
                if (obj is not ObjectValue ov)
                    throw new ExecutionException("concat() requires objects");

                // Validate key ordering
                foreach (var key in ov.Fields.Keys)
                {
                    if (maxKey != null && CompareKeys(key, maxKey) <= 0)
                        throw new ExecutionException($"concat() key ordering violated: {key} not > {maxKey}");

                    maxKey = key;
                    result.Fields[key] = ov.Fields[key];
                }
            }

            return result;
        }

        public static DslValue Replace(params DslValue[] objects)
        {
            var result = new ObjectValue();

            foreach (var obj in objects)
            {
                if (obj is not ObjectValue ov)
                    throw new ExecutionException("replace() requires objects");

                foreach (var (key, value) in ov.Fields)
                {
                    result.Fields[key] = value; // Overwrite if exists
                }
            }

            return result;
        }

        // Helper functions
        private static DslValue ConvertKeyToValue(object key)
        {
            return key switch
            {
                long l => new NumericValue(l, "int"),
                int i => new NumericValue(i, "int"),
                decimal d => new NumericValue(d, "real"),
                string s => new StringValue(s),
                _ => throw new ExecutionException($"Unexpected key type: {key.GetType()}")
            };
        }

        private static int CompareKeys(object a, object b)
        {
            var aIsNumeric = a is long or int or decimal;
            var bIsNumeric = b is long or int or decimal;

            if (aIsNumeric && !bIsNumeric) return -1;
            if (!aIsNumeric && bIsNumeric) return 1;

            if (aIsNumeric && bIsNumeric)
            {
                var aNum = Convert.ToDecimal(a);
                var bNum = Convert.ToDecimal(b);
                return aNum.CompareTo(bNum);
            }

            return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
        }
    }
}
