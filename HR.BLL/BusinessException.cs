using System;

namespace HR.BLL
{
    /// <summary>
    /// Validation/business-rule failure carrying an Arabic, user-facing message.
    /// The UI shows Exception.Message directly; developers receive details via Logger.
    /// </summary>
    public class BusinessException : Exception
    {
        /// <summary>Creates a failure with a ready-to-display Arabic message.</summary>
        public BusinessException(string message)
            : base(message)
        {
        }

        /// <summary>Creates a failure wrapping the original exception.</summary>
        public BusinessException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
