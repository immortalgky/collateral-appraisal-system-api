namespace Workflow.Workflow.Engine.Expression;

public abstract class ExpressionNode
{
    public abstract object Evaluate(Dictionary<string, object> variables);
}

public class VariableNode : ExpressionNode
{
    public string VariableName { get; }

    public VariableNode(string variableName)
    {
        VariableName = variableName;
    }

    public override object Evaluate(Dictionary<string, object> variables)
    {
        return variables.TryGetValue(VariableName, out var value) ? value : null!;
    }
}

public class LiteralNode : ExpressionNode
{
    public object Value { get; }

    public LiteralNode(object value)
    {
        Value = value;
    }

    public override object Evaluate(Dictionary<string, object> variables)
    {
        return Value;
    }
}

public class BinaryOperatorNode : ExpressionNode
{
    public ExpressionNode Left { get; }
    public ExpressionNode Right { get; }
    public BinaryOperator Operator { get; }

    public BinaryOperatorNode(ExpressionNode left, BinaryOperator op, ExpressionNode right)
    {
        Left = left;
        Right = right;
        Operator = op;
    }

    public override object Evaluate(Dictionary<string, object> variables)
    {
        var leftValue = Left.Evaluate(variables);
        var rightValue = Right.Evaluate(variables);

        return Operator switch
        {
            BinaryOperator.Equal => CompareValues(leftValue, rightValue) == 0,
            BinaryOperator.NotEqual => CompareValues(leftValue, rightValue) != 0,
            BinaryOperator.GreaterThan => CompareValues(leftValue, rightValue) > 0,
            BinaryOperator.GreaterThanOrEqual => CompareValues(leftValue, rightValue) >= 0,
            BinaryOperator.LessThan => CompareValues(leftValue, rightValue) < 0,
            BinaryOperator.LessThanOrEqual => CompareValues(leftValue, rightValue) <= 0,
            BinaryOperator.Contains => ContainsValue(leftValue, rightValue),
            BinaryOperator.And => IsTruthy(leftValue) && IsTruthy(rightValue),
            BinaryOperator.Or => IsTruthy(leftValue) || IsTruthy(rightValue),
            _ => throw new InvalidOperationException($"Unknown binary operator: {Operator}")
        };
    }

    private int CompareValues(object? left, object? right)
    {
        if (left == null && right == null) return 0;
        if (left == null) return -1;
        if (right == null) return 1;

        if (left is string leftStr && right is string rightStr)
            return string.Compare(leftStr, rightStr, StringComparison.OrdinalIgnoreCase);

        if (IsNumeric(left) && IsNumeric(right))
        {
            var leftDecimal = Convert.ToDecimal(left);
            var rightDecimal = Convert.ToDecimal(right);
            return leftDecimal.CompareTo(rightDecimal);
        }

        if (left is DateTime leftDate && right is DateTime rightDate)
            return leftDate.CompareTo(rightDate);

        if (left is bool leftBool && right is bool rightBool)
            return leftBool.CompareTo(rightBool);

        return string.Compare(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private bool ContainsValue(object? container, object? value)
    {
        if (container == null || value == null) return false;

        var containerStr = container.ToString();
        var valueStr = value.ToString();

        return !string.IsNullOrEmpty(containerStr) && !string.IsNullOrEmpty(valueStr) &&
               containerStr.Contains(valueStr, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsTruthy(object? value)
    {
        if (value == null) return false;
        if (value is bool boolValue) return boolValue;
        if (IsNumeric(value)) return Convert.ToDecimal(value) != 0;
        if (value is string strValue) return !string.IsNullOrWhiteSpace(strValue);
        return true;
    }

    private bool IsNumeric(object value)
    {
        return value is byte || value is sbyte || value is short || value is ushort ||
               value is int || value is uint || value is long || value is ulong ||
               value is float || value is double || value is decimal;
    }
}

public class UnaryOperatorNode : ExpressionNode
{
    public ExpressionNode Operand { get; }
    public UnaryOperator Operator { get; }

    public UnaryOperatorNode(UnaryOperator op, ExpressionNode operand)
    {
        Operator = op;
        Operand = operand;
    }

    public override object Evaluate(Dictionary<string, object> variables)
    {
        var operandValue = Operand.Evaluate(variables);

        return Operator switch
        {
            UnaryOperator.Not => !IsTruthy(operandValue),
            _ => throw new InvalidOperationException($"Unknown unary operator: {Operator}")
        };
    }

    private bool IsTruthy(object? value)
    {
        if (value == null) return false;
        if (value is bool boolValue) return boolValue;
        if (value is string strValue) return !string.IsNullOrWhiteSpace(strValue);
        return true;
    }
}

public enum BinaryOperator
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Contains,
    And,
    Or
}

public enum UnaryOperator
{
    Not
}