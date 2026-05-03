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
                if (!Fields.TryGetValue(fieldName, out var (thisType, thisCard)))
                    return false;

                if (!thisType.IsAssignableTo(otherType))
                    return false;

                // Cardinality compatibility
                if (!IsCardinalityCompatible(thisCard, otherCard))
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
        public abstract DslType GetType();
    }

    public class NumericValue : DslValue
    {
        public decimal Value { get; }
        private readonly string _type; // "num", "real", "int", "nat"

        public NumericValue(decimal value, string type = "num")
        {
            Value = value;
            _type = type;
        }

        public override DslType GetType() => new PrimitiveTypeInfo(_type);
    }

    public class BoolValue : DslValue
    {
        public bool Value { get; }

        public BoolValue(bool value)
        {
            Value = value;
        }

        public override DslType GetType() => new PrimitiveTypeInfo("bool");
    }

    public class StringValue : DslValue
    {
        public string Value { get; }

        public StringValue(string value)
        {
            Value = value;
        }

        public override DslType GetType() => new PrimitiveTypeInfo("string");
    }

    public class ObjectValue : DslValue
    {
        /// <summary>
        /// Sorted dictionary: numeric keys sort lower than string keys.
        /// </summary>
        public SortedDictionary<object, DslValue> Fields { get; }

        public ObjectValue()
        {
            Fields = new SortedDictionary<object, DslValue>(new ObjectKeyComparer());
        }

        public ObjectValue(SortedDictionary<object, DslValue> fields)
        {
            Fields = fields;
        }

        public override DslType GetType() => new PrimitiveTypeInfo("object");
    }

    /// <summary>
    /// Custom comparer for object keys: numeric keys sort lower than string keys.
    /// </summary>
    public class ObjectKeyComparer : IComparer<object>
    {
        public int Compare(object? x, object? y)
        {
            if (x is null && y is null) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            // Numeric keys sort lower than string keys
            var xIsNumeric = x is long or int or decimal;
            var yIsNumeric = y is long or int or decimal;

            if (xIsNumeric && !yIsNumeric) return -1;
            if (!xIsNumeric && yIsNumeric) return 1;

            // Both numeric or both string
            if (xIsNumeric && yIsNumeric)
            {
                var xNum = Convert.ToDecimal(x);
                var yNum = Convert.ToDecimal(y);
                return xNum.CompareTo(yNum);
            }

            // Both strings
            return string.Compare(x.ToString(), y.ToString(), StringComparison.Ordinal);
        }
    }
}
