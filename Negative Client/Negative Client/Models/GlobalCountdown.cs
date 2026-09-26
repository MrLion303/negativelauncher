using System;
using System.Collections.Generic;

namespace Negative_Client.Models
{
    public sealed class GlobalCountdown
    {
        public string Id { get; set; } =
            string.Empty;

        public string Name { get; set; } =
            string.Empty;

        public DateTimeOffset EndAtUtc { get; set; }

        public bool Active { get; set; }
    }


    public sealed class GlobalCountdownFeed
    {
        public int Schema { get; set; } =
            1;

        public DateTimeOffset? UpdatedAt { get; set; }

        public List<GlobalCountdown> Countdowns { get; set; } =
            new();
    }
}
