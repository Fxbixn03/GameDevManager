using System.Globalization;

namespace GameDevManager.Domain.Curves;

/// <summary>
/// Ein Ausdruck, der sich nicht lesen ließ. <see cref="Position"/> ist die Stelle im Text,
/// an der es hakte — die Maske kann darauf zeigen.
/// </summary>
public sealed class CurveExpressionException(string message, int position) : Exception(message)
{
    public int Position { get; } = position;
}

/// <summary>
/// Ein einmal gelesener Formelausdruck über der Variablen <c>x</c>, bereit zum Auswerten.
/// <para>
/// Gelesen wird nach dem Shunting-Yard-Verfahren in eine umgekehrt polnische Notation; das
/// Auswerten läuft danach über einen Stapel ohne weiteres Parsen. Eine Levelkurve wird für die
/// Vorschau sechzig Mal ausgewertet — der Ausdruck wird deshalb einmal gelesen und nicht je Punkt.
/// </para>
/// <para>
/// Bewusst ein eigener kleiner Rechner statt einer Ausdrucksbibliothek: Gebraucht werden Zahlen,
/// vier Grundrechenarten, Potenz und eine Handvoll Funktionen — dasselbe Verhältnis wie beim
/// <c>ImageDimensionReader</c>, der die Bildmaße selbst liest statt eine Bildbibliothek zu holen.
/// Und weil das Ergebnis in den Export geht, muss es über alle Rechner hinweg dasselbe sein.
/// </para>
/// </summary>
public sealed class CurveExpression
{
    /// <summary>Die Variable, über der jede Kurve läuft — Stufe, Rang, Sekunde.</summary>
    public const string Variable = "x";

    private readonly IReadOnlyList<Token> _rpn;

    private CurveExpression(string text, IReadOnlyList<Token> rpn)
    {
        Text = text;
        _rpn = rpn;
    }

    /// <summary>Der ursprüngliche Text, so wie ihn der Nutzer geschrieben hat.</summary>
    public string Text { get; }

    /// <summary>Liest einen Ausdruck. Wirft <see cref="CurveExpressionException"/>, wenn er nicht aufgeht.</summary>
    public static CurveExpression Parse(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return new CurveExpression(expression, ToReversePolish(Tokenize(expression)));
    }

    /// <summary>Liest einen Ausdruck, ohne bei einem Fehler zu werfen.</summary>
    public static bool TryParse(string? expression, out CurveExpression? parsed)
    {
        parsed = null;

        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        try
        {
            parsed = Parse(expression);
            return true;
        }
        catch (CurveExpressionException)
        {
            return false;
        }
    }

    /// <summary>
    /// Wertet den Ausdruck für einen Wert von <c>x</c> aus. Rechenfehler wie eine Division
    /// durch null ergeben <see cref="double.NaN"/> oder Unendlich statt einer Ausnahme — die
    /// Vorschau lässt solche Punkte einfach aus, statt die ganze Kurve zu verweigern.
    /// </summary>
    public double Evaluate(double x)
    {
        var stack = new Stack<double>();

        foreach (var token in _rpn)
        {
            switch (token.Kind)
            {
                case TokenKind.Number:
                    stack.Push(token.Number);
                    break;

                case TokenKind.Variable:
                    stack.Push(x);
                    break;

                case TokenKind.Operator:
                    {
                        var right = Pop(stack, token);
                        var left = Pop(stack, token);
                        stack.Push(Apply(token.Text, left, right));
                        break;
                    }

                case TokenKind.Negate:
                    stack.Push(-Pop(stack, token));
                    break;

                case TokenKind.Function:
                    {
                        var arguments = new double[token.ArgumentCount];
                        for (var i = token.ArgumentCount - 1; i >= 0; i--)
                        {
                            arguments[i] = Pop(stack, token);
                        }

                        stack.Push(Call(token, arguments));
                        break;
                    }
            }
        }

        return stack.Count == 1
            ? stack.Pop()
            : throw new CurveExpressionException(ErrorMessages.Incomplete, Text.Length);
    }

    private static double Pop(Stack<double> stack, Token token) =>
        stack.Count > 0 ? stack.Pop() : throw new CurveExpressionException(ErrorMessages.Incomplete, token.Position);

    private static double Apply(string op, double left, double right) => op switch
    {
        "+" => left + right,
        "-" => left - right,
        "*" => left * right,
        "/" => left / right,
        "%" => left % right,
        "^" => Math.Pow(left, right),
        _ => double.NaN
    };

    private static double Call(Token token, double[] arguments)
    {
        var name = token.Text;
        var expected = FunctionArity(name);

        if (expected != arguments.Length)
        {
            throw new CurveExpressionException(
                string.Format(CultureInfo.InvariantCulture, ErrorMessages.WrongArity, name, expected),
                token.Position);
        }

        return name switch
        {
            "abs" => Math.Abs(arguments[0]),
            "ceil" => Math.Ceiling(arguments[0]),
            "exp" => Math.Exp(arguments[0]),
            "floor" => Math.Floor(arguments[0]),
            "log" => Math.Log(arguments[0]),
            "log10" => Math.Log10(arguments[0]),
            "max" => Math.Max(arguments[0], arguments[1]),
            "min" => Math.Min(arguments[0], arguments[1]),
            "pow" => Math.Pow(arguments[0], arguments[1]),
            "round" => Math.Round(arguments[0], MidpointRounding.AwayFromZero),
            "sign" => Math.Sign(arguments[0]),
            "sqrt" => Math.Sqrt(arguments[0]),
            _ => double.NaN
        };
    }

    /// <summary>Die bekannten Funktionen samt Stelligkeit — zugleich die Liste für die Hilfe in der Maske.</summary>
    public static IReadOnlyList<string> FunctionNames { get; } =
        ["abs", "ceil", "exp", "floor", "log", "log10", "max", "min", "pow", "round", "sign", "sqrt"];

    private static int FunctionArity(string name) => name switch
    {
        "max" or "min" or "pow" => 2,
        _ => 1
    };

    // ------------------------------------------------------------------------------ Lesen

    private static List<Token> Tokenize(string expression)
    {
        var tokens = new List<Token>();
        var index = 0;

        while (index < expression.Length)
        {
            var current = expression[index];

            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (char.IsAsciiDigit(current) || current == '.')
            {
                var start = index;
                while (index < expression.Length && (char.IsAsciiDigit(expression[index]) || expression[index] == '.'))
                {
                    index++;
                }

                var text = expression[start..index];

                // Bewusst die feste Kultur: Derselbe Ausdruck muss auf jedem Rechner dieselbe
                // Kurve ergeben, und er geht so auch in den Export.
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    throw new CurveExpressionException(
                        string.Format(CultureInfo.InvariantCulture, ErrorMessages.BadNumber, text), start);
                }

                tokens.Add(Token.Value(TokenKind.Number, text, start, number));
                continue;
            }

            if (char.IsAsciiLetter(current) || current == '_')
            {
                var start = index;
                while (index < expression.Length
                    && (char.IsAsciiLetterOrDigit(expression[index]) || expression[index] == '_'))
                {
                    index++;
                }

                var name = expression[start..index].ToLowerInvariant();

                if (name == Variable)
                {
                    tokens.Add(Token.Value(TokenKind.Variable, name, start, 0));
                }
                else if (name == "pi")
                {
                    tokens.Add(Token.Value(TokenKind.Number, name, start, Math.PI));
                }
                else if (name == "e")
                {
                    tokens.Add(Token.Value(TokenKind.Number, name, start, Math.E));
                }
                else if (FunctionNames.Contains(name))
                {
                    tokens.Add(Token.Value(TokenKind.Function, name, start, 0));
                }
                else
                {
                    throw new CurveExpressionException(
                        string.Format(CultureInfo.InvariantCulture, ErrorMessages.UnknownName, name), start);
                }

                continue;
            }

            switch (current)
            {
                case '(':
                    tokens.Add(Token.Value(TokenKind.OpenParenthesis, "(", index, 0));
                    break;
                case ')':
                    tokens.Add(Token.Value(TokenKind.CloseParenthesis, ")", index, 0));
                    break;
                case ',' or ';':
                    tokens.Add(Token.Value(TokenKind.Separator, ",", index, 0));
                    break;
                case '+' or '-' or '*' or '/' or '%' or '^':
                    tokens.Add(Token.Value(TokenKind.Operator, current.ToString(), index, 0));
                    break;
                default:
                    throw new CurveExpressionException(
                        string.Format(CultureInfo.InvariantCulture, ErrorMessages.UnknownCharacter, current), index);
            }

            index++;
        }

        return tokens;
    }

    /// <summary>
    /// Shunting-Yard: Aus der geschriebenen Reihenfolge wird die Rechenreihenfolge. Ein Minus
    /// am Anfang oder nach einem Operator ist ein Vorzeichen und kein Abziehen — sonst hätte
    /// <c>-x^2</c> keinen linken Operanden.
    /// </summary>
    private static List<Token> ToReversePolish(List<Token> tokens)
    {
        var output = new List<Token>();
        var operators = new Stack<Token>();
        var arity = new Stack<int>();
        var expectValue = true;

        foreach (var token in tokens)
        {
            switch (token.Kind)
            {
                case TokenKind.Number:
                case TokenKind.Variable:
                    output.Add(token);
                    expectValue = false;
                    break;

                case TokenKind.Function:
                    operators.Push(token);
                    expectValue = true;
                    break;

                case TokenKind.Separator:
                    PopUntilParenthesis(operators, output, token);
                    if (arity.Count == 0)
                    {
                        throw new CurveExpressionException(ErrorMessages.SeparatorOutsideCall, token.Position);
                    }

                    arity.Push(arity.Pop() + 1);
                    expectValue = true;
                    break;

                case TokenKind.Operator when expectValue && token.Text == "-":
                    operators.Push(token with { Kind = TokenKind.Negate });
                    break;

                case TokenKind.Operator when expectValue && token.Text == "+":
                    // Ein führendes Plus ändert nichts und wird verworfen.
                    break;

                case TokenKind.Operator:
                    // Das Vorzeichen bindet schwächer als die Potenz: „-x^2“ ist -(x²) und
                    // nicht (-x)². Vor allen anderen Operatoren wird es dagegen gerechnet.
                    while (operators.TryPeek(out var top)
                        && ((top.Kind == TokenKind.Negate && token.Text != "^")
                            || (top.Kind == TokenKind.Operator && HasPrecedence(top, token))))
                    {
                        output.Add(operators.Pop());
                    }

                    operators.Push(token);
                    expectValue = true;
                    break;

                case TokenKind.OpenParenthesis:
                    operators.Push(token);
                    arity.Push(1);
                    expectValue = true;
                    break;

                case TokenKind.CloseParenthesis:
                    {
                        PopUntilParenthesis(operators, output, token);
                        operators.Pop();

                        var arguments = arity.Count > 0 ? arity.Pop() : 1;

                        if (operators.TryPeek(out var function) && function.Kind == TokenKind.Function)
                        {
                            output.Add(operators.Pop() with { ArgumentCount = arguments });
                        }

                        expectValue = false;
                        break;
                    }
            }
        }

        while (operators.TryPop(out var rest))
        {
            if (rest.Kind is TokenKind.OpenParenthesis or TokenKind.CloseParenthesis)
            {
                throw new CurveExpressionException(ErrorMessages.UnbalancedParenthesis, rest.Position);
            }

            output.Add(rest);
        }

        if (output.Count == 0)
        {
            throw new CurveExpressionException(ErrorMessages.Empty, 0);
        }

        EnsureComplete(output);
        return output;
    }

    /// <summary>
    /// Prüft schon beim Lesen, dass jeder Operator seine Operanden hat und am Ende genau ein
    /// Wert übrig bleibt — <c>1 +</c> ist sonst erst beim Rechnen aufgefallen, also nachdem
    /// die Maske die Formel längst angenommen hat.
    /// <para>
    /// Gezählt wird die Stapeltiefe, die das Auswerten hätte: Werte legen einen ab, ein
    /// Operator nimmt zwei und legt einen zurück, eine Funktion so viele, wie sie Argumente hat.
    /// </para>
    /// </summary>
    private static void EnsureComplete(List<Token> rpn)
    {
        var depth = 0;

        foreach (var token in rpn)
        {
            var consumed = token.Kind switch
            {
                TokenKind.Number or TokenKind.Variable => 0,
                TokenKind.Negate => 1,
                TokenKind.Function => token.ArgumentCount,
                _ => 2
            };

            if (depth < consumed)
            {
                throw new CurveExpressionException(ErrorMessages.Incomplete, token.Position);
            }

            depth = depth - consumed + 1;
        }

        if (depth != 1)
        {
            throw new CurveExpressionException(ErrorMessages.Incomplete, rpn[^1].Position);
        }
    }

    private static void PopUntilParenthesis(Stack<Token> operators, List<Token> output, Token token)
    {
        while (operators.TryPeek(out var top) && top.Kind != TokenKind.OpenParenthesis)
        {
            output.Add(operators.Pop());
        }

        if (operators.Count == 0)
        {
            throw new CurveExpressionException(ErrorMessages.UnbalancedParenthesis, token.Position);
        }
    }

    /// <summary>
    /// Ob der obere Operator vor dem neuen gerechnet wird. Die Potenz ist rechtsassoziativ:
    /// <c>2^3^2</c> ist 2^(3^2) und nicht (2^3)^2.
    /// </summary>
    private static bool HasPrecedence(Token top, Token incoming)
    {
        var topLevel = Precedence(top.Text);
        var incomingLevel = Precedence(incoming.Text);

        return incoming.Text == "^"
            ? topLevel > incomingLevel
            : topLevel >= incomingLevel;
    }

    private static int Precedence(string op) => op switch
    {
        "+" or "-" => 1,
        "*" or "/" or "%" => 2,
        "^" => 3,
        _ => 0
    };

    private enum TokenKind
    {
        Number,
        Variable,
        Operator,
        Negate,
        Function,
        Separator,
        OpenParenthesis,
        CloseParenthesis
    }

    private readonly record struct Token(
        TokenKind Kind, string Text, int Position, double Number, int ArgumentCount)
    {
        public static Token Value(TokenKind kind, string text, int position, double number) =>
            new(kind, text, position, number, 1);
    }

    /// <summary>
    /// Die technischen Meldungen. Sie stehen bewusst hier und nicht in einer resx: Die
    /// Domain-Schicht hat keine Lokalisierung, und die Maske zeigt ohnehin ihren eigenen
    /// verständlichen Satz — diese Texte sind der Zusatz für den, der die Formel schreibt.
    /// </summary>
    private static class ErrorMessages
    {
        public const string Empty = "Der Ausdruck ist leer.";
        public const string Incomplete = "Dem Ausdruck fehlt ein Operand.";
        public const string UnbalancedParenthesis = "Die Klammern sind nicht ausgeglichen.";
        public const string SeparatorOutsideCall = "Ein Komma steht außerhalb eines Funktionsaufrufs.";
        public const string BadNumber = "'{0}' ist keine Zahl.";
        public const string UnknownName = "'{0}' ist weder Variable noch bekannte Funktion.";
        public const string UnknownCharacter = "Das Zeichen '{0}' gehört nicht in einen Ausdruck.";
        public const string WrongArity = "'{0}' erwartet {1} Argument(e).";
    }
}
