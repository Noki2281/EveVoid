﻿using EveVoid.Models.EveObjects;
using EveVoid.Models.Pilots;
using EveVoid.Models.Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace EveVoid.Models.Navigation
{
    public class Jump: IHasCreationTime
    {
        public int Id { get; set; }
        public int ShipId { get; set; }
        public int WormholeId { get; set; }
        public int PilotId { get; set; }
        public DateTime CreationDate { get; set; }

        public virtual Signature Wormhole { get; set; }
        public virtual Pilot Pilot { get; set; }
        public virtual ItemType Ship { get; set; }
    }
}
