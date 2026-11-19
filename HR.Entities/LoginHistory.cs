using System;

namespace HR.Entities
{
    /// <summary>Maps to table [LoginHistory].</summary>
    public class LoginHistory
    {
        /// <summary>Primary key (AUTOINCREMENT: LoginId).</summary>
        public int LoginId { get; set; }
        /// <summary>UserId.</summary>
        public int? UserId { get; set; }

        /// <summary>AttemptTime.</summary>
        public DateTime? AttemptTime { get; set; }

        /// <summary>Success.</summary>
        public bool? Success { get; set; }

        /// <summary>MachineName.</summary>
        public string MachineName { get; set; }

        /// <summary>IpAddress.</summary>
        public string IpAddress { get; set; }
    }
}
