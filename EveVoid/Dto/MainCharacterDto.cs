﻿using EveVoid.Models.Navigation.Masks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EveVoid.Dto
{
    public class MainCharacterDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? CorporationId { get; set; }
        public string CorporationName { get; set; }
        public int? AllianceId { get; set; }
        public string AllianceName { get; set; }
        public List<EsiCharacterDto> EsiCharacterDtos { get; set; }
        public MaskType MaskType { get; set; }
    }
}
