using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace RoguelikeToolkit.Entities;

/// <summary>
/// A parsed, immutable dice expression such as <c>2d6+3</c>, <c>4d6k3</c> or <c>(1d8+2)*2</c>.
/// </summary>
/// <remarks>
/// <para>Grammar (case-insensitive, whitespace ignored):</para>
/// <code>
/// expr    := term (('+' | '-') term)*
/// term    := unary (('*' | '/') unary)*
/// unary   := '-' unary | primary
/// primary := '(' expr ')'
///          | [N] 'd' (S | '%') [keep]     N dice with S sides ('%' = 100), N defaults to 1
///          | N 'k' K                      roll-and-keep: N d10, keep the K highest
///          | N                            constant
/// keep    := ('k' | 'kh' | 'kl') K        keep the K highest ('k', 'kh') or lowest ('kl')
/// </code>
/// <para>
/// Parsing compiles the expression into a flat postfix program once; <see cref="Roll()"/> evaluates it on a small
/// stack without allocating (keep-rolls rent a pooled buffer). Instances are thread-safe and meant to be shared.
/// Division is integer division truncating toward zero; arithmetic is checked and throws on overflow.
/// </para>
/// </remarks>
public sealed class Dice
{
    /// <summary>Upper bound for the number of dice in a single roll term (guards templates against absurd input).</summary>
    public const int MaxDiceCount = 1000;

    /// <summary>Upper bound for the number of sides of a die.</summary>
    public const int MaxSides = 1_000_000;

    private const int StackAllocThreshold = 64;

    [ThreadStatic]
    private static Random? _threadRandom;

    private readonly Instruction[] _program;
    private readonly int _maxStackDepth;

    private Dice(string expression, Instruction[] program, int maxStackDepth)
    {
        Expression = expression;
        _program = program;
        _maxStackDepth = maxStackDepth;
    }

    private enum OpCode : byte
    {
        Constant,
        Roll,
        Add,
        Subtract,
        Multiply,
        Divide,
        Negate,
    }

    private enum KeepMode : byte
    {
        All,
        Highest,
        Lowest,
    }

    /// <summary>Gets the source expression this instance was parsed from (trimmed).</summary>
    public string Expression { get; }

    /// <summary>Parses a dice expression.</summary>
    /// <param name="expression">Expression to parse, e.g. <c>2d6+3</c>.</param>
    /// <returns>The parsed expression.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> is null.</exception>
    /// <exception cref="FormatException">The expression is malformed or exceeds <see cref="MaxDiceCount"/>/<see cref="MaxSides"/>.</exception>
    public static Dice Parse(string expression)
    {
        if (expression == null)
        {
            throw new ArgumentNullException(nameof(expression));
        }

        var parser = new Parser(expression);
        var result = parser.ParseRoot(out var error);
        return result ?? throw new FormatException($"Invalid dice expression '{expression}': {error}");
    }

    /// <summary>Tries to parse a dice expression.</summary>
    /// <param name="expression">Expression to parse, e.g. <c>2d6+3</c>.</param>
    /// <param name="dice">The parsed expression on success; otherwise null.</param>
    /// <returns>true if parsing succeeded.</returns>
    public static bool TryParse(string? expression, [NotNullWhen(true)] out Dice? dice)
    {
        if (expression == null)
        {
            dice = null;
            return false;
        }

        dice = new Parser(expression).ParseRoot(out _);
        return dice != null;
    }

    /// <summary>Rolls the expression using a thread-local random source.</summary>
    /// <returns>The rolled value.</returns>
    public int Roll() => Roll(_threadRandom ??= new Random(Guid.NewGuid().GetHashCode()));

    /// <summary>Rolls the expression using the given random source (pass a seeded instance for reproducible rolls).</summary>
    /// <param name="random">Random source to draw dice from.</param>
    /// <returns>The rolled value.</returns>
    public int Roll(Random random)
    {
        if (random == null)
        {
            throw new ArgumentNullException(nameof(random));
        }

        var stack = _maxStackDepth <= StackAllocThreshold
            ? stackalloc int[_maxStackDepth]
            : new int[_maxStackDepth];
        var top = 0;

        foreach (var instruction in _program)
        {
            switch (instruction.Code)
            {
                case OpCode.Constant:
                    stack[top++] = instruction.A;
                    break;
                case OpCode.Roll:
                    stack[top++] = RollTerm(random, instruction.A, instruction.B, instruction.C, instruction.Keep);
                    break;
                case OpCode.Negate:
                    stack[top - 1] = checked(-stack[top - 1]);
                    break;
                default:
                    var right = stack[--top];
                    var left = stack[top - 1];
                    stack[top - 1] = instruction.Code switch
                    {
                        OpCode.Add => checked(left + right),
                        OpCode.Subtract => checked(left - right),
                        OpCode.Multiply => checked(left * right),
                        _ => checked(left / right),
                    };
                    break;
            }
        }

        return stack[0];
    }

    /// <inheritdoc/>
    public override string ToString() => Expression;

    private static int RollTerm(Random random, int count, int sides, int keep, KeepMode mode)
    {
        if (mode == KeepMode.All)
        {
            var sum = 0;
            for (var i = 0; i < count; i++)
            {
                sum += random.Next(1, sides + 1);
            }

            return sum;
        }

        // count <= MaxDiceCount, so sorting a pooled buffer is cheap and keeps this allocation-free.
        var rolls = ArrayPool<int>.Shared.Rent(count);
        try
        {
            for (var i = 0; i < count; i++)
            {
                rolls[i] = random.Next(1, sides + 1);
            }

            Array.Sort(rolls, 0, count);

            var kept = 0;
            var start = mode == KeepMode.Lowest ? 0 : count - keep;
            for (var i = start; i < start + keep; i++)
            {
                kept += rolls[i];
            }

            return kept;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(rolls);
        }
    }

    private readonly struct Instruction
    {
        public readonly OpCode Code;
        public readonly KeepMode Keep;
        public readonly int A;
        public readonly int B;
        public readonly int C;

        public Instruction(OpCode code, int a = 0, int b = 0, int c = 0, KeepMode keep = KeepMode.All)
        {
            Code = code;
            A = a;
            B = b;
            C = c;
            Keep = keep;
        }
    }

    /// <summary>
    /// Recursive-descent parser emitting postfix instructions directly; tracks stack depth so evaluation can size its
    /// stack up front. Errors are reported through <c>_error</c> instead of exceptions so TryParse stays exception-free.
    /// </summary>
    private ref struct Parser
    {
        private const int MaxNesting = 64;

        private readonly string _source;
        private readonly List<Instruction> _program;
        private int _pos;
        private int _depth;
        private int _maxDepth;
        private int _nesting;
        private string? _error;

        public Parser(string source)
        {
            _source = source;
            _program = new List<Instruction>();
            _pos = 0;
            _depth = 0;
            _maxDepth = 0;
            _nesting = 0;
            _error = null;
        }

        public static bool IsDigit(char c) => c >= '0' && c <= '9';

        public Dice? ParseRoot(out string? error)
        {
            SkipWhitespace();
            if (_pos == _source.Length)
            {
                error = "expression is empty";
                return null;
            }

            if (ParseExpression())
            {
                SkipWhitespace();
                if (_pos != _source.Length)
                {
                    Fail($"unexpected '{_source[_pos]}'");
                }
            }

            error = _error;
            return _error == null ? new Dice(_source.Trim(), _program.ToArray(), _maxDepth) : null;
        }

        private bool ParseExpression()
        {
            if (++_nesting > MaxNesting)
            {
                return Fail($"nesting deeper than {MaxNesting}");
            }

            if (!ParseTerm())
            {
                return false;
            }

            while (true)
            {
                var op = Peek();
                if (op != '+' && op != '-')
                {
                    break;
                }

                _pos++;
                if (!ParseTerm())
                {
                    return false;
                }

                EmitBinary(op == '+' ? OpCode.Add : OpCode.Subtract);
            }

            _nesting--;
            return true;
        }

        private bool ParseTerm()
        {
            if (!ParseUnary())
            {
                return false;
            }

            while (true)
            {
                var op = Peek();
                if (op != '*' && op != '/')
                {
                    return true;
                }

                _pos++;
                if (!ParseUnary())
                {
                    return false;
                }

                EmitBinary(op == '*' ? OpCode.Multiply : OpCode.Divide);
            }
        }

        private bool ParseUnary()
        {
            if (Peek() != '-')
            {
                return ParsePrimary();
            }

            _pos++;
            if (++_nesting > MaxNesting)
            {
                return Fail($"nesting deeper than {MaxNesting}");
            }

            if (!ParseUnary())
            {
                return false;
            }

            _nesting--;
            _program.Add(new Instruction(OpCode.Negate));
            return true;
        }

        private bool ParsePrimary()
        {
            var c = Peek();
            if (c == '(')
            {
                _pos++;
                if (!ParseExpression())
                {
                    return false;
                }

                if (Peek() != ')')
                {
                    return Fail("expected ')'");
                }

                _pos++;
                return true;
            }

            var count = 1;
            var hasCount = false;
            if (IsDigit(c))
            {
                if (!ParseNumber(out count))
                {
                    return false;
                }

                hasCount = true;
            }

            switch (char.ToLowerInvariant(Peek()))
            {
                case 'd':
                    _pos++;
                    return ParseDiceTail(count);
                case 'k' when hasCount:
                    // Legacy roll-and-keep shorthand: NkK == Nd10kK.
                    return ParseKeep(count, 10);
                default:
                    if (!hasCount)
                    {
                        return Fail(_pos < _source.Length ? $"unexpected '{_source[_pos]}'" : "unexpected end of expression");
                    }

                    Emit(new Instruction(OpCode.Constant, count));
                    return true;
            }
        }

        private bool ParseDiceTail(int count)
        {
            int sides;
            if (Peek() == '%')
            {
                _pos++;
                sides = 100;
            }
            else if (!IsDigit(Peek()))
            {
                return Fail("expected number of sides after 'd'");
            }
            else if (!ParseNumber(out sides))
            {
                return false;
            }

            if (count < 1 || count > MaxDiceCount)
            {
                return Fail($"dice count must be between 1 and {MaxDiceCount}, got {count}");
            }

            if (sides < 1 || sides > MaxSides)
            {
                return Fail($"number of sides must be between 1 and {MaxSides}, got {sides}");
            }

            if (char.ToLowerInvariant(Peek()) == 'k')
            {
                return ParseKeep(count, sides);
            }

            Emit(new Instruction(OpCode.Roll, count, sides));
            return true;
        }

        private bool ParseKeep(int count, int sides)
        {
            _pos++; // 'k'
            var mode = KeepMode.Highest;
            if (_pos < _source.Length)
            {
                var modifier = char.ToLowerInvariant(_source[_pos]);
                if (modifier == 'h' || modifier == 'l')
                {
                    mode = modifier == 'l' ? KeepMode.Lowest : KeepMode.Highest;
                    _pos++;
                }
            }

            if (!IsDigit(Peek()))
            {
                return Fail("expected number of dice to keep");
            }

            if (!ParseNumber(out var keep))
            {
                return false;
            }

            if (count < 1 || count > MaxDiceCount)
            {
                return Fail($"dice count must be between 1 and {MaxDiceCount}, got {count}");
            }

            if (keep < 1 || keep > count)
            {
                return Fail($"keep count must be between 1 and {count}, got {keep}");
            }

            Emit(keep == count
                ? new Instruction(OpCode.Roll, count, sides)
                : new Instruction(OpCode.Roll, count, sides, keep, mode));
            return true;
        }

        private bool ParseNumber(out int value)
        {
            SkipWhitespace();
            value = 0;
            var start = _pos;
            while (_pos < _source.Length && IsDigit(_source[_pos]))
            {
                var digit = _source[_pos] - '0';
                if (value > (int.MaxValue - digit) / 10)
                {
                    return Fail($"number at position {start} is too large");
                }

                value = (value * 10) + digit;
                _pos++;
            }

            return true;
        }

        private void Emit(Instruction instruction)
        {
            _program.Add(instruction);
            if (++_depth > _maxDepth)
            {
                _maxDepth = _depth;
            }
        }

        private void EmitBinary(OpCode code)
        {
            _program.Add(new Instruction(code));
            _depth--;
        }

        private char Peek()
        {
            SkipWhitespace();
            return _pos < _source.Length ? _source[_pos] : '\0';
        }

        private void SkipWhitespace()
        {
            while (_pos < _source.Length && char.IsWhiteSpace(_source[_pos]))
            {
                _pos++;
            }
        }

        private bool Fail(string message)
        {
            _error ??= $"{message} (at position {_pos})";
            return false;
        }
    }
}
