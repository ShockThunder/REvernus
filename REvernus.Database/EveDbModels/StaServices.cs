﻿namespace REvernus.Database.EveDbModels
{
    using JetBrains.Annotations;

    [PublicAPI]
    public class StaServices
    {
        public long ServiceId { get; set; }
        public string ServiceName { get; set; }
        public string Description { get; set; }
    }
}
