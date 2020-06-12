using System;

namespace ReactiveWebSocket
{

    [Serializable]
    public class InvalidTransitionException : RxWebSocketException
    {
        public InvalidTransitionException(Type stateType, Type inputType)
            : base($"There is no transition for {inputType.Name} from state {stateType.Name}.")
        {
            this.StateType = stateType;
            this.InputType = inputType;
        }

        public InvalidTransitionException(Type stateType, Type inputType, string message)
            : base($"There is no transition for {inputType.Name} from state {stateType.Name}." + $" {message}".Trim())
        {
            this.StateType = stateType;
            this.InputType = inputType;
        }

        protected InvalidTransitionException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {
            this.StateType = (Type)info.GetValue(nameof(StateType), typeof(Type));
            this.InputType = (Type)info.GetValue(nameof(InputType), typeof(Type));
        }

        public Type StateType { get; }
        public Type InputType { get; }
    }
}
