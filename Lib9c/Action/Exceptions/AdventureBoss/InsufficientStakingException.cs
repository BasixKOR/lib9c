using System;
using System.Runtime.Serialization;

namespace Nekoyume.Action.Exceptions.AdventureBoss
{
    [Serializable]
    public class InsufficientStakingException : Exception
    {
        public InsufficientStakingException()
        {
        }

        protected InsufficientStakingException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public InsufficientStakingException(string msg) : base(msg)
        {
        }
    }
}
