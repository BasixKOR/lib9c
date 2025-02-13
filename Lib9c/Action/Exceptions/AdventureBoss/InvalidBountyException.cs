using System;
using System.Runtime.Serialization;

namespace Nekoyume.Action.Exceptions.AdventureBoss
{
    [Serializable]
    public class InvalidBountyException : Exception
    {
        public InvalidBountyException()
        {
        }

        protected InvalidBountyException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public InvalidBountyException(string msg) : base(msg)
        {
        }
    }
}
