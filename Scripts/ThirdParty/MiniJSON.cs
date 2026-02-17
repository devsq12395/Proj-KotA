/*
 * MiniJSON.cs
 *
 * A simple JSON parser and serializer.
 *
 * Based on the public domain/mit-licensed implementations commonly used in Unity projects.
 * Original author often credited: Calvin Rien (https://gist.github.com/darktable/1411710)
 * This variant is trimmed to work with Unity and C#.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MiniJSON
{
    public static class Json
    {
        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            return Parser.Parse(json);
        }

        public static string Serialize(object obj)
        {
            return Serializer.Serialize(obj);
        }
    }

    internal sealed class Parser : IDisposable
    {
        private const string WORD_TRUE = "true";
        private const string WORD_FALSE = "false";
        private const string WORD_NULL = "null";

        private StringReader json;

        private Parser(string jsonString)
        {
            json = new StringReader(jsonString);
        }

        public static object Parse(string jsonString)
        {
            using (var instance = new Parser(jsonString))
            {
                return instance.ParseValue();
            }
        }

        public void Dispose()
        {
            if (json != null)
            {
                json.Dispose();
                json = null;
            }
        }

        private Dictionary<string, object> ParseObject()
        {
            var table = new Dictionary<string, object>();

            // consume '{'
            json.Read();

            while (true)
            {
                var nextToken = NextToken;

                if (nextToken == TOKEN.NONE)
                {
                    return null;
                }
                else if (nextToken == TOKEN.CURLY_CLOSE)
                {
                    // consume '}'
                    json.Read();
                    return table;
                }
                else
                {
                    // parse key
                    string name = ParseString();
                    if (name == null)
                    {
                        return null;
                    }

                    // consume ':'
                    if (NextToken != TOKEN.COLON)
                    {
                        return null;
                    }
                    // consume ':'
                    json.Read();

                    // parse value
                    table[name] = ParseValue();
                }

                // consume ',' or '}'
                switch (NextToken)
                {
                    case TOKEN.COMMA:
                        json.Read();
                        continue;
                    case TOKEN.CURLY_CLOSE:
                        json.Read();
                        return table;
                    default:
                        return null;
                }
            }
        }

        private List<object> ParseArray()
        {
            var array = new List<object>();

            // consume '['
            json.Read();

            var parsing = true;
            while (parsing)
            {
                var nextToken = NextToken;
                if (nextToken == TOKEN.NONE)
                {
                    return null;
                }
                else if (nextToken == TOKEN.SQUARE_CLOSE)
                {
                    // consume ']'
                    json.Read();
                    break;
                }
                else
                {
                    var value = ParseValue();
                    array.Add(value);
                }

                // consume ',' or ']'
                switch (NextToken)
                {
                    case TOKEN.COMMA:
                        json.Read();
                        continue;
                    case TOKEN.SQUARE_CLOSE:
                        json.Read();
                        parsing = false;
                        break;
                    default:
                        return null;
                }
            }

            return array;
        }

        private object ParseValue()
        {
            switch (NextToken)
            {
                case TOKEN.STRING:
                    return ParseString();
                case TOKEN.NUMBER:
                    return ParseNumber();
                case TOKEN.CURLY_OPEN:
                    return ParseObject();
                case TOKEN.SQUARE_OPEN:
                    return ParseArray();
                case TOKEN.TRUE:
                    return true;
                case TOKEN.FALSE:
                    return false;
                case TOKEN.NULL:
                    return null;
                case TOKEN.NONE:
                default:
                    return null;
            }
        }

        private string ParseString()
        {
            var s = new StringBuilder();
            char c;

            // consume '"'
            json.Read();

            bool parsing = true;
            while (parsing)
            {
                if (json.Peek() == -1)
                {
                    break;
                }

                c = NextChar;
                switch (c)
                {
                    case '"':
                        parsing = false;
                        break;
                    case '\\':
                        if (json.Peek() == -1)
                        {
                            parsing = false;
                            break;
                        }
                        c = NextChar;
                        switch (c)
                        {
                            case '"':
                            case '\\':
                            case '/':
                                s.Append(c);
                                break;
                            case 'b':
                                s.Append('\b');
                                break;
                            case 'f':
                                s.Append('\f');
                                break;
                            case 'n':
                                s.Append('\n');
                                break;
                            case 'r':
                                s.Append('\r');
                                break;
                            case 't':
                                s.Append('\t');
                                break;
                            case 'u':
                                var hex = new char[4];
                                for (int i = 0; i < 4; i++)
                                {
                                    hex[i] = NextChar;
                                }
                                s.Append((char)Convert.ToInt32(new string(hex), 16));
                                break;
                        }
                        break;
                    default:
                        s.Append(c);
                        break;
                }
            }

            return s.ToString();
        }

        private object ParseNumber()
        {
            string number = NextWord;
            if (number.IndexOf('.') != -1 || number.IndexOf('e') != -1 || number.IndexOf('E') != -1)
            {
                if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDouble))
                    return parsedDouble;
            }
            else
            {
                // Try parse as long first to keep integers intact
                if (long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLong))
                    return parsedLong;
            }

            // Fallback as double
            if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
            return 0d;
        }

        private void EatWhitespace()
        {
            while (json.Peek() != -1)
            {
                char c = (char)json.Peek();
                if (char.IsWhiteSpace(c))
                {
                    json.Read();
                    continue;
                }
                break;
            }
        }

        private char NextChar => Convert.ToChar(json.Read());

        private string NextWord
        {
            get
            {
                var sb = new StringBuilder();
                while (json.Peek() != -1 && !IsWordBreak((char)json.Peek()))
                {
                    sb.Append(NextChar);
                }
                return sb.ToString();
            }
        }

        private TOKEN NextToken
        {
            get
            {
                EatWhitespace();
                if (json.Peek() == -1) return TOKEN.NONE;

                switch ((char)json.Peek())
                {
                    case '{': return TOKEN.CURLY_OPEN;
                    case '}': return TOKEN.CURLY_CLOSE;
                    case '[': return TOKEN.SQUARE_OPEN;
                    case ']': return TOKEN.SQUARE_CLOSE;
                    case ',': return TOKEN.COMMA;
                    case '"': return TOKEN.STRING;
                    case ':': return TOKEN.COLON;
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                    case '-':
                        return TOKEN.NUMBER;
                }

                string word = NextWord;
                switch (word)
                {
                    case WORD_TRUE: return TOKEN.TRUE;
                    case WORD_FALSE: return TOKEN.FALSE;
                    case WORD_NULL: return TOKEN.NULL;
                }

                return TOKEN.NONE;
            }
        }

        private static bool IsWordBreak(char c)
        {
            return char.IsWhiteSpace(c) || c == '{' || c == '}' || c == '[' || c == ']' || c == ':' || c == ',' || c == '"' || c == '\\' || c == '/' || c == '\0';
        }

        private enum TOKEN
        {
            NONE,
            CURLY_OPEN,
            CURLY_CLOSE,
            SQUARE_OPEN,
            SQUARE_CLOSE,
            COLON,
            COMMA,
            STRING,
            NUMBER,
            TRUE,
            FALSE,
            NULL
        }
    }

    internal sealed class Serializer
    {
        private StringBuilder builder;

        private Serializer()
        {
            builder = new StringBuilder(4096);
        }

        public static string Serialize(object obj)
        {
            var instance = new Serializer();
            instance.SerializeValue(obj);
            return instance.builder.ToString();
        }

        private void SerializeValue(object value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            if (value is string s)
            {
                SerializeString(s);
                return;
            }

            if (value is bool b)
            {
                builder.Append(b ? "true" : "false");
                return;
            }

            if (value is IDictionary dict)
            {
                SerializeObject(dict);
                return;
            }

            if (value is IList list)
            {
                SerializeArray(list);
                return;
            }

            if (value is char ch)
            {
                SerializeString(new string(ch, 1));
                return;
            }

            if (value is IFormattable formattable)
            {
                // Use invariant culture for numbers
                builder.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
                return;
            }

            // Fallback to ToString() wrapped in quotes
            SerializeString(value.ToString());
        }

        private void SerializeObject(IDictionary obj)
        {
            bool first = true;
            builder.Append('{');
            foreach (DictionaryEntry entry in obj)
            {
                if (!first) builder.Append(',');
                SerializeString(entry.Key.ToString());
                builder.Append(':');
                SerializeValue(entry.Value);
                first = false;
            }
            builder.Append('}');
        }

        private void SerializeArray(IList array)
        {
            builder.Append('[');
            bool first = true;
            for (int i = 0; i < array.Count; i++)
            {
                if (!first) builder.Append(',');
                SerializeValue(array[i]);
                first = false;
            }
            builder.Append(']');
        }

        private void SerializeString(string str)
        {
            builder.Append('"');
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(c);
                        }
                        break;
                }
            }
            builder.Append('"');
        }
    }
}
