using System;
using System.Globalization;
using System.Runtime.Serialization;

namespace Nekoyume.TableData
{
    [Serializable]
    public class SheetRowColumnException : Exception
    {
        public SheetRowColumnException(string message) : base(message)
        {
        }

        protected SheetRowColumnException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    [Serializable]
    public class SheetRowValidateException : Exception
    {
        public SheetRowValidateException(string message) : base(message)
        {
        }

        protected SheetRowValidateException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    [Serializable]
    public class SheetRowNotFoundException : Exception
    {
        public SheetRowNotFoundException(string sheetName, int intKey)
            : this(sheetName, intKey.ToString(CultureInfo.InvariantCulture))
        {
        }

        public SheetRowNotFoundException(string sheetName, long longKey)
            : this(sheetName, longKey.ToString(CultureInfo.InvariantCulture))
        {
        }

        public SheetRowNotFoundException(string sheetName, string key) : this(sheetName, "Key", key)
        {
        }

        public SheetRowNotFoundException(string sheetName, string condition, string value) :
            base($"{sheetName}: {condition} - {value}")
        {
        }

        public SheetRowNotFoundException(string addressesHex, string sheetName, int intKey)
            : base($"{addressesHex}{sheetName}: Key - {intKey.ToString(CultureInfo.InvariantCulture)}")
        {
        }

        protected SheetRowNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    [Serializable]
    public class RoundDoesNotExistException : Exception
    {
        public RoundDoesNotExistException(string message) : base(message)
        {
        }

        protected RoundDoesNotExistException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    [Serializable]
    public class AlreadyEnteredArenaException : Exception
    {
        public AlreadyEnteredArenaException(string message) : base(message)
        {
        }

        protected AlreadyEnteredArenaException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
