namespace RoguelikeToolkit.Entities.Scripting.Jint
{
    /// <summary>
    /// A named value exposed to a script as a global for the duration of one execution.
    /// </summary>
    /// <remarks>
    /// Replaces the <c>(string, dynamic)</c> tuples of <c>RoguelikeToolkit.Scripts</c>: values are marshalled through
    /// <see cref="global::Jint.Native.JsValue.FromObject"/>, not the <c>Microsoft.CSharp</c> runtime binder, so no runtime code
    /// generation is involved (IL2CPP / NativeAOT safe). A <see cref="global::Jint.Native.JsValue"/> value is passed through as-is.
    /// </remarks>
    /// <param name="Name">global name the script sees</param>
    /// <param name="Value">value to expose (CLR objects are wrapped, primitives converted)</param>
    public readonly record struct ScriptArgument(string Name, object? Value)
    {
        /// <summary>
        /// Converts a <c>(name, value)</c> tuple, so call sites can keep the tuple syntax.
        /// </summary>
        /// <param name="argument">name/value pair</param>
        public static implicit operator ScriptArgument((string Name, object? Value) argument) =>
            new(argument.Name, argument.Value);
    }
}
