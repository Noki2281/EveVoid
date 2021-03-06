﻿using EveVoid.Data;
using EveVoid.Extensions;
using EveVoid.Models.Navigation;
using EveVoid.Models.Navigation.MapObjects;
using IO.Swagger.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EveVoid.Services.Navigation.MapObjects
{
    public class SolarSystemService : ISolarSystemService
    {
        public EveVoidContext _context { get; set; }
        public IUniverseApi _universeApi { get; set; }
        public IConstellationService _constellationService { get; set; }
        public IStargateService _stargateService { get; set; }
        public SolarSystemService(EveVoidContext context,
            IUniverseApi universeApi, IConstellationService constellationService, IStargateService stargateService)
        {
            _context = context;
            _universeApi = universeApi;
            _constellationService = constellationService;
            _stargateService = stargateService;
        }

        public SolarSystem GetSystemById(int id)
        {
            var system = _context.SolarSystems.FirstOrDefault(x => x.Id == id);
            if (system == null || system.PassedMoreThan())
            {
                var esiResult = _universeApi.GetUniverseSystemsSystemId(id, "en-us", null, null, "en-us");
                var wClass = 0;
                if (esiResult.ConstellationId.HasValue)
                {
                    switch (_constellationService.GetConstellationById(esiResult.ConstellationId.Value).Name.Substring(0, 1))
                    {
                        case "A":
                            {
                                wClass = 1;
                                break;
                            }
                        case "B":
                            {
                                wClass = 2;
                                break;
                            }
                        case "C":
                            {
                                wClass = 3;
                                break;
                            }
                        case "D":
                            {
                                wClass = 4;
                                break;
                            }
                        case "E":
                            {
                                wClass = 5;
                                break;
                            }
                        case "F":
                            {
                                wClass = 6;
                                break;
                            }
                    }
                }
                if (system == null)
                {
                    system = new SolarSystem
                    {
                        Id = id,
                        Name = esiResult.Name,
                        Class = wClass,
                        ConstellationId = esiResult.ConstellationId.Value,
                        SystemTypeId = GetSystemTypeIdBySecStatusAndName(esiResult.SecurityStatus.Value, esiResult.Name),
                        Security = esiResult.SecurityStatus.GetValueOrDefault()
                    };
                    _context.SolarSystems.Add(system);
                    _context.SaveChanges();
                }
                else
                {
                    system.ConstellationId = esiResult.ConstellationId.Value;
                    system.Security = esiResult.SecurityStatus.GetValueOrDefault();
                }
                if (esiResult.Stargates != null)
                {
                    foreach (var gateId in esiResult.Stargates)
                    {
                        if (gateId.HasValue)
                        {
                            var gate = _stargateService.AddOrUpdateStargateToSystemId(gateId.Value, esiResult);

                        }
                    }
                }
            }
            _context.SaveChanges();
            return system;
        }

        private int GetSystemTypeIdBySecStatusAndName(double secStatus, string name)
        {
            if (secStatus >= 0.5)
            {
                return _context.SystemTypes.FirstOrDefault(x => x.Name == "High-Sec").Id;
            }
            if (secStatus > 0)
            {
                return _context.SystemTypes.FirstOrDefault(x => x.Name == "Low-Sec").Id;
            }
            if (name == "Thera")
            {
                return _context.SystemTypes.FirstOrDefault(x => x.Name == "Thera").Id;
            }
            var TrigSystemNames = new List<string>
            {
                "Urhinichi", "Nani", "Skarkon", "Raravoss", "Niarja", "Harva", "Tunudan", "Kuharah", "Ahtila", 
                "Senda", "Wirashoda", "Ala", "Vale", "Archee", "Angymonne", "Ichoriya", "Kaunokka", "Arvasaras",
                "Sakenta", "Komo", "Ignebaener", "Otela", "Kino", "Nalvula", "Konola", "Krirald", "Otanuomi"
            };
            if (TrigSystemNames.Contains(name))
            {
                return _context.SystemTypes.FirstOrDefault(x => x.Name == "Trig").Id;
            }
            if (Regex.Match(name, "(J[0-9]{6})").Success)
            {
                var jSystem = _context.SolarSystems.FirstOrDefault(x => x.Name == name);
                return jSystem == null ? _context.SystemTypes.FirstOrDefault(x => x.Name == "Unknown").Id : jSystem.SystemType.Id;
            }
            return _context.SystemTypes.FirstOrDefault(x => x.Name == "Null-Sec").Id;
        }

        //public IEnumerable<SolarSystem> GetConnectedSystems(int id)
        //{
        //    var system = GetSystemById(id);
        //    return system.Gates.Select(x => x.Destination.SolarSystem).Union(system.Signatures.Where(x => x.LeadsToId != null).Select(x => x.LeadsTo.System));
        //}

        public void UpdateSystem(SolarSystem solarSystem)
        {
            _context.Update(solarSystem);
            _context.SaveChanges();
        }

        public List<SolarSystem> Find(string name, int pageSize)
        {
            return _context.SolarSystems.Where(x => x.Name.Contains(name)).Take(pageSize).ToList();
        }

        public List<SolarSystem> GetAll()
        {
            return _context.SolarSystems.ToList();
        }
    }
}
