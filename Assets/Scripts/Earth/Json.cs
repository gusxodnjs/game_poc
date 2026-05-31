// Json.cs
// 최소 JSON 파서 (MiniJSON 축약본). Overpass 응답처럼 dictionary 키가 동적인 JSON을
// Unity의 JsonUtility 로는 못 읽으므로 내장.
//
// 출처: MiniJSON by Calvin Rien (https://gist.github.com/darktable/1411710),
//       Patrick van Bergen 의 구현 기반. MIT / Public Domain.
//       본 프로젝트는 namespace 를 제거해 전역 `public static class Json` 으로 두고,
//       Deserialize 경로만 사용한다(Serialize 미사용).
//
// 반환 타입 계약 (OverpassParser 가 의존):
//   object  → Dictionary<string, object> | List<object> | string | double | long | bool | null
//   정수는 long, 실수는 double.

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public static class Json
{
    /// <summary>JSON 문자열 → object (Dictionary/List/string/double/long/bool/null). 실패 시 예외.</summary>
    public static object Deserialize(string json)
    {
        if (json == null) return null;
        return Parser.Parse(json);
    }

    private sealed class Parser : System.IDisposable
    {
        private const string WordBreak = "{}[],:\"";
        private System.IO.StringReader _json;

        private Parser(string jsonString)
        {
            _json = new System.IO.StringReader(jsonString);
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
            _json.Dispose();
            _json = null;
        }

        private Dictionary<string, object> ParseObject()
        {
            var table = new Dictionary<string, object>();
            _json.Read(); // {
            while (true)
            {
                switch (NextToken)
                {
                    case Token.None:
                        return null;
                    case Token.Comma:
                        continue;
                    case Token.CurlyClose:
                        return table;
                    default:
                        string name = ParseString();
                        if (name == null) return null;
                        if (NextToken != Token.Colon) return null;
                        _json.Read(); // :
                        table[name] = ParseValue();
                        break;
                }
            }
        }

        private List<object> ParseArray()
        {
            var array = new List<object>();
            _json.Read(); // [
            bool parsing = true;
            while (parsing)
            {
                Token nextToken = NextToken;
                switch (nextToken)
                {
                    case Token.None:
                        return null;
                    case Token.Comma:
                        continue;
                    case Token.SquareClose:
                        parsing = false;
                        break;
                    default:
                        array.Add(ParseByToken(nextToken));
                        break;
                }
            }
            return array;
        }

        private object ParseValue()
        {
            return ParseByToken(NextToken);
        }

        private object ParseByToken(Token token)
        {
            switch (token)
            {
                case Token.String: return ParseString();
                case Token.Number: return ParseNumber();
                case Token.CurlyOpen: return ParseObject();
                case Token.SquareOpen: return ParseArray();
                case Token.True: return true;
                case Token.False: return false;
                case Token.Null: return null;
                default: return null;
            }
        }

        private string ParseString()
        {
            var s = new StringBuilder();
            _json.Read(); // opening "
            bool parsing = true;
            while (parsing)
            {
                if (_json.Peek() == -1) break;
                char c = NextChar;
                switch (c)
                {
                    case '"':
                        parsing = false;
                        break;
                    case '\\':
                        if (_json.Peek() == -1) { parsing = false; break; }
                        c = NextChar;
                        switch (c)
                        {
                            case '"': s.Append('"'); break;
                            case '\\': s.Append('\\'); break;
                            case '/': s.Append('/'); break;
                            case 'b': s.Append('\b'); break;
                            case 'f': s.Append('\f'); break;
                            case 'n': s.Append('\n'); break;
                            case 'r': s.Append('\r'); break;
                            case 't': s.Append('\t'); break;
                            case 'u':
                                var hex = new char[4];
                                for (int i = 0; i < 4; i++) hex[i] = NextChar;
                                s.Append((char)System.Convert.ToInt32(new string(hex), 16));
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
            if (number.IndexOf('.') == -1 && number.IndexOf('e') == -1 && number.IndexOf('E') == -1)
            {
                long parsedInt;
                long.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedInt);
                return parsedInt;
            }
            double parsedDouble;
            double.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedDouble);
            return parsedDouble;
        }

        private void EatWhitespace()
        {
            while (char.IsWhiteSpace(PeekChar))
            {
                _json.Read();
                if (_json.Peek() == -1) break;
            }
        }

        private char PeekChar => System.Convert.ToChar(_json.Peek());
        private char NextChar => System.Convert.ToChar(_json.Read());

        private string NextWord
        {
            get
            {
                var word = new StringBuilder();
                while (!IsWordBreak(PeekChar))
                {
                    word.Append(NextChar);
                    if (_json.Peek() == -1) break;
                }
                return word.ToString();
            }
        }

        private Token NextToken
        {
            get
            {
                EatWhitespace();
                if (_json.Peek() == -1) return Token.None;
                switch (PeekChar)
                {
                    case '{': return Token.CurlyOpen;
                    case '}': _json.Read(); return Token.CurlyClose;
                    case '[': return Token.SquareOpen;
                    case ']': _json.Read(); return Token.SquareClose;
                    case ',': _json.Read(); return Token.Comma;
                    case '"': return Token.String;
                    case ':': return Token.Colon;
                    case '0': case '1': case '2': case '3': case '4':
                    case '5': case '6': case '7': case '8': case '9':
                    case '-':
                        return Token.Number;
                }
                switch (NextWord)
                {
                    case "false": return Token.False;
                    case "true": return Token.True;
                    case "null": return Token.Null;
                }
                return Token.None;
            }
        }

        private static bool IsWordBreak(char c)
        {
            return char.IsWhiteSpace(c) || WordBreak.IndexOf(c) != -1;
        }

        private enum Token
        {
            None, CurlyOpen, CurlyClose, SquareOpen, SquareClose,
            Colon, Comma, String, Number, True, False, Null
        }
    }
}
