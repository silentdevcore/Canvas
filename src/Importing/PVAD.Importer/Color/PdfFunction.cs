using System.Globalization;
using PVAD.Importer.Objects;
using PVAD.Importer.Streams;

namespace PVAD.Importer.Color;

/// <summary>
/// A PDF function (ISO 32000-1, 7.10). Needed because Separation and DeviceN colours are
/// only defined through the tint transform that maps them into an alternate space.
/// </summary>
public abstract class PdfFunction
{
    protected PdfFunction(IReadOnlyList<double> domain, IReadOnlyList<double>? range)
    {
        Domain = domain;
        Range = range;
    }

    public IReadOnlyList<double> Domain { get; }
    public IReadOnlyList<double>? Range { get; }

    public abstract int OutputCount { get; }

    public double[] Evaluate(IReadOnlyList<double> inputs)
    {
        var clamped = new double[inputs.Count];
        for (var index = 0; index < inputs.Count; index++)
        {
            clamped[index] = index * 2 + 1 < Domain.Count
                ? Math.Clamp(inputs[index], Domain[index * 2], Domain[index * 2 + 1])
                : inputs[index];
        }

        var outputs = EvaluateCore(clamped);
        if (Range is null)
        {
            return outputs;
        }

        for (var index = 0; index < outputs.Length && index * 2 + 1 < Range.Count; index++)
        {
            outputs[index] = Math.Clamp(outputs[index], Range[index * 2], Range[index * 2 + 1]);
        }

        return outputs;
    }

    protected abstract double[] EvaluateCore(double[] inputs);

    /// <summary>Resolves a function object, or returns null when the type is not supported.</summary>
    public static PdfFunction? TryCreate(PdfObject? value, Func<PdfObject?, PdfObject?> resolve, int depth = 0)
    {
        if (depth > 8)
        {
            return null;
        }

        var resolved = resolve(value);
        if (resolved is PdfArray array)
        {
            // An array of functions, one per output component.
            var parts = new List<PdfFunction>(array.Items.Count);
            foreach (var item in array.Items)
            {
                var part = TryCreate(item, resolve, depth + 1);
                if (part is null)
                {
                    return null;
                }

                parts.Add(part);
            }

            return parts.Count == 0 ? null : new PdfFunctionArray(parts);
        }

        var dictionary = resolved switch
        {
            PdfStreamObject stream => stream.Dictionary,
            PdfDictionary direct => direct,
            _ => null
        };
        if (dictionary is null)
        {
            return null;
        }

        var domain = ReadNumbers(dictionary["Domain"], resolve);
        var range = ReadNumbers(dictionary["Range"], resolve);
        return ReadInteger(dictionary["FunctionType"], resolve) switch
        {
            0 when resolved is PdfStreamObject sampled => PdfSampledFunction.TryCreate(sampled, domain, range, resolve),
            2 => new PdfExponentialFunction(
                domain,
                range,
                ReadNumbers(dictionary["C0"], resolve) is { Count: > 0 } c0 ? c0 : [0d],
                ReadNumbers(dictionary["C1"], resolve) is { Count: > 0 } c1 ? c1 : [1d],
                ReadNumber(dictionary["N"], resolve, 1d)),
            3 => PdfStitchingFunction.TryCreate(dictionary, domain, range, resolve, depth),
            4 when resolved is PdfStreamObject postScript =>
                PdfPostScriptFunction.TryCreate(postScript, domain, range),
            _ => null
        };
    }

    internal static IReadOnlyList<double> ReadNumbers(PdfObject? value, Func<PdfObject?, PdfObject?> resolve)
    {
        if (resolve(value) is not PdfArray array)
        {
            return [];
        }

        var numbers = new List<double>(array.Items.Count);
        foreach (var item in array.Items)
        {
            numbers.Add(ReadNumber(item, resolve, 0d));
        }

        return numbers;
    }

    internal static double ReadNumber(PdfObject? value, Func<PdfObject?, PdfObject?> resolve, double fallback) =>
        resolve(value) switch
        {
            PdfInteger integer => integer.Value,
            PdfNumber number => number.Value,
            _ => fallback
        };

    internal static int ReadInteger(PdfObject? value, Func<PdfObject?, PdfObject?> resolve) =>
        (int)ReadNumber(value, resolve, -1);
}

/// <summary>Several single-output functions acting as one multi-output function.</summary>
internal sealed class PdfFunctionArray(IReadOnlyList<PdfFunction> parts)
    : PdfFunction(parts[0].Domain, null)
{
    public override int OutputCount => parts.Count;

    protected override double[] EvaluateCore(double[] inputs)
    {
        var outputs = new double[parts.Count];
        for (var index = 0; index < parts.Count; index++)
        {
            var value = parts[index].Evaluate(inputs);
            outputs[index] = value.Length > 0 ? value[0] : 0d;
        }

        return outputs;
    }
}

/// <summary>Type 2: exponential interpolation between C0 and C1.</summary>
internal sealed class PdfExponentialFunction(
    IReadOnlyList<double> domain,
    IReadOnlyList<double>? range,
    IReadOnlyList<double> c0,
    IReadOnlyList<double> c1,
    double exponent) : PdfFunction(domain, range)
{
    public override int OutputCount => Math.Max(c0.Count, c1.Count);

    protected override double[] EvaluateCore(double[] inputs)
    {
        var x = inputs.Length > 0 ? inputs[0] : 0d;
        var factor = exponent == 1d ? x : Math.Pow(x, exponent);
        var outputs = new double[OutputCount];
        for (var index = 0; index < outputs.Length; index++)
        {
            var start = index < c0.Count ? c0[index] : 0d;
            var end = index < c1.Count ? c1[index] : 1d;
            outputs[index] = start + factor * (end - start);
        }

        return outputs;
    }
}

/// <summary>Type 3: a sequence of subfunctions stitched across the domain.</summary>
internal sealed class PdfStitchingFunction : PdfFunction
{
    private readonly IReadOnlyList<PdfFunction> _functions;
    private readonly IReadOnlyList<double> _bounds;
    private readonly IReadOnlyList<double> _encode;

    private PdfStitchingFunction(
        IReadOnlyList<double> domain,
        IReadOnlyList<double>? range,
        IReadOnlyList<PdfFunction> functions,
        IReadOnlyList<double> bounds,
        IReadOnlyList<double> encode) : base(domain, range)
    {
        _functions = functions;
        _bounds = bounds;
        _encode = encode;
    }

    public override int OutputCount => _functions[0].OutputCount;

    public static PdfFunction? TryCreate(
        PdfDictionary dictionary,
        IReadOnlyList<double> domain,
        IReadOnlyList<double>? range,
        Func<PdfObject?, PdfObject?> resolve,
        int depth)
    {
        if (resolve(dictionary["Functions"]) is not PdfArray array || domain.Count < 2)
        {
            return null;
        }

        var functions = new List<PdfFunction>(array.Items.Count);
        foreach (var item in array.Items)
        {
            var function = TryCreate(item, resolve, depth + 1);
            if (function is null)
            {
                return null;
            }

            functions.Add(function);
        }

        if (functions.Count == 0)
        {
            return null;
        }

        return new PdfStitchingFunction(
            domain,
            range,
            functions,
            ReadNumbers(dictionary["Bounds"], resolve),
            ReadNumbers(dictionary["Encode"], resolve));
    }

    protected override double[] EvaluateCore(double[] inputs)
    {
        var x = inputs.Length > 0 ? inputs[0] : 0d;
        var index = 0;
        while (index < _bounds.Count && x >= _bounds[index])
        {
            index++;
        }

        index = Math.Min(index, _functions.Count - 1);
        var low = index == 0 ? Domain[0] : _bounds[index - 1];
        var high = index == _bounds.Count ? Domain[1] : _bounds[index];
        var encodeLow = index * 2 < _encode.Count ? _encode[index * 2] : 0d;
        var encodeHigh = index * 2 + 1 < _encode.Count ? _encode[index * 2 + 1] : 1d;
        var encoded = high - low <= double.Epsilon
            ? encodeLow
            : encodeLow + (x - low) / (high - low) * (encodeHigh - encodeLow);
        return _functions[index].Evaluate([encoded]);
    }
}

/// <summary>Type 0: a sampled function with multilinear interpolation.</summary>
internal sealed class PdfSampledFunction : PdfFunction
{
    private readonly int[] _size;
    private readonly int _bitsPerSample;
    private readonly IReadOnlyList<double> _encode;
    private readonly IReadOnlyList<double> _decode;
    private readonly byte[] _samples;
    private readonly int _outputCount;

    private PdfSampledFunction(
        IReadOnlyList<double> domain,
        IReadOnlyList<double>? range,
        int[] size,
        int bitsPerSample,
        IReadOnlyList<double> encode,
        IReadOnlyList<double> decode,
        byte[] samples,
        int outputCount) : base(domain, range)
    {
        _size = size;
        _bitsPerSample = bitsPerSample;
        _encode = encode;
        _decode = decode;
        _samples = samples;
        _outputCount = outputCount;
    }

    public override int OutputCount => _outputCount;

    public static PdfFunction? TryCreate(
        PdfStreamObject stream,
        IReadOnlyList<double> domain,
        IReadOnlyList<double>? range,
        Func<PdfObject?, PdfObject?> resolve)
    {
        if (range is null || range.Count == 0)
        {
            return null;
        }

        var sizes = ReadNumbers(stream.Dictionary["Size"], resolve).Select(static value => (int)value).ToArray();
        var bits = ReadInteger(stream.Dictionary["BitsPerSample"], resolve);
        if (sizes.Length == 0 || sizes.Length > 12 || sizes.Any(static value => value <= 0) ||
            bits is not (1 or 2 or 4 or 8 or 12 or 16 or 24 or 32))
        {
            return null;
        }

        byte[] samples;
        try
        {
            samples = new PdfStreamDecoderRegistry().Decode(stream).ToArray();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return null;
        }

        return new PdfSampledFunction(
            domain,
            range,
            sizes,
            bits,
            ReadNumbers(stream.Dictionary["Encode"], resolve),
            ReadNumbers(stream.Dictionary["Decode"], resolve),
            samples,
            range.Count / 2);
    }

    protected override double[] EvaluateCore(double[] inputs)
    {
        var lower = new int[_size.Length];
        var upper = new int[_size.Length];
        var fractions = new double[_size.Length];
        for (var dimension = 0; dimension < _size.Length; dimension++)
        {
            var value = dimension < inputs.Length ? inputs[dimension] : 0d;
            var domainLow = dimension * 2 < Domain.Count ? Domain[dimension * 2] : 0d;
            var domainHigh = dimension * 2 + 1 < Domain.Count ? Domain[dimension * 2 + 1] : 1d;
            var encodeLow = dimension * 2 < _encode.Count ? _encode[dimension * 2] : 0d;
            var encodeHigh = dimension * 2 + 1 < _encode.Count ? _encode[dimension * 2 + 1] : _size[dimension] - 1;
            var encoded = domainHigh - domainLow <= double.Epsilon
                ? encodeLow
                : encodeLow + (value - domainLow) / (domainHigh - domainLow) * (encodeHigh - encodeLow);
            encoded = Math.Clamp(encoded, 0, _size[dimension] - 1);
            lower[dimension] = (int)Math.Floor(encoded);
            upper[dimension] = (int)Math.Ceiling(encoded);
            fractions[dimension] = encoded - lower[dimension];
        }

        var outputs = new double[_outputCount];
        var maximum = Math.Pow(2, _bitsPerSample) - 1;
        var cornerCount = 1 << _size.Length;
        for (var corner = 0; corner < cornerCount; corner++)
        {
            var sampleIndex = 0L;
            var stride = 1L;
            var weight = 1d;
            for (var dimension = 0; dimension < _size.Length; dimension++)
            {
                var useUpper = (corner & (1 << dimension)) != 0;
                sampleIndex += (useUpper ? upper[dimension] : lower[dimension]) * stride;
                weight *= useUpper ? fractions[dimension] : 1d - fractions[dimension];
                stride *= _size[dimension];
            }

            if (weight <= 0)
            {
                continue;
            }

            for (var output = 0; output < _outputCount; output++)
            {
                var bitOffset = (sampleIndex * _outputCount + output) * _bitsPerSample;
                var raw = ReadSample(bitOffset);
                var decodeLow = output * 2 < _decode.Count ? _decode[output * 2] : Range![output * 2];
                var decodeHigh = output * 2 + 1 < _decode.Count ? _decode[output * 2 + 1] : Range![output * 2 + 1];
                outputs[output] += weight * (decodeLow + raw / maximum * (decodeHigh - decodeLow));
            }
        }

        return outputs;
    }

    private double ReadSample(long bitOffset)
    {
        long value = 0;
        for (var bit = 0; bit < _bitsPerSample; bit++)
        {
            var absolute = bitOffset + bit;
            var byteIndex = (int)(absolute / 8);
            if (byteIndex >= _samples.Length)
            {
                return 0;
            }

            var bitIndex = 7 - (int)(absolute % 8);
            value = (value << 1) | (uint)((_samples[byteIndex] >> bitIndex) & 1);
        }

        return value;
    }
}

/// <summary>Type 4: the PostScript calculator subset the specification defines.</summary>
internal sealed class PdfPostScriptFunction : PdfFunction
{
    private static readonly HashSet<string> SupportedOperators = new(StringComparer.Ordinal)
    {
        "add", "sub", "mul", "div", "idiv", "mod", "neg", "abs", "sqrt", "sin", "cos",
        "atan", "exp", "ln", "log", "cvi", "cvr", "floor", "ceiling", "round", "truncate",
        "dup", "pop", "exch", "copy", "index", "roll", "eq", "ne", "gt", "ge", "lt", "le",
        "and", "or", "xor", "not", "bitshift", "true", "false", "if", "ifelse"
    };
    private readonly IReadOnlyList<object> _program;
    private readonly int _outputCount;

    private PdfPostScriptFunction(
        IReadOnlyList<double> domain,
        IReadOnlyList<double>? range,
        IReadOnlyList<object> program,
        int outputCount) : base(domain, range)
    {
        _program = program;
        _outputCount = outputCount;
    }

    public override int OutputCount => _outputCount;

    public static PdfFunction? TryCreate(
        PdfStreamObject stream,
        IReadOnlyList<double> domain,
        IReadOnlyList<double>? range)
    {
        if (range is null || range.Count == 0)
        {
            return null;
        }

        byte[] source;
        try
        {
            source = new PdfStreamDecoderRegistry().Decode(stream).ToArray();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return null;
        }

        var text = System.Text.Encoding.ASCII.GetString(source);
        var position = 0;
        var tokens = Tokenize(text);
        if (tokens.Count == 0 || !Equals(tokens[0], "{") || !HasBalancedBlocks(tokens))
        {
            return null;
        }

        position = 1;
        var program = ParseBlock(tokens, ref position);
        return program is null || position != tokens.Count
            ? null
            : new PdfPostScriptFunction(domain, range, program, range.Count / 2);
    }

    private static bool HasBalancedBlocks(IReadOnlyList<string> tokens)
    {
        var depth = 0;
        foreach (var token in tokens)
        {
            if (token == "{")
            {
                depth++;
            }
            else if (token == "}" && --depth < 0)
            {
                return false;
            }
        }

        return depth == 0;
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var character in text)
        {
            if (character is '{' or '}')
            {
                Flush(tokens, current);
                tokens.Add(character.ToString());
            }
            else if (char.IsWhiteSpace(character))
            {
                Flush(tokens, current);
            }
            else
            {
                current.Append(character);
            }
        }

        Flush(tokens, current);
        return tokens;

        static void Flush(List<string> target, System.Text.StringBuilder builder)
        {
            if (builder.Length > 0)
            {
                target.Add(builder.ToString());
                builder.Clear();
            }
        }
    }

    private static List<object>? ParseBlock(List<string> tokens, ref int position)
    {
        var program = new List<object>();
        while (position < tokens.Count)
        {
            var token = tokens[position++];
            if (token == "}")
            {
                return program;
            }

            if (token == "{")
            {
                var nested = ParseBlock(tokens, ref position);
                if (nested is null)
                {
                    return null;
                }

                program.Add(nested);
                continue;
            }

            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                program.Add(number);
                continue;
            }

            if (!SupportedOperators.Contains(token))
            {
                return null;
            }

            program.Add(token);
        }

        return program;
    }

    protected override double[] EvaluateCore(double[] inputs)
    {
        var stack = new List<double>(inputs);
        Execute(_program, stack, 0);
        var outputs = new double[_outputCount];
        var start = Math.Max(0, stack.Count - _outputCount);
        for (var index = 0; index < _outputCount && start + index < stack.Count; index++)
        {
            outputs[index] = stack[start + index];
        }

        return outputs;
    }

    private static void Execute(IReadOnlyList<object> program, List<double> stack, int depth)
    {
        if (depth > 32)
        {
            return;
        }

        for (var index = 0; index < program.Count; index++)
        {
            switch (program[index])
            {
                case double number:
                    stack.Add(number);
                    break;
                case List<object>:
                    // Procedure blocks are consumed by the if/ifelse operators that follow.
                    break;
                case string op:
                    ExecuteOperator(op, program, stack, index, depth);
                    break;
            }
        }
    }

    private static void ExecuteOperator(
        string op,
        IReadOnlyList<object> program,
        List<double> stack,
        int index,
        int depth)
    {
        double Pop() => stack.Count > 0 ? Take(stack) : 0d;

        switch (op)
        {
            case "add": { var b = Pop(); var a = Pop(); stack.Add(a + b); break; }
            case "sub": { var b = Pop(); var a = Pop(); stack.Add(a - b); break; }
            case "mul": { var b = Pop(); var a = Pop(); stack.Add(a * b); break; }
            case "div": { var b = Pop(); var a = Pop(); stack.Add(b == 0 ? 0 : a / b); break; }
            case "idiv": { var b = Pop(); var a = Pop(); stack.Add(b == 0 ? 0 : Math.Truncate(a / b)); break; }
            case "mod": { var b = Pop(); var a = Pop(); stack.Add(b == 0 ? 0 : a % b); break; }
            case "neg": stack.Add(-Pop()); break;
            case "abs": stack.Add(Math.Abs(Pop())); break;
            case "sqrt": stack.Add(Math.Sqrt(Math.Max(0, Pop()))); break;
            case "sin": stack.Add(Math.Sin(Pop() * Math.PI / 180d)); break;
            case "cos": stack.Add(Math.Cos(Pop() * Math.PI / 180d)); break;
            case "atan": { var b = Pop(); var a = Pop(); var angle = Math.Atan2(a, b) * 180d / Math.PI; stack.Add(angle < 0 ? angle + 360d : angle); break; }
            case "exp": { var b = Pop(); var a = Pop(); stack.Add(Math.Pow(a, b)); break; }
            case "ln": { var a = Pop(); stack.Add(a > 0 ? Math.Log(a) : 0); break; }
            case "log": { var a = Pop(); stack.Add(a > 0 ? Math.Log10(a) : 0); break; }
            case "cvi": stack.Add(Math.Truncate(Pop())); break;
            case "cvr": break;
            case "floor": stack.Add(Math.Floor(Pop())); break;
            case "ceiling": stack.Add(Math.Ceiling(Pop())); break;
            case "round": stack.Add(Math.Round(Pop(), MidpointRounding.AwayFromZero)); break;
            case "truncate": stack.Add(Math.Truncate(Pop())); break;
            case "dup": { var a = Pop(); stack.Add(a); stack.Add(a); break; }
            case "pop": Pop(); break;
            case "exch": { var b = Pop(); var a = Pop(); stack.Add(b); stack.Add(a); break; }
            case "copy": CopyOperator(stack, (int)Pop()); break;
            case "index": { var n = (int)Pop(); stack.Add(n >= 0 && n < stack.Count ? stack[stack.Count - 1 - n] : 0d); break; }
            case "roll": RollOperator(stack, (int)Pop(), (int)Pop()); break;
            case "eq": { var b = Pop(); var a = Pop(); stack.Add(a == b ? 1 : 0); break; }
            case "ne": { var b = Pop(); var a = Pop(); stack.Add(a != b ? 1 : 0); break; }
            case "gt": { var b = Pop(); var a = Pop(); stack.Add(a > b ? 1 : 0); break; }
            case "ge": { var b = Pop(); var a = Pop(); stack.Add(a >= b ? 1 : 0); break; }
            case "lt": { var b = Pop(); var a = Pop(); stack.Add(a < b ? 1 : 0); break; }
            case "le": { var b = Pop(); var a = Pop(); stack.Add(a <= b ? 1 : 0); break; }
            case "and": { var b = Pop(); var a = Pop(); stack.Add((long)a & (long)b); break; }
            case "or": { var b = Pop(); var a = Pop(); stack.Add((long)a | (long)b); break; }
            case "xor": { var b = Pop(); var a = Pop(); stack.Add((long)a ^ (long)b); break; }
            case "not": { var a = Pop(); stack.Add(a == 0 ? 1 : a == 1 ? 0 : ~(long)a); break; }
            case "bitshift": { var shift = (int)Pop(); var a = (long)Pop(); stack.Add(shift >= 0 ? a << shift : a >> -shift); break; }
            case "true": stack.Add(1); break;
            case "false": stack.Add(0); break;
            case "if":
                if (index >= 1 && program[index - 1] is List<object> body && Pop() != 0)
                {
                    Execute(body, stack, depth + 1);
                }

                break;
            case "ifelse":
                if (index >= 2 &&
                    program[index - 2] is List<object> thenBody &&
                    program[index - 1] is List<object> elseBody)
                {
                    Execute(Pop() != 0 ? thenBody : elseBody, stack, depth + 1);
                }

                break;
        }
    }

    private static double Take(List<double> stack)
    {
        var value = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return value;
    }

    private static void CopyOperator(List<double> stack, int count)
    {
        if (count <= 0 || count > stack.Count)
        {
            return;
        }

        stack.AddRange(stack.GetRange(stack.Count - count, count));
    }

    private static void RollOperator(List<double> stack, int shift, int count)
    {
        if (count <= 0 || count > stack.Count)
        {
            return;
        }

        var items = stack.GetRange(stack.Count - count, count);
        stack.RemoveRange(stack.Count - count, count);
        var offset = ((shift % count) + count) % count;
        stack.AddRange(items[(count - offset)..]);
        stack.AddRange(items[..(count - offset)]);
    }
}
