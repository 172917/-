using MotionStudio.Core.Variables;

namespace MotionStudio.Core.Expressions;

public sealed class SimpleExpressionEvaluator
{
    public bool TryEvaluate(string expression, MotionVariableTable variables, out object? result, out string error)
    {
        result = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(expression))
        {
            error = "表达式不能为空";
            return false;
        }

        try
        {
            var parser = new Parser(expression, variables);
            result = parser.Parse();
            return true;
        }
        catch (ExpressionException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private sealed class Parser
    {
        private readonly List<Token> _tokens;
        private int _index;
        private readonly MotionVariableTable _variables;

        public Parser(string text, MotionVariableTable variables)
        {
            _tokens = Tokenize(text);
            _variables = variables;
        }

        public object? Parse()
        {
            var value = ParseComparison();
            Expect(TokenType.Eof);
            return value;
        }

        private object? ParseComparison()
        {
            var left = ParseAddSub();
            while (Match(TokenType.Greater, TokenType.GreaterEqual, TokenType.Less, TokenType.LessEqual, TokenType.EqualEqual, TokenType.NotEqual))
            {
                var op = Previous();
                var right = ParseAddSub();
                left = EvaluateComparison(left, right, op.Type);
            }

            return left;
        }

        private object? ParseAddSub()
        {
            var left = ParseMulDiv();
            while (Match(TokenType.Plus, TokenType.Minus))
            {
                var op = Previous();
                var right = ParseMulDiv();
                left = EvaluateAddSub(left, right, op.Type);
            }

            return left;
        }

        private object? ParseMulDiv()
        {
            var left = ParseUnary();
            while (Match(TokenType.Star, TokenType.Slash))
            {
                var op = Previous();
                var right = ParseUnary();
                left = EvaluateMulDiv(left, right, op.Type);
            }

            return left;
        }

        private object? ParseUnary()
        {
            if (Match(TokenType.Minus))
            {
                var value = ParseUnary();
                if (!TryToDouble(value, out var num))
                {
                    throw new ExpressionException("一元负号仅支持数字");
                }

                return -num;
            }

            return ParsePrimary();
        }

        private object? ParsePrimary()
        {
            if (Match(TokenType.Number))
            {
                return Previous().NumberValue;
            }

            if (Match(TokenType.String))
            {
                return Previous().Text;
            }

            if (Match(TokenType.True))
            {
                return true;
            }

            if (Match(TokenType.False))
            {
                return false;
            }

            if (Match(TokenType.Identifier))
            {
                var name = Previous().Text;
                if (!_variables.TryGetVariable(name, out var value))
                {
                    throw new ExpressionException($"未定义变量: {name}");
                }

                return value;
            }

            if (Match(TokenType.LeftParen))
            {
                var value = ParseComparison();
                Expect(TokenType.RightParen, "缺少右括号 ')'");
                return value;
            }

            throw new ExpressionException($"无法解析表达式，位置 {Current().Position}");
        }

        private static object EvaluateComparison(object? left, object? right, TokenType op)
        {
            if (op is TokenType.EqualEqual or TokenType.NotEqual)
            {
                var equal = EqualsWithNumberCompat(left, right);
                return op == TokenType.EqualEqual ? equal : !equal;
            }

            if (!TryToDouble(left, out var l) || !TryToDouble(right, out var r))
            {
                throw new ExpressionException("比较运算仅支持数字");
            }

            return op switch
            {
                TokenType.Greater => l > r,
                TokenType.GreaterEqual => l >= r,
                TokenType.Less => l < r,
                TokenType.LessEqual => l <= r,
                _ => throw new ExpressionException("不支持的比较运算")
            };
        }

        private static object EvaluateAddSub(object? left, object? right, TokenType op)
        {
            if (op == TokenType.Plus && (left is string || right is string))
            {
                return $"{left}{right}";
            }

            if (!TryToDouble(left, out var l) || !TryToDouble(right, out var r))
            {
                throw new ExpressionException("加减运算仅支持数字（或字符串拼接使用 +）");
            }

            return op == TokenType.Plus ? l + r : l - r;
        }

        private static object EvaluateMulDiv(object? left, object? right, TokenType op)
        {
            if (!TryToDouble(left, out var l) || !TryToDouble(right, out var r))
            {
                throw new ExpressionException("乘除运算仅支持数字");
            }

            if (op == TokenType.Slash && Math.Abs(r) < double.Epsilon)
            {
                throw new ExpressionException("除数不能为 0");
            }

            return op == TokenType.Star ? l * r : l / r;
        }

        private static bool TryToDouble(object? value, out double result)
        {
            switch (value)
            {
                case int i:
                    result = i;
                    return true;
                case double d:
                    result = d;
                    return true;
                case string s when double.TryParse(s, out var parsed):
                    result = parsed;
                    return true;
                default:
                    result = 0;
                    return false;
            }
        }

        private static bool EqualsWithNumberCompat(object? left, object? right)
        {
            if (TryToDouble(left, out var l) && TryToDouble(right, out var r))
            {
                return Math.Abs(l - r) < 1e-12;
            }

            return Equals(left, right);
        }

        private bool Match(params TokenType[] types)
        {
            foreach (var type in types)
            {
                if (!Check(type))
                {
                    continue;
                }

                _index++;
                return true;
            }

            return false;
        }

        private void Expect(TokenType type, string? message = null)
        {
            if (Check(type))
            {
                _index++;
                return;
            }

            throw new ExpressionException(message ?? $"期望 {type}，但实际为 {Current().Type}");
        }

        private bool Check(TokenType type) => Current().Type == type;

        private Token Current() => _tokens[Math.Min(_index, _tokens.Count - 1)];

        private Token Previous() => _tokens[Math.Max(_index - 1, 0)];

        private static List<Token> Tokenize(string text)
        {
            var tokens = new List<Token>();
            var i = 0;
            while (i < text.Length)
            {
                var ch = text[i];
                if (char.IsWhiteSpace(ch))
                {
                    i++;
                    continue;
                }

                if (char.IsDigit(ch))
                {
                    var start = i;
                    i++;
                    while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.'))
                    {
                        i++;
                    }

                    var raw = text[start..i];
                    if (!double.TryParse(raw, out var num))
                    {
                        throw new ExpressionException($"数字格式错误: {raw}");
                    }

                    tokens.Add(new Token(TokenType.Number, raw, start, num));
                    continue;
                }

                if (char.IsLetter(ch) || ch == '_')
                {
                    var start = i;
                    i++;
                    while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                    {
                        i++;
                    }

                    var id = text[start..i];
                    var lowered = id.ToLowerInvariant();
                    tokens.Add(lowered switch
                    {
                        "true" => new Token(TokenType.True, id, start),
                        "false" => new Token(TokenType.False, id, start),
                        _ => new Token(TokenType.Identifier, id, start)
                    });
                    continue;
                }

                if (ch == '"' || ch == '\'')
                {
                    var quote = ch;
                    var start = i;
                    i++;
                    var strStart = i;
                    while (i < text.Length && text[i] != quote)
                    {
                        i++;
                    }

                    if (i >= text.Length)
                    {
                        throw new ExpressionException("字符串缺少结束引号");
                    }

                    var str = text[strStart..i];
                    i++;
                    tokens.Add(new Token(TokenType.String, str, start));
                    continue;
                }

                Token token = ch switch
                {
                    '+' => new Token(TokenType.Plus, "+", i),
                    '-' => new Token(TokenType.Minus, "-", i),
                    '*' => new Token(TokenType.Star, "*", i),
                    '/' => new Token(TokenType.Slash, "/", i),
                    '(' => new Token(TokenType.LeftParen, "(", i),
                    ')' => new Token(TokenType.RightParen, ")", i),
                    '>' when Peek(text, i + 1) == '=' => new Token(TokenType.GreaterEqual, ">=", i),
                    '<' when Peek(text, i + 1) == '=' => new Token(TokenType.LessEqual, "<=", i),
                    '>' => new Token(TokenType.Greater, ">", i),
                    '<' => new Token(TokenType.Less, "<", i),
                    '=' when Peek(text, i + 1) == '=' => new Token(TokenType.EqualEqual, "==", i),
                    '!' when Peek(text, i + 1) == '=' => new Token(TokenType.NotEqual, "!=", i),
                    _ => throw new ExpressionException($"不支持的字符: '{ch}'，位置 {i}")
                };

                i += token.Text.Length;
                tokens.Add(token);
            }

            tokens.Add(new Token(TokenType.Eof, string.Empty, text.Length));
            return tokens;
        }

        private static char Peek(string text, int index)
        {
            return index >= 0 && index < text.Length ? text[index] : '\0';
        }
    }

    private sealed record Token(TokenType Type, string Text, int Position, double NumberValue = 0);

    private enum TokenType
    {
        Number,
        String,
        Identifier,
        True,
        False,
        Plus,
        Minus,
        Star,
        Slash,
        Greater,
        GreaterEqual,
        Less,
        LessEqual,
        EqualEqual,
        NotEqual,
        LeftParen,
        RightParen,
        Eof
    }

    private sealed class ExpressionException : Exception
    {
        public ExpressionException(string message) : base(message)
        {
        }
    }
}
