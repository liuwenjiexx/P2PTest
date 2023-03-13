using System.Collections;
using System.Collections.Generic;
using System;

namespace Yanmonet.Multiplayer
{

    public class CommandArguments : IEnumerable<string>
    {
        private List<string> args = new();
        private IEqualityComparer<string> keyComparer;

        public CommandArguments(bool ignoreCase = false)
            : this(null, ignoreCase)
        {
        }

        public CommandArguments(IEqualityComparer<string> keyComparer)
            : this(null, keyComparer)
        {
        }

        public CommandArguments(IEnumerable<string> args, bool ignoreCase = false)
            : this(args, (ignoreCase ? StringComparer.InvariantCultureIgnoreCase : StringComparer.Ordinal))
        {
        }

        public CommandArguments(IEnumerable<string> args, IEqualityComparer<string> keyComparer)
        {
            if (keyComparer != null)
            {
                this.keyComparer = keyComparer;
            }
            else
            {
                this.keyComparer = StringComparer.Ordinal;
            }

            if (args != null)
            {
                foreach (var arg in args)
                {
                    this.args.Add(arg);
                }
            }

        }

        public string this[string name]
        {
            get => Get(name);
            set
            {
                if (Has(name))
                {

                }
            }
        }


        public bool Has(string name)
        {
            return FindIndex(name) != -1;
        }


        public int FindIndex(string name)
        {
            for (int i = 0; i < args.Count; i++)
            {
                if (keyComparer.Equals(args[i], name))
                {
                    return i;
                }
            }

            return -1;
        }


        public bool TryGet(string name, out string value)
        {
            value = null;
            int index = FindIndex(name);
            if (index == -1)
                return false;
            if (index + 1 < args.Count)
            {
                value = args[index + 1];
                return true;
            }
            return false;
        }

        public string Get(string name)
        {
            if (TryGet(name, out var value))
            {
                return value;
            }
            throw new KeyNotFoundException($"Not found argument name: {name}");
        }


        public T Get<T>(string name)
        {
            string value = Get(name);
            var type = typeof(T);
            try
            {
                if (type.IsPrimitive)
                {
                    TypeCode typeCode = Type.GetTypeCode(type);
                    switch (typeCode)
                    {
                        case TypeCode.Int32:
                            return (T)(object)int.Parse(value);
                        case TypeCode.Single:
                            return (T)(object)float.Parse(value);
                        case TypeCode.Boolean:
                            return (T)(object)bool.Parse(value);
                        case TypeCode.String:
                            return (T)(object)value;
                    }
                }
                else if (type == typeof(string))
                {
                    return (T)(object)value;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Format error. name: '{name}' value: '{value}'", ex);
            }
            throw new NotImplementedException();
        }

        public bool GetBool(string name)
        {
            return bool.Parse(Get(name));
        }

        public bool Get(string name, ref string value)
        {
            if (TryGet(name, out var newValue))
            {
                if (value != newValue)
                {
                    value = newValue;
                    return true;
                }
            }
            return false;
        }

        public bool Get(string name, ref int value)
        {
            if (TryGet(name, out var newValue))
            {
                if (int.TryParse(newValue, out var v))
                {
                    if (value != v)
                    {
                        value = v;
                        return true;
                    }
                }
            }
            return false;
        }

        public bool Get(string name, ref bool value)
        {
            if (TryGet(name, out var newValue))
            {
                if (bool.TryParse(newValue, out var v))
                {
                    if (value != v)
                    {
                        value = v;
                        return true;
                    }
                }
            }
            return false;
        }

        public bool Get(string name, ref uint value)
        {
            if (TryGet(name, out var newValue))
            {
                if (uint.TryParse(newValue, out var v))
                {
                    if (value != v)
                    {
                        value = v;
                        return true;
                    }
                }
            }
            return false;
        }
        public bool Get(string name, ref ushort value)
        {
            if (TryGet(name, out var newValue))
            {
                if (ushort.TryParse(newValue, out var v))
                {
                    if (value != v)
                    {
                        value = v;
                        return true;
                    }
                }
            }
            return false;
        }


        public bool Get(string name, ref float value)
        {
            if (TryGet(name, out var newValue))
            {
                if (float.TryParse(newValue, out var v))
                {
                    if (value != v)
                    {
                        value = v;
                        return true;
                    }
                }
            }
            return false;
        }

        public string[] ToArray()
        {
            return args.ToArray();
        }

        public IEnumerator<string> GetEnumerator()
        {
            foreach (var arg in args)
            {
                yield return arg;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public static CommandArguments GetEnvironmentArguments()
        {
            return new CommandArguments(Environment.GetCommandLineArgs());
        }
    }
}