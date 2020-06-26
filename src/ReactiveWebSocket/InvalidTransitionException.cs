using Microsoft;
using System;

namespace ReactiveWebSocket
{
    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Required parameters, default constructors not possible")]
    public sealed class InvalidTransitionException : RxWebSocketException
    {
        internal InvalidTransitionException(Type stateType, Type inputType)
            : base($"State {GetTypeName(stateType)} has no transition for input {GetTypeName(inputType)} .")
        {
            this.StateType = Requires.NotNull(stateType, nameof(stateType));
            this.InputType = Requires.NotNull(inputType, nameof(inputType));
        }

        private InvalidTransitionException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {
            this.StateType = (Type)info.GetValue(nameof(StateType), typeof(Type));
            this.InputType = (Type)info.GetValue(nameof(InputType), typeof(Type));
        }

        public Type StateType { get; }
        public Type InputType { get; }

        private static string GetTypeName(Type type)
            => type != null ? type.Name : "[null]";
    }
}
