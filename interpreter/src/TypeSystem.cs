using System;
using System.Collections.Generic;

namespace TclInterpreter.Typing
{
    /// <summary>
    /// Represents the type hierarchy and provides type checking functionality.
    /// </summary>
    public abstract class DslType
    {
        public abstract string TypeName { get; }
        public abstract bool IsAssignableTo(DslType other);
    }

    public class PrimitiveTypeInfo : DslType
    {
        public string Name { get; }

        public override string TypeName => Name;

        public PrimitiveTypeInfo(string name)
        {
            Name = name;
        }

        public override bool IsAssignableTo(DslType other)
        {
            if (other is not PrimitiveTypeInfo otherPrim)
                return false;

            // Narrowing hierarchy: any > num > real, and any > num > int > nat > bool
            return (Name, otherPrim.Name) switch
            {
                ("any", _) => true,
                (_, "any") => true,
                ("num", "num") => true,
                ("real", "num") => true,
                ("real", "real") => true,
                ("int", "num") => true,
                ("int", "int") => true,
                ("nat", "int") => true,
                ("nat", "num") => true,
                ("nat", "nat") => true,
                ("bool", "nat") => true,
                ("bool", "int") => true,
                ("bool", "num") => true,
                ("bool", "bool") => true,
                _ => false
            };
        }
    }

    public class ObjectTypeInfo : DslType
    {
        public Dictionary<string, (DslType Type, string Cardinality)> Fields { get; }

        public override string TypeName => "object";

        public ObjectTypeInfo(Dictionary<string, (DslType, string)> fields)
        {
            Fields = fields;
        }

        public override bool IsAssignableTo(DslType other)
        {
            if (other is PrimitiveTypeInfo prim && prim.Name == "any")
                return true;

            if (other is not ObjectTypeInfo otherObj)
                return false;

            // Check that all fields in 'other' exist in this object
            foreach (var (fieldName, (otherType, otherCard)) in otherObj.Fields)
            {
                if (!Fields.TryGetValue(fieldName, out var thisField))
                    return false;

                if (!thisField.Type.IsAssignableTo(otherType))
                    return false;

                // Cardinality compatibility
                if (!IsCardinalityCompatible(thisField.Cardinality, otherCard))
                    return false;
            }

            return true;
        }

        private static bool IsCardinalityCompatible(string actual, string required)
        {
            // required is what's in the template/signature
            // actual is what we have
            return (actual, required) switch
            {
                ("", "") => true,
                ("+", "+") => true,
                ("+", "*") => true,
                ("*", "*") => true,
                ("?", "?") => true,
                ("+", "?") => true,
                ("*", "?") => true,
                _ => false
            };
        }
    }

    public class TypeRegistry
    {
        private readonly Dictionary<string, DslType> _types = new();

        public TypeRegistry()
        {
            // Register primitive types
            RegisterType("any", new PrimitiveTypeInfo("any"));
            RegisterType("num", new PrimitiveTypeInfo("num"));
            RegisterType("real", new PrimitiveTypeInfo("real"));
            RegisterType("int", new PrimitiveTypeInfo("int"));
            RegisterType("nat", new PrimitiveTypeInfo("nat"));
            RegisterType("bool", new PrimitiveTypeInfo("bool"));
            RegisterType("object", new PrimitiveTypeInfo("object"));
        }

        public void RegisterType(string name, DslType type)
        {
            _types[name] = type;
        }

        public DslType? LookupType(string name)
        {
            _types.TryGetValue(name, out var type);
            return type;
        }

        public bool IsAssignable(DslType source, DslType target)
        {
            return source.IsAssignableTo(target);
        }
    }

    /// <summary>
    /// Represents runtime values in the DSL.
    /// </summary>
    public abstract class DslValue
    {
        public abstract DslType GetDslType();
    }

    public class NumericValue : DslValue
    {
        public decimal Value { get; }
        public string NumericType { get; } // "num", "real", "int", "nat"

        public NumericValue(decimal value, string type = "num")
        {
            Value = value;
            NumericType = type;
        }

        public override DslType GetDslType() => new PrimitiveTypeInfo(NumericType);

        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public class BoolValue : DslValue
    {
        public bool Value { get; }

        public BoolValue(bool value)
        {
            Value = value;
        }

        public override DslType GetDslType() => new PrimitiveTypeInfo("bool");

        public override string ToString() => Value ? "true" : "false";
    }

    public class StringValue : DslValue
    {
        public string Value { get; }

        public StringValue(string value)
        {
            Value = value;
        }

        public override DslType GetDslType() => new PrimitiveTypeInfo("string");

        public override string ToString() => $"\"{Value}\"";
    }

    public class ObjectValue : DslValue
    {
        /// <summary>
        /// Sorted dictionary: numeric keys sort lower than string keys.
        /// </summary>
        public SortedDictionary<object, DslValue> Fields { get; }

        public ObjectValue()
        {
            Fields = new SortedDictionary<object, DslValue>(ObjectKeyComparer.Instance);
        }

        public ObjectValue(SortedDictionary<object, DslValue> fields)
        {
            Fields = fields;
        }

        public override DslType GetDslType() => new PrimitiveTypeInfo("object");

        public override string ToString() => $"{{object with {Fields.Count} fields}}";
    }

    /// <summary>
    /// Custom comparer for object keys.
    /// Ordering: numeric keys &lt; string keys &lt; ObjectValue keys.
    /// ObjectValue keys are compared via depth-first traversal of their values.
    /// </summary>
    public class ObjectKeyComparer : IComparer<object>
    {
        public static readonly ObjectKeyComparer Instance = new();

        public int Compare(object? x, object? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            int xRank = KeyRank(x);
            int yRank = KeyRank(y);
            if (xRank != yRank) return xRank.CompareTo(yRank);

            return xRank switch
            {
                0 => CompareNumeric(x, y),
                1 => string.Compare(x.ToString(), y.ToString(), StringComparison.Ordinal),
                2 => CompareObjects((ObjectValue)x, (ObjectValue)y),
                _ => 0
            };
        }

        /// <summary>0 = numeric, 1 = string, 2 = object</summary>
        private static int KeyRank(object k) => k switch
        {
            long or int or decimal => 0,
            string => 1,
            ObjectValue => 2,
            _ => 3
        };

        private static int CompareNumeric(object x, object y)
        {
            var a = Convert.ToDecimal(x);
            var b = Convert.ToDecimal(y);
            return a.CompareTo(b);
        }

        /// <summary>
        /// Compare two ObjectValue keys by depth-first traversal of their values
        /// in increasing key order (values only, not keys).
        /// </summary>
        private static int CompareObjects(ObjectValue a, ObjectValue b)
        {
            using var ea = a.Fields.GetEnumerator();
            using var eb = b.Fields.GetEnumerator();

            while (true)
            {
                bool ha = ea.MoveNext();
                bool hb = eb.MoveNext();
                if (!ha && !hb) return 0;
                if (!ha) return -1;  // fewer keys = smaller
                if (!hb) return 1;

                // Compare keys first (to establish traversal order alignment)
                int kc = Instance.Compare(ea.Current.Key, eb.Current.Key);
                if (kc != 0) return kc;

                // Compare values
                int vc = CompareValues(ea.Current.Value, eb.Current.Value);
                if (vc != 0) return vc;
            }
        }

        /// <summary>
        /// Compare two DslValues for ordering.
        /// </summary>
        public static int CompareValues(DslValue a, DslValue b)
        {
            // Same type comparisons
            if (a is NumericValue an && b is NumericValue bn)
                return an.Value.CompareTo(bn.Value);

            if (a is BoolValue ab && b is BoolValue bb)
                return ab.Value.CompareTo(bb.Value);

            if (a is StringValue sa && b is StringValue sb)
                return string.Compare(sa.Value, sb.Value, StringComparison.Ordinal);

            if (a is ObjectValue ao && b is ObjectValue bo)
                return CompareObjects(ao, bo);

            // Cross-type: numeric < bool < string < object
            return ValueRank(a).CompareTo(ValueRank(b));
        }

        private static int ValueRank(DslValue v) => v switch
        {
            NumericValue => 0,
            BoolValue => 1,
            StringValue => 2,
            ObjectValue => 3,
            _ => 4
        };
    }
}
