using System;

namespace BlackBarLabs.Persistence
{
    public class NoRecordException : Exception
    {
        public NoRecordException() : base()
        {

        }

        public NoRecordException(string message) : base(message)
        {

        }
    }
}
