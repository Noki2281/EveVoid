﻿using EveVoid.Data;
using EveVoid.Dto;
using EveVoid.Extensions;
using EveVoid.Models.Navigation;
using EveVoid.Models.Navigation.MapObjects;
using EveVoid.Models.Navigation.Masks;
using EveVoid.Models.Pilots;
using EveVoid.Models.Responses;
using EveVoid.Services.EveObjects;
using EveVoid.Services.Navigation;
using EveVoid.Services.Navigation.MapObjects;
using IO.Swagger.Api;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EveVoid.Services.Pilots
{
    public class CharacterService : ICharacterService
    {
        public EveVoidContext _context { get; set; }
        public ICorporationService _corporationService { get; set; }
        public ILocationApi _locationApi { get; set; }
        public ICharacterApi _characterApi { get; set; }
        public ITokenService _tokenService { get; set; }
        public IItemTypeService _itemTypeService { get; set; }
        public ISolarSystemService _solarSystemService { get; set; }
        public ISignatureService _signatureService { get; set; }
        public IStargateService _stargateService { get; set; }
        public IRouteService _routeService { get; set; }
        public IPilotService _pilotService { get; set; }

        public CharacterService(EveVoidContext context,
            ICorporationService corporationService,
            ILocationApi locationApi,
            ICharacterApi characterApi,
            ITokenService tokenService,
            ISolarSystemService solarSystemService,
            IItemTypeService itemTypeService,
            ISignatureService signatureService,
            IStargateService stargateService,
            IRouteService routeService, 
            IPilotService pilotService)
        {
            _context = context;
            _corporationService = corporationService;
            _locationApi = locationApi;
            _characterApi = characterApi;
            _tokenService = tokenService;
            _solarSystemService = solarSystemService;
            _itemTypeService = itemTypeService;
            _signatureService = signatureService;
            _stargateService = stargateService;
            _routeService = routeService;
            _pilotService = pilotService;
        }

        public MainCharacter CreateOrUpdateMain(MainLoginDto dto)
        {
            var character = _context.MainCharacters.FirstOrDefault(x => x.Pilot.Id == dto.CharacterId);
            if (character == null)
            {
                var pilot = _pilotService.GetPilotById(dto.CharacterId);
                character = new MainCharacter
                {
                    AccessToken = dto.AccessToken,
                    RefreshToken = null,
                    MaskType = pilot.Corporation.AllianceId.HasValue ? MaskType.Alliance : MaskType.Corp,
                    PilotId = pilot.Id
                };
                _context.MainCharacters.Add(character);
            }
            else
            {
                character.AccessToken = dto.AccessToken;
            }
            _context.SaveChanges();
            return character;
        }

        public MainCharacter GetMainCharacterByToken(string token)
        {
            var main = _context.MainCharacters.FirstOrDefault(x => x.AccessToken == token);
            if (main != null)
            {
                foreach (var esi in main.EsiCharacters)
                {
                    if (esi.AccessToken == null || esi.TokenExpiresIn <= DateTime.UtcNow)
                    {
                        var newToken = _tokenService.GetTokenFromRefreshToken(esi.RefreshToken, version: "Location").Result;
                        esi.AccessToken = newToken.access_token;
                        esi.TokenExpiresIn = DateTime.UtcNow.AddSeconds(newToken.expires_in);
                    }
                }
                _context.SaveChanges();
            }
            return main;
        }

        public EsiCharacter AddOrUpdateEsiCharacterToMainToken(string token, OAuthToken authToken, OAuthVerify authVerify)
        {
            var main = GetMainCharacterByToken(token);
            if (main == null)
            {
                return null;
            }
            var esiChar = _context.EsiCharacters.FirstOrDefault(x => x.Pilot.Id == authVerify.CharacterID);
            if (esiChar == null || esiChar.PassedMoreThan() || esiChar.TokenExpiresIn <= DateTime.UtcNow)
            {
                var pilot = _pilotService.GetPilotById(authVerify.CharacterID);
                if (esiChar == null)
                {
                    esiChar = new EsiCharacter
                    {
                        AccessToken = authToken.access_token,
                        RefreshToken = authToken.refresh_token,
                        TokenExpiresIn = DateTime.UtcNow.AddSeconds(authToken.expires_in),
                        PilotId = pilot.Id
                    };
                    main.EsiCharacters.Add(esiChar);
                }
                else
                {
                    esiChar.RefreshToken = authToken.refresh_token;
                    esiChar.TokenExpiresIn = DateTime.UtcNow.AddSeconds(authToken.expires_in);
                    esiChar.PilotId = pilot.Id;
                }
                _context.SaveChanges();
            }
            return esiChar;
        }

        public EsiCharacter GetEsiCharacterWithActiveToken(string mainToken, int esiId)
        {
            var esi = _context.EsiCharacters.FirstOrDefault(x => x.Id == esiId && x.MainCharacter.AccessToken == mainToken);
            if (esi == null)
            {
                return null;
            }
            if (esi.AccessToken == null || esi.TokenExpiresIn <= DateTime.UtcNow)
            {
                var newToken = _tokenService.GetTokenFromRefreshToken(esi.RefreshToken, version: "Location").Result;
                esi.AccessToken = newToken.access_token;
                esi.TokenExpiresIn = DateTime.UtcNow.AddSeconds(newToken.expires_in);
            }
            _context.SaveChanges();
            return esi;
        }

        public EsiCharacter UpdateEsiCharacter(string mainToken, EsiCharacterDto dto, int sigId)
        {
            var esi = _context.EsiCharacters.FirstOrDefault(x => x.Id == dto.Id && x.MainCharacter.AccessToken == mainToken);
            if (esi == null)
            {
                return null;
            }
            if (esi.CurrentSystemId != null && esi.PassedLessThan(seconds: 11) && sigId >= 0) // sigId = -1 means angular client didn't notice a change in location
            {
                if (esi.CurrentSystemId != dto.CurrentSystemId)
                {
                    var maskId = esi.MainCharacter.MaskType == MaskType.Alliance ? esi.MainCharacter.Pilot.Corporation.Alliance.MaskId : esi.MainCharacter.Pilot.Corporation.MaskId;
                    var destoSystem = _solarSystemService.GetSystemById(dto.CurrentSystemId.GetValueOrDefault());
                    var originSystem = _solarSystemService.GetSystemById(esi.CurrentSystemId.Value);
                    var connection = _stargateService.GetStargateByOriginAndDestoId(esi.CurrentSystemId.GetValueOrDefault(), dto.CurrentSystemId.GetValueOrDefault());
                    if (connection == null)
                    {
                        var wormhole = _signatureService.GetBySignatureId(sigId);
                        if (wormhole == null) // sigId = 0 means it's an unmarked wormhole
                        {
                            wormhole = new Signature
                            {
                                SystemId = esi.CurrentSystemId.Value,
                                SignatureId = "???",
                                ExpiryDate = DateTime.UtcNow.AddDays(1),
                                Name = "",
                                SignatureType = SignatureType.Wormhole,
                                MaskId = maskId,
                                WormholeTypeId = _signatureService.GetByTypeName("????").Id
                            };
                            _signatureService.Insert(wormhole, commit: true);
                        }
                        wormhole.Jumps.Add(new Jump
                        {
                            PilotId = esi.Pilot.Id,
                            ShipId = dto.CurrentShipTypeId.GetValueOrDefault()
                        });
                        var destoWormhole = wormhole.Destination;
                        if (destoWormhole == null)
                        {
                            destoWormhole = new Signature
                            {
                                SystemId = dto.CurrentSystemId.Value,
                                SignatureId = "???",
                                ExpiryDate = wormhole.ExpiryDate,
                                Name = "",
                                SignatureType = SignatureType.Wormhole,
                                MaskId = maskId,
                                WormholeTypeId = _signatureService.GetByTypeName("K162").Id
                            };
                            _signatureService.Insert(destoWormhole, commit: true);
                            destoWormhole.DestinationId = wormhole.Id;
                            wormhole.DestinationId = destoWormhole.Id;
                            _signatureService.Update(wormhole, commit: true);
                        }
                        _routeService.AddAdjacency(esi.CurrentSystemId.Value, dto.CurrentSystemId.Value, maskId,
                            destoSystem.Class > 0 || originSystem.Class > 0 ? 10 : // J Space Connection = 10
                            destoSystem.Security <= 0.45 || originSystem.Security <= 0.45 ? 100 : // Null/Low sec connection = 100
                            1); // Hisec to Hisec = 1
                        //var wormhole = _signatureService.GetOrAddWormholeByOriginAndDestoId(esi.CurrentSystemId.GetValueOrDefault(), dto.CurrentSystemId.GetValueOrDefault(), maskId, sigId);
                        //wormhole.Wormhole.Jumps.Add(new Jump
                        //{
                        //    EsiCharacterId = esi.Id,
                        //    ShipId = dto.CurrentShipTypeId.GetValueOrDefault()
                        //});
                    }
                    else
                    {
                        connection.StargateJumps.Add(new StargateJump
                        {
                            PilotId = esi.Pilot.Id,
                            ShipId = dto.CurrentShipTypeId.GetValueOrDefault(),
                            StargateId = connection.Id,
                            MaskId = maskId
                        });
                    }
                }
            }
            if (dto.CurrentShipTypeId.HasValue)
            {
                _itemTypeService.GetItemTypeById(dto.CurrentShipTypeId.Value);
                esi.CurrentShipTypeId = dto.CurrentShipTypeId;
            }
            if (dto.CurrentSystemId.HasValue)
            {
                _solarSystemService.GetSystemById(dto.CurrentSystemId.Value);
                esi.CurrentSystemId = dto.CurrentSystemId;
                _context.Update(esi);
            }
            esi.CurrentShipName = dto.CurrentShipName;
            _context.SaveChanges();
            return esi;
        }

        public void UpdateMainCharacter(MainCharacter main)
        {
            _context.Update(main);
            _context.SaveChanges();
        }
    }
}
