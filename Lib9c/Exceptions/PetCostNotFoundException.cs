﻿using System;

namespace Nekoyume.Exceptions
{
    [Serializable]
    public class PetCostNotFoundException : Exception
    {
        public PetCostNotFoundException(string message) : base(message)
        {
        }

        public PetCostNotFoundException(string message, Exception innerException) :
            base(message, innerException)
        {
        }

        public PetCostNotFoundException(
            string actionType,
            string addressesHex,
            string message,
            Exception innerException = null) :
            base(
                $"[{actionType}][{addressesHex}] {message}",
                innerException)
        {
        }
    }
}
