﻿using EveVoid.Models.EveObjects;
using EveVoid.Models.Navigation;
using EveVoid.Models.Navigation.MapObjects;
using EveVoid.Models.Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using EveVoid.Models.Navigation.Masks;

namespace EveVoid.Models.Pilots
{
    public class EsiCharacter: IEsiEntity, IHasUpdateTime
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public string Name { get; set; }
        public int? CorporationId { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public int MainCharacterId { get; set; }
        public int? CurrentSystemId { get; set; }
        public int? CurrentShipTypeId { get; set; }
        public string CurrentShipName { get; set; }
        public DateTime LastUpdate { get; set; }
        public DateTime TokenExpiresIn { get; set; }

        public virtual List<Jump> Jumps { get; set; }
        public virtual MainCharacter MainCharacter { get; set; }
        public virtual Corporation Corporation { get; set; }
        public virtual SolarSystem CurrentSystem { get; set; }
        public virtual ItemType CurrentShip { get; set; }

        public EsiCharacter()
        {
            Jumps = new List<Jump>();
        }
    }
}