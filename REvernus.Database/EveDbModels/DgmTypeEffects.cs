﻿namespace REvernus.Database.EveDbModels
{
    public partial class DgmTypeEffects
    {
        public long TypeId { get; set; }
        public long EffectId { get; set; }
        public byte[] IsDefault { get; set; }
    }
}