﻿using System;
using System.Collections.Generic;

namespace REvernus.Models.EveDbModels
{
    public partial class IndustryActivitySkills
    {
        public long? TypeId { get; set; }
        public long? ActivityId { get; set; }
        public long? SkillId { get; set; }
        public long? Level { get; set; }
    }
}