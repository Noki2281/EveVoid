﻿using System;

namespace EveVoid.Dto
{
    public class SolarSystemNoteDto
    {
        public int Id { get; set; }
        public int SolarSystemId { get; set; }
        public string MainCharacterName { get; set; }
        public string Content { get; set; }
        public DateTime LastUpdate { get; set; }
        public DateTime CreationDate { get; set; }
    }
}
