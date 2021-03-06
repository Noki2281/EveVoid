﻿using EveVoid.Data;
using EveVoid.Extensions;
using EveVoid.Models.Navigation.MapObjects;
using IO.Swagger.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EveVoid.Services.Navigation.MapObjects
{
    public class ConstellationService : IConstellationService
    {
        public EveVoidContext _context { get; set; }
        public IUniverseApi _universeApi { get; set; }
        public IRegionService _regionService { get; set; }

        public ConstellationService(EveVoidContext context, IUniverseApi universeApi, IRegionService regionService)
        {
            _context = context;
            _universeApi = universeApi;
            _regionService = regionService;
        }

        public Constellation GetConstellationById(int id)
        {
            var constellation = _context.Constellations.FirstOrDefault(x => x.Id == id);
            if (constellation == null || constellation.PassedMoreThan() || constellation.Name == "Temp")
            {
                var esiResult = _universeApi.GetUniverseConstellationsConstellationId(id, "en-us", null, null, "en-us");
                if (esiResult.RegionId.HasValue)
                {
                    _regionService.GetRegionById(esiResult.RegionId.Value);
                }
                if (constellation == null)
                {
                    constellation = new Constellation
                    {
                        Id = id,
                        Name = esiResult.Name,
                        RegionId = esiResult.RegionId.Value
                    };
                    _context.Constellations.Add(constellation);
                }
                else
                {
                    constellation.Name = esiResult.Name;
                    constellation.RegionId = esiResult.RegionId.Value;
                }
                _context.SaveChanges();
            }
            return constellation;
        }
    }
}
