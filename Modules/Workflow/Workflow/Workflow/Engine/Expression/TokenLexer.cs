namespace Workflow.Workflow.Engine.Expression;

public class TokenLexer
{
    private readonly string _input;
    private int _position;
    private readonly List<Token> _tokens = new();

    public TokenLexer(string input)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
    }

    public List<Token> Tokenize()
    {
        _tokens.Clear();
        _position = 0;

        while (_position < _input.Length)
        {
            SkipWhitespace();

            if (_position >= _input.Length)
                break;

            var currentChar = _input[_position];

            switch (currentChar)
            {
                case '(':
                    _tokens.Add(new Token(TokenType.LeftParen, "("));
                    _position++;
                    break;
                case ')':
                    _tokens.Add(new Token(TokenType.RightParen, ")"));
                    _position++;
                    break;
                case '\'':
                case '"':
                    ReadStringLiteral(currentChar);
                    break;
                case '=':
                    if (PeekNext() == '=')
                    {
                        _tokens.Add(new Token(TokenType.Equal, "=="));
                        _position += 2;
                    }
                    else
                    {
                        throw new ExpressionParseException($"Unexpected character '=' at position {_position}");
                    }
                    break;
                case '!':
                    if (PeekNext() == '=')
                    {
                        _tokens.Add(new Token(TokenType.NotEqual, "!="));
                        _position += 2;
                    }
                    else
                    {
                        _tokens.Add(new Token(TokenType.Not, "!"));
                        _position++;
                    }
                    break;
                case '>':
                    if (PeekNext() == '=')
                    {
                        _tokens.Add(new Token(TokenType.GreaterThanOrEqual, ">="));
                        _position += 2;
                    }
                    else
                    {
                        _tokens.Add(new Token(TokenType.GreaterThan, ">"));
                        _position++;
                    }
                    break;
                case '<':
                    if (PeekNext() == '=')
                    {
                        _tokens.Add(new Token(TokenType.LessThanOrEqual, "<="));
                        _position += 2;
                    }
                    else
                    {
                        _tokens.Add(new Token(TokenType.LessThan, "<"));
                        _position++;
                    }
                    break;
                case '&':
                    if (PeekNext() == '&')
                    {
                        _tokens.Add(new Token(TokenType.And, "&&"));
                        _position += 2;
                    }
                    else
                    {
                        throw new ExpressionParseException($"Unexpected character '&' at position {_position}");
                    }
                    break;
                case '|':
                    if (PeekNext() == '|')
                    {
                        _tokens.Add(new Token(TokenType.Or, "||"));
                        _position += 2;
                    }
                    else
                    {
                        throw new ExpressionParseException($"Unexpected character '|' at position {_position}");
                    }
                    break;
                default:
                    if (char.IsLetter(currentChar) || currentChar == '_')
                    {
                        ReadIdentifierOrKeyword();
                    }
                    else if (char.IsDigit(currentChar))
                    {
                        ReadNumber();
                    }
                    else
                    {
                        throw new ExpressionParseException($"Unexpected character '{currentChar}' at position {_position}");
                    }
                    break;
            }
        }

        return _tokens;
    }

    private void SkipWhitespace()
    {
        while (_position < _input.Length && char.IsWhiteSpace(_input[_position]))
        {
            _position++;
        }
    }

    private char PeekNext()
    {
        return _position + 1 < _input.Length ? _input[_position + 1] : '\0';
    }

    private void ReadStringLiteral(char quote)
    {
        var startPos = _position;
        _position++; // Skip opening quote

        var value = "";
        while (_position < _input.Length && _input[_position] != quote)
        {
            if (_input[_position] == '\\' && _position + 1 < _input.Length)
            {
                _position++;
                var escapeChar = _input[_position] switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '\\' => '\\',
                    '\'' => '\'',
                    '"' => '"',
                    _ => _input[_position]
                };
                value += escapeChar;
            }
            else
            {
                value += _input[_position];
            }
            _position++;
        }

        if (_position >= _input.Length)
        {
            throw new ExpressionParseException($"Unterminated string literal starting at position {startPos}");
        }

        _position++; // Skip closing quote
        _tokens.Add(new Token(TokenType.StringLiteral, value));
    }

    private void ReadIdentifierOrKeyword()
    {
        var startPos = _position;
        while (_position < _input.Length && 
               (char.IsLetterOrDigit(_input[_position]) || _input[_position] == '_'))
        {
            _position++;
        }

        var value = _input.Substring(startPos, _position - startPos);
        var tokenType = value.ToUpperInvariant() switch
        {
            "AND" => TokenType.And,
            "OR" => TokenType.Or,
            "NOT" => TokenType.Not,
            "CONTAINS" => TokenType.Contains,
            "TRUE" => TokenType.BooleanLiteral,
            "FALSE" => TokenType.BooleanLiteral,
            "NULL" => TokenType.NullLiteral,
            _ => TokenType.Identifier
        };

        _tokens.Add(new Token(tokenType, value));
    }

    private void ReadNumber()
    {
        var startPos = _position;
        var hasDecimal = false;

        while (_position < _input.Length && 
               (char.IsDigit(_input[_position]) || (_input[_position] == '.' && !hasDecimal)))
        {
            if (_input[_position] == '.')
                hasDecimal = true;
            _position++;
        }

        var value = _input.Substring(startPos, _position - startPos);
        _tokens.Add(new Token(TokenType.NumberLiteral, value));
    }
}

public class Token
{
    public TokenType Type { get; }
    public string Value { get; }

    public Token(TokenType type, string value)
    {
        Type = type;
        Value = value;
    }

    public override string ToString()
    {
        return $"{Type}: {Value}";
    }
}

public enum TokenType
{
    Identifier,
    StringLiteral,
    NumberLiteral,
    BooleanLiteral,
    NullLiteral,
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Contains,
    And,
    Or,
    Not,
    LeftParen,
    RightParen
}

public class ExpressionParseException : Exception
{
    public ExpressionParseException(string message) : base(message) { }
    public ExpressionParseException(string message, Exception innerException) : base(message, innerException) { }
}