namespace Assignment.Workflow.Engine.Expression;

public class ExpressionParser
{
    private readonly List<Token> _tokens;
    private int _position;

    public ExpressionParser(List<Token> tokens)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    public ExpressionNode Parse()
    {
        _position = 0;
        var result = ParseOrExpression();
        
        if (_position < _tokens.Count)
        {
            throw new ExpressionParseException($"Unexpected token '{_tokens[_position].Value}' at position {_position}");
        }
        
        return result;
    }

    private ExpressionNode ParseOrExpression()
    {
        var left = ParseAndExpression();

        while (_position < _tokens.Count && _tokens[_position].Type == TokenType.Or)
        {
            Advance(); // Skip 'OR'
            var right = ParseAndExpression();
            left = new BinaryOperatorNode(left, BinaryOperator.Or, right);
        }

        return left;
    }

    private ExpressionNode ParseAndExpression()
    {
        var left = ParseNotExpression();

        while (_position < _tokens.Count && _tokens[_position].Type == TokenType.And)
        {
            Advance(); // Skip 'AND'
            var right = ParseNotExpression();
            left = new BinaryOperatorNode(left, BinaryOperator.And, right);
        }

        return left;
    }

    private ExpressionNode ParseNotExpression()
    {
        if (_position < _tokens.Count && _tokens[_position].Type == TokenType.Not)
        {
            Advance(); // Skip 'NOT'
            var operand = ParseNotExpression();
            return new UnaryOperatorNode(UnaryOperator.Not, operand);
        }

        return ParseComparisonExpression();
    }

    private ExpressionNode ParseComparisonExpression()
    {
        var left = ParsePrimaryExpression();

        if (_position < _tokens.Count)
        {
            var token = _tokens[_position];
            var op = token.Type switch
            {
                TokenType.Equal => BinaryOperator.Equal,
                TokenType.NotEqual => BinaryOperator.NotEqual,
                TokenType.GreaterThan => BinaryOperator.GreaterThan,
                TokenType.GreaterThanOrEqual => BinaryOperator.GreaterThanOrEqual,
                TokenType.LessThan => BinaryOperator.LessThan,
                TokenType.LessThanOrEqual => BinaryOperator.LessThanOrEqual,
                TokenType.Contains => BinaryOperator.Contains,
                _ => (BinaryOperator?)null
            };

            if (op.HasValue)
            {
                Advance(); // Skip operator
                var right = ParsePrimaryExpression();
                return new BinaryOperatorNode(left, op.Value, right);
            }
        }

        return left;
    }

    private ExpressionNode ParsePrimaryExpression()
    {
        if (_position >= _tokens.Count)
        {
            throw new ExpressionParseException("Unexpected end of expression");
        }

        var token = _tokens[_position];

        switch (token.Type)
        {
            case TokenType.Identifier:
                Advance();
                return new VariableNode(token.Value);

            case TokenType.StringLiteral:
                Advance();
                return new LiteralNode(token.Value);

            case TokenType.NumberLiteral:
                Advance();
                if (decimal.TryParse(token.Value, out var decimalValue))
                {
                    return new LiteralNode(decimalValue);
                }
                throw new ExpressionParseException($"Invalid number format: {token.Value}");

            case TokenType.BooleanLiteral:
                Advance();
                var boolValue = string.Equals(token.Value, "true", StringComparison.OrdinalIgnoreCase);
                return new LiteralNode(boolValue);

            case TokenType.NullLiteral:
                Advance();
                return new LiteralNode(null!);

            case TokenType.LeftParen:
                Advance(); // Skip '('
                var expression = ParseOrExpression();
                
                if (_position >= _tokens.Count || _tokens[_position].Type != TokenType.RightParen)
                {
                    throw new ExpressionParseException("Missing closing parenthesis");
                }
                
                Advance(); // Skip ')'
                return expression;

            default:
                throw new ExpressionParseException($"Unexpected token: {token.Value}");
        }
    }

    private void Advance()
    {
        if (_position < _tokens.Count)
        {
            _position++;
        }
    }
}