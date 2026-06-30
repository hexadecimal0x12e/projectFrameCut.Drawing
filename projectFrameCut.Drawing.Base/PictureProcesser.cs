using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using System.Diagnostics.CodeAnalysis;

namespace projectFrameCut.Drawing.Base
{
    public record ProcessableIPictureContext<T> where T : IPicture
    {
        /// <summary>
        /// Auto dispose the old processed picture when setting a new one. 
        /// This is true by default to avoid memory leaks, but you can set it to false if you want to manage the disposal manually or if the processing function returns the same picture instance without modification.
        /// </summary>
        public static bool AutoDisposeOldProcessedPicture = true;

        /// <summary>
        /// The processed result image.
        /// </summary>
        /// <remarks>
        /// <b>If you MANIPULATE the Result property in any way, you MUST append your step into <see cref="IPicture.ProcessStack"/>.</b>
        /// </remarks>
        public required T Result { get; set; }

        /// <summary>
        /// Set the processed result image and return this context for chaining. 
        /// If <paramref name="disposeOld"/> is true or <see cref="AutoDisposeOldProcessedPicture"/> is true, the old Result will be disposed before being replaced.
        /// </summary>
        public ProcessableIPictureContext<T> SetAndReturn(T? value, bool? disposeOld = null)
        {
            if (disposeOld ?? AutoDisposeOldProcessedPicture)
            {
                Result?.Dispose(appendDisposedProcessStack: false);
            }
            Result = value ?? throw new NullReferenceException("Trying to set Picture to null");
            return this;
        }

        /// <summary>Implicitly extract the result picture from a processing context.</summary>
        public static implicit operator T(ProcessableIPictureContext<T> context) => context.Result;

    }

    public static class PictureProcesserExtensions
    {
        extension<T>(T source) where T : IPicture
        {
            /// <summary>
            /// Create a processing context for this picture. 
            /// You can use the returned context to manipulate the picture and get the final result after processing.
            /// </summary>
            /// <returns>A context for processing.</returns>
            public ProcessableIPictureContext<T> EnterProcessContext() => new() { Result = source };

            /// <summary>
            /// Mutate the input picture by the specified processing function. 
            /// The processing function takes a <see cref="ProcessableIPictureContext{T}"/> as input and returns a <see cref="ProcessableIPictureContext{T}"/> as output. 
            /// The input context contains the original picture in the Result property, and the output context should contain the processed picture in the Result property. This method will return the processed picture from the output context.
            /// </summary>
            /// <returns>The processed image.</returns>
            public IPicture Mutate(Func<ProcessableIPictureContext<T>, ProcessableIPictureContext<T>> processFunc)
            {
                var context = new ProcessableIPictureContext<T> { Result = source };
                return processFunc(context).Result;
            }
        }

        extension<T>(ProcessableIPictureContext<T> ctx) where T : IPicture
        {
            /// <summary>
            /// If the condition is true, applies the specified processing function to the current context.
            /// </summary>
            /// <param name="condition">Whether to execute <paramref name="processFunc"/>.</param>
            /// <param name="processFunc">The processing function to apply when the condition is true.</param>
            /// <returns>The updated processing context.</returns>
            public ProcessableIPictureContext<T> If(bool condition, Func<ProcessableIPictureContext<T>, ProcessableIPictureContext<T>> processFunc)
            {
                if (condition)
                {
                    return processFunc(ctx);
                }
                else
                {
                    return ctx;
                }
            }

            /// <summary>
            /// If the condition is true, applies <paramref name="trueFunc"/>; otherwise applies <paramref name="falseFunc"/>.
            /// </summary>
            /// <param name="condition">Whether to execute the true branch.</param>
            /// <param name="trueFunc">The processing function to apply when the condition is true.</param>
            /// <param name="falseFunc">The processing function to apply when the condition is false.</param>
            /// <returns>The updated processing context.</returns>
            public ProcessableIPictureContext<T> IfElse(bool condition, Func<ProcessableIPictureContext<T>, ProcessableIPictureContext<T>> trueFunc, Func<ProcessableIPictureContext<T>, ProcessableIPictureContext<T>> falseFunc)
            {
                if (condition)
                {
                    return trueFunc(ctx);
                }
                else
                {
                    return falseFunc(ctx);
                }
            }

            /// <summary>
            /// If <paramref name="condition"/> returns true, applies the specified processing function.
            /// </summary>
            /// <param name="condition">A function that determines whether to execute <paramref name="processFunc"/>.</param>
            /// <param name="processFunc">The processing function to apply when the condition returns true.</param>
            /// <returns>The updated processing context.</returns>
            public ProcessableIPictureContext<T> If(Func<bool> condition, Func<ProcessableIPictureContext<T>, ProcessableIPictureContext<T>> processFunc)
            {
                if (condition())
                {
                    return processFunc(ctx);
                }
                else
                {
                    return ctx;
                }
            }

            /// <summary>
            /// If <paramref name="condition"/> returns true, applies <paramref name="trueFunc"/>; otherwise applies <paramref name="falseFunc"/>.
            /// </summary>
            /// <param name="condition">A function that determines whether to execute the true branch.</param>
            /// <param name="trueFunc">The processing function to apply when the condition returns true.</param>
            /// <param name="falseFunc">The processing function to apply when the condition returns false.</param>
            /// <returns>The updated processing context.</returns>
            public ProcessableIPictureContext<T> IfElse(Func<bool> condition, Func<ProcessableIPictureContext<T>, ProcessableIPictureContext<T>> trueFunc, Func<ProcessableIPictureContext<T>, ProcessableIPictureContext<T>> falseFunc)
            {
                if (condition())
                {
                    return trueFunc(ctx);
                }
                else
                {
                    return falseFunc(ctx);
                }
            }

            /// <summary>
            /// Applies each processing function in order to the current context.
            /// </summary>
            /// <param name="processFuncs">The processing functions to execute sequentially.</param>
            /// <returns>The final processing context.</returns>
            public ProcessableIPictureContext<T> Foreach(IEnumerable<Func<ProcessableIPictureContext<T>, ProcessableIPictureContext<T>>> processFuncs)
            {
                var current = ctx;
                foreach (var func in processFuncs)
                {
                    current = func(current);
                }
                return current;

            }

            /// <summary>
            /// Repeatedly applies <paramref name="process"/> while <paramref name="when"/> returns true.
            /// </summary>
            /// <typeparam name="TAccumulate">The accumulator type.</typeparam>
            /// <param name="seed">The initial accumulator value.</param>
            /// <param name="when">The loop condition.</param>
            /// <param name="after">The function used to advance the accumulator.</param>
            /// <param name="process">The processing function executed on each iteration.</param>
            /// <returns>The final processing context.</returns>
            public ProcessableIPictureContext<T> For<TAccumulate>(TAccumulate seed, Func<TAccumulate, bool> when, Func<TAccumulate, TAccumulate> after, Func<ProcessableIPictureContext<T>, TAccumulate, ProcessableIPictureContext<T>> process)
            {
                for (TAccumulate acc = seed; when(acc); acc = after(acc))
                {
                    ctx = process(ctx, acc);
                }
                return ctx;
            }

            /// <summary>
            /// Repeatedly applies <paramref name="process"/> while <paramref name="condition"/> returns true.
            /// </summary>
            /// <param name="condition">The loop condition.</param>
            /// <param name="process">The processing function executed on each iteration.</param>
            /// <returns>The final processing context.</returns>
            public ProcessableIPictureContext<T> While(Func<bool> condition, Func<ProcessableIPictureContext<T>, ProcessableIPictureContext<T>> process)
            {
                while (condition())
                {
                    ctx = process(ctx);
                }
                return ctx;
            }

            /// <summary>
            /// Executes <paramref name="process"/> at least once, then repeats while <paramref name="condition"/> returns true.
            /// </summary>
            /// <param name="condition">The loop condition.</param>
            /// <param name="process">The processing function executed on each iteration.</param>
            /// <returns>The final processing context.</returns>
            public ProcessableIPictureContext<T> DoWhile(Func<bool> condition, Func<ProcessableIPictureContext<T>, ProcessableIPictureContext<T>> process)
            {
                do
                {
                    ctx = process(ctx);
                }
                while (condition());
                return ctx;
            }

            /// <summary>
            /// Executes the first case whose flag is true.
            /// </summary>
            /// <param name="cases">The ordered set of flag/process pairs.</param>
            /// <returns>The updated processing context.</returns>
            public ProcessableIPictureContext<T> Switch(params (bool flag, Func<ProcessableIPictureContext<T>, ProcessableIPictureContext<T>> process)[] cases)
            {
                foreach (var (flag, process) in cases)
                {
                    if (flag)
                    {
                        ctx = process(ctx);
                        break;
                    }
                }
                return ctx;
            }

            /// <summary>
            /// Executes the first case whose flag function returns true.
            /// </summary>
            /// <param name="cases">The ordered set of flag/process pairs.</param>
            /// <returns>The updated processing context.</returns>
            public ProcessableIPictureContext<T> Switch(params (Func<bool> flag, Func<ProcessableIPictureContext<T>, ProcessableIPictureContext<T>> process)[] cases)
            {
                foreach (var (flag, process) in cases)
                {
                    if (flag())
                    {
                        ctx = process(ctx);
                        break;
                    }
                }
                return ctx;
            }
        }
    }

    /// <summary>
    /// Represents a single step in the processing of a picture, including the operation performed, the operator used, the stack trace of the processing function, any additional properties, and the elapsed time for the operation.
    /// </summary>
    public class PictureProcessStack
    {
        /// <summary>
        /// A human-readable name for the operation performed in this processing step. This should describe the action taken on the picture, such as "Resize", "Apply Filter", or "Convert Color Space".
        /// </summary>
        public required string OperationDisplayName { get; set; }
        /// <summary>
        /// The type of the operator (class or struct) that performed this processing step. This can be used to identify the specific implementation or algorithm used for the operation.
        /// </summary>
        [JsonConverter(typeof(TypeJsonConverter))]
        public required Type? Operator { get; set; }
        /// <summary>
        /// The stack trace of the function that performed this processing step. This can be useful for debugging and tracing the flow of operations, especially when multiple processing steps are involved. The stack trace is serialized to a string for logging purposes.
        /// </summary>
        [JsonConverter(typeof(StackTraceJsonConverter))]
        public required StackTrace? ProcessingFuncStackTrace { get; set; }
        /// <summary>
        /// A dictionary of additional properties or metadata associated with this processing step. This can include parameters used for the operation, intermediate results, or any other relevant information. The properties are serialized to JSON for logging and debugging purposes.
        /// </summary>
        public Dictionary<string, object>? Properties { get; set; }
        /// <summary>
        /// The elapsed time taken to perform this processing step. This can be used for performance monitoring and optimization, allowing developers to identify slow operations and improve the efficiency of the picture processing pipeline.
        /// </summary>
        public TimeSpan? Elapsed { get; set; }
        /// <summary>
        /// A string tag that can be used to categorize or label this processing step. This can be useful for filtering and searching through logs, especially when dealing with complex processing pipelines that involve multiple steps and operations.
        /// </summary>
        public string? Tag { get; set; }

        private static readonly JsonSerializerOptions options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

        /// <summary>
        /// Formats a process stack for logging, including operation names, operators, elapsed times, properties, and stack traces. The output is a human-readable string suitable for log files or console output.
        /// </summary>
        public static string FormatProcessStackForLog(IEnumerable<PictureProcessStack>? processStack, int maxFramesPerStep = 12)
        {
            if (processStack == null) return "(null)";

            // Materialize once to avoid multiple enumeration and to preserve ordering.
            var steps = processStack as IList<PictureProcessStack> ?? processStack.ToList();
            if (steps.Count == 0) return "(empty)";

            var sb = new StringBuilder(capacity: 512);
            sb.AppendLine($"Steps: {steps.Count}");

            for (int i = 0; i < steps.Count; i++)
            {
                AppendProcessStep(sb, steps[i], i + 1, maxFramesPerStep, indent: "");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Formats a process stack for logging in Markdown format, including operation names, operators, elapsed times, properties, and stack traces. The output is a Markdown string suitable for documentation or rich-text logs.
        /// </summary>
        public static string FormatProcessStackForLogMarkdown(IEnumerable<PictureProcessStack>? processStack, int maxFramesPerStep = 12)
        {
            if (processStack == null) return "(null)";

            var steps = processStack as IList<PictureProcessStack> ?? processStack.ToList();
            if (steps.Count == 0) return "(empty)";

            var sb = new StringBuilder(capacity: 1024);
            sb.AppendLine($"# Process Steps ({steps.Count})");
            sb.AppendLine();

            for (int i = 0; i < steps.Count; i++)
            {
                AppendProcessStepMarkdown(sb, steps[i], i + 1, maxFramesPerStep, 0);
            }

            return sb.ToString();
        }

        private static void AppendProcessStepMarkdown(StringBuilder sb, PictureProcessStack step, int index, int maxFramesPerStep, int indentLevel)
        {
            if (step == null)
            {
                sb.AppendLine($"## #{index} <null>");
                return;
            }

            int baseLevel = Math.Min(6, 2 + indentLevel);
            sb.AppendLine(new string('#', baseLevel) + " " + index + ". " + (step.OperationDisplayName ?? "(no name)"));
            sb.AppendLine();

            if (step.Operator != null)
            {
                sb.AppendLine("- **Operator:** " + step.Operator.FullName);
            }
            if (step.Elapsed != null)
            {
                sb.AppendLine("- **Elapsed:** " + step.Elapsed);
            }

            if (step.Properties != null && step.Properties.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("**Properties:**");
                foreach (var kv in step.Properties.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    sb.AppendLine($"- **{kv.Key}:** {FormatPropertyValueForLog(kv.Value)}");
                }
            }

            if (step is OverlayedPictureProcessStack overlay)
            {
                if (overlay.TopSteps != null && overlay.TopSteps.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine(new string('#', Math.Min(6, baseLevel + 1)) + " TopSteps:");
                    for (int i = 0; i < overlay.TopSteps.Count; i++)
                    {
                        AppendProcessStepMarkdown(sb, overlay.TopSteps[i], i + 1, maxFramesPerStep, indentLevel + 2);
                    }
                }
                if (overlay.BaseSteps != null && overlay.BaseSteps.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine(new string('#', Math.Min(6, baseLevel + 1)) + " BaseSteps:");
                    for (int i = 0; i < overlay.BaseSteps.Count; i++)
                    {
                        AppendProcessStepMarkdown(sb, overlay.BaseSteps[i], i + 1, maxFramesPerStep, indentLevel + 2);
                    }
                }
            }

            AppendStackTraceForLogMarkdown(sb, step.ProcessingFuncStackTrace, maxFramesPerStep);

            sb.AppendLine();
        }

        private static void AppendStackTraceForLogMarkdown(StringBuilder sb, StackTrace? trace, int maxFrames)
        {
            if (trace == null) return;
            var frames = trace.GetFrames();
            if (frames == null || frames.Length == 0) return;

            sb.AppendLine();
            sb.AppendLine("**CallStack:**");
            sb.AppendLine();
            sb.AppendLine("```text");

            int take = Math.Min(frames.Length, Math.Max(0, maxFrames));
            for (int i = 0; i < take; i++)
            {
                var frame = frames[i];
                var method = DiagnosticMethodInfo.Create(frame);
                string methodName = method == null
                    ? "(unknown)"
                    : $"{method.DeclaringTypeName}.{method.Name}";

                string? file = frame.GetFileName();
                int line = frame.GetFileLineNumber();
                if (!string.IsNullOrWhiteSpace(file) && line > 0)
                {
                    sb.AppendLine($"{i + 1}. {methodName} ({System.IO.Path.GetFileName(file)}:{line})");
                }
                else
                {
                    sb.AppendLine($"{i + 1}. {methodName}");
                }
            }

            if (frames.Length > take)
            {
                sb.AppendLine($"... {frames.Length - take} more");
            }

            sb.AppendLine("```");
        }

        private static void AppendProcessStep(StringBuilder sb, PictureProcessStack step, int index, int maxFramesPerStep, string indent)
        {
            if (step == null)
            {
                sb.Append(indent).Append('#').Append(index).AppendLine(" <null>");
                return;
            }

            sb.Append(indent).Append('#').Append(index).Append(' ')
                .Append(step.OperationDisplayName ?? "(no name)");

            if (step.Operator != null)
            {
                sb.Append("  [Operator: ").Append(step.Operator.FullName).Append(']');
            }
            if (step.Elapsed != null)
            {
                sb.AppendLine();
                sb.Append(indent).Append("  Elapsed: ").Append(step.Elapsed);

            }
            sb.AppendLine();

            if (step.Properties != null && step.Properties.Count > 0)
            {
                sb.Append(indent).AppendLine("  Properties:");
                foreach (var kv in step.Properties.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    sb.Append(indent).Append("    - ").Append(kv.Key).Append(": ").AppendLine(FormatPropertyValueForLog(kv.Value));
                }
            }

            // Special-case overlay stacks to keep them readable.
            if (step is OverlayedPictureProcessStack overlay)
            {
                if (overlay.TopSteps != null && overlay.TopSteps.Count > 0)
                {
                    sb.Append(indent).AppendLine("  TopSteps:");
                    for (int i = 0; i < overlay.TopSteps.Count; i++)
                    {
                        AppendProcessStep(sb, overlay.TopSteps[i], i + 1, maxFramesPerStep, indent + "    ");
                    }
                }
                if (overlay.BaseSteps != null && overlay.BaseSteps.Count > 0)
                {
                    sb.Append(indent).AppendLine("  BaseSteps:");
                    for (int i = 0; i < overlay.BaseSteps.Count; i++)
                    {
                        AppendProcessStep(sb, overlay.BaseSteps[i], i + 1, maxFramesPerStep, indent + "    ");
                    }
                }
            }

            AppendStackTraceForLog(sb, step.ProcessingFuncStackTrace, maxFramesPerStep, indent);
        }

        private static void AppendStackTraceForLog(StringBuilder sb, StackTrace? trace, int maxFrames, string indent)
        {
            if (trace == null) return;
            var frames = trace.GetFrames();
            if (frames == null || frames.Length == 0) return;

            sb.Append(indent).AppendLine("  CallStack:");

            int take = Math.Min(frames.Length, Math.Max(0, maxFrames));
            for (int i = 0; i < take; i++)
            {
                var frame = frames[i];
                var method = DiagnosticMethodInfo.Create(frame);
                string methodName = method == null
                    ? "(unknown)"
                    : $"{method.DeclaringTypeName}.{method.Name}";

                string? file = frame.GetFileName();
                int line = frame.GetFileLineNumber();
                if (!string.IsNullOrWhiteSpace(file) && line > 0)
                {
                    sb.Append(indent).Append("    ").Append(i + 1).Append(". ").Append(methodName)
                        .Append(" (").Append(System.IO.Path.GetFileName(file)).Append(':').Append(line).Append(')')
                        .AppendLine();
                }
                else
                {
                    sb.Append(indent).Append("    ").Append(i + 1).Append(". ").Append(methodName).AppendLine();
                }
            }

            if (frames.Length > take)
            {
                sb.Append(indent).Append("    ... ").Append(frames.Length - take).AppendLine(" more");
            }
        }

        [SuppressMessage("Trimming", "IL3050", Justification = "Already checked whether reflection is available")]
        [SuppressMessage("Trimming", "IL2026", Justification = "Already checked whether reflection is available")]
        private static string FormatPropertyValueForLog(object? value)
        {
            if (value == null) return "(null)";
            if (value is string s) return s;
            if (value is Type t) return t.FullName ?? t.Name;
            if (value is StackTrace st) return st.ToString();
            if (value is Exception ex) return ex.ToString();

            // Avoid huge dumps for common collections; show count + a short preview.
            if (value is System.Collections.ICollection coll && value is not Array)
            {
                return $"{value.GetType().Name} (Count={coll.Count})";
            }

            try
            {
                // Best-effort JSON for anonymous/complex objects.
                if (value is not ValueType && JsonSerializer.IsReflectionEnabledByDefault)
                {
                    return JsonSerializer.Serialize(value, options);
                }
            }
            catch
            {
                // ignore and fall back to ToString
            }

            return value.ToString() ?? value.GetType().FullName ?? "(unknown)";
        }

        private class TypeJsonConverter : JsonConverter<Type?>
        {
            [SuppressMessage("Trimming", "IL2057", Justification = "Already checked whether reflection is available")]
            public override Type? Read(ref Utf8JsonReader reader, Type? typeToConvert, JsonSerializerOptions options)
            {
                string? typeName = reader.GetString();
                if (typeName == null || !JsonSerializer.IsReflectionEnabledByDefault) return null;
                return Type.GetType(typeName);
            }
            public override void Write(Utf8JsonWriter writer, Type? value, JsonSerializerOptions options)
            {
                if (value == null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    writer.WriteStringValue(value.AssemblyQualifiedName ?? value.FullName ?? value.Name);
                }
            }
        }

        private class StackTraceJsonConverter : JsonConverter<StackTrace?>
        {
            public override StackTrace? Read(ref Utf8JsonReader reader, Type? typeToConvert, JsonSerializerOptions options)
            {
                string? traceString = reader.GetString();
                if (traceString == null) return null;
                // We can't reliably parse a StackTrace from a string, so we'll just return null.
                return null;
            }
            public override void Write(Utf8JsonWriter writer, StackTrace? value, JsonSerializerOptions options)
            {
                if (value == null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    writer.WriteStringValue(value.ToString());
                }
            }
        }
    }

    /// <summary>Process stack that tracks separate top and base picture processing histories for composition operations.</summary>
    public class OverlayedPictureProcessStack : PictureProcessStack
    {
        /// <summary>Process steps applied to the top picture.</summary>
        public required List<PictureProcessStack> TopSteps { get; set; }
        /// <summary>Process steps applied to the base picture.</summary>
        public required List<PictureProcessStack> BaseSteps { get; set; }
    }
}
