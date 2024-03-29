import { AutoJumpDialogComponent } from './../auto-jump-dialog/auto-jump-dialog.component';
import { SignatureDto } from './../api/models/signature-dto';
import { AuthControl } from './../control/auth-control';
import { Component, OnInit, Input, OnDestroy } from '@angular/core';
import { ImageControl } from '../control/image-control';
import { Observable, interval, Subscription, combineLatest, timer, Subject } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { EsiCharacterDto } from '../api/models';
import { CharacterService, SolarSystemService, SignatureService } from '../api/services';
import { LocationService } from '../eve-esi-api/services';
import { MatDialog } from '@angular/material/dialog';
import { AutoJumpDailogResponseData, DialogResult } from '../signature-dialog/confim-dialog.model';
import { PreferencesControl } from '../control/preferences-control';
import { DataControl } from '../control/data-control';
import { takeUntil } from 'rxjs/operators';


@Component({
  selector: 'app-esi-character',
  templateUrl: './esi-character.component.html',
  styleUrls: ['./esi-character.component.css']
})
export class EsiCharacterComponent implements OnInit, OnDestroy {
  @Input() esiChar: EsiCharacterDto;
  portraitSize = 64;
  locationObserver: Subscription;
  shipObserver: Subscription;
  reportingLocation = false;
  private destroyed$ = new Subject();

  get portrait(): string {
    return this.imageControl.getPortraitForCharacter(this.esiChar.pilotId, this.portraitSize);
  }
  get corpLogo(): string {
    return this.imageControl.getCorpLogo(this.esiChar.corporationId, this.portraitSize / 2);
  }
  get allianceLogo(): string {
    return this.imageControl.getAllianceLogo(this.esiChar.allianceId, this.portraitSize / 2);
  }
  constructor(private imageControl: ImageControl,
    public preferencesControl: PreferencesControl,
    private _locationApi: LocationService,
    private _characterService: CharacterService,
    private signatureService: SignatureService,
    private dataControl: DataControl,
    private dialog: MatDialog,
    private authControl: AuthControl) {

   }

  ngOnInit() {
    // this.shipObserver = interval(15000)
    timer(0, 25000).pipe(
      takeUntil(this.destroyed$)
    ).subscribe(() => {
      this._locationApi.getCharactersCharacterIdShip({characterId: this.esiChar.pilotId,
      token: this.esiChar.esiToken, datasource: null, IfNoneMatch: null}).subscribe(ship => {
        this.esiChar.currentShipTypeId = ship.ship_type_id;
        if (ship.ship_name.startsWith('u\'') && ship.ship_name.endsWith('\'')) {
          const r = /\\u([\d\w]{4})/gi;
          const uri = ship.ship_name.substring(2, ship.ship_name.lastIndexOf('\'')).replace(r, function (match, grp) {
              return String.fromCharCode(parseInt(grp, 16)); } );
          this.esiChar.currentShipName = uri;
        } else {
          this.esiChar.currentShipName = ship.ship_name;
        }
      }, err => {
        console.log(err);
        this._characterService.postApiCharacterGetEsiCharacter({mainToken: this.authControl.getMainToken(),
          esiCharId: this.esiChar.id}).subscribe(dto => {
          this.esiChar = dto;
        });
      });
    });

    // this.locationObserver = interval(5000)
    timer(0, 5000).pipe(
      takeUntil(this.destroyed$)
    ).subscribe(() => {
      this.reportingLocation = true;
      this._locationApi.getCharactersCharacterIdLocation({characterId: this.esiChar.pilotId,
        token: this.esiChar.esiToken, datasource: null, IfNoneMatch: null}).subscribe(loc => {
          if (this.esiChar.currentSystemId && loc.solar_system_id !== this.esiChar.currentSystemId) {
            this.signatureService
              .getApiSignatureGetPossibleJumpSignatures({
                mainToken: this.authControl.getMainToken(),
                originId: this.esiChar.currentSystemId,
                destoId: loc.solar_system_id
              }).subscribe(potentialSigs => {
                let jumpedSig = 0;
                this.esiChar.currentSystemId = loc.solar_system_id;
                if (potentialSigs.length < 2) {
                  jumpedSig = potentialSigs.length === 1 ? potentialSigs[0].id : 0;
                  this.updateWithSigId(jumpedSig);
                  return;
                } else {
                  const dialogRef = this.dialog.open(AutoJumpDialogComponent, {
                    data: {
                      title: '',
                      body: '',
                      data: potentialSigs,
                    },
                  });
                  dialogRef.afterClosed().subscribe((response: AutoJumpDailogResponseData) => {
                    if (response.result === DialogResult.confirmed) {
                      this.updateWithSigId(response.data.id);
                    }
                  });
                }
            });
          } else {
            this.esiChar.currentSystemId = loc.solar_system_id;
            this.updateWithSigId(-1);
          }
        }, err => {
          console.log(err);
          this._characterService.postApiCharacterGetEsiCharacter({mainToken: this.authControl.getMainToken(),
            esiCharId: this.esiChar.id}).subscribe(dto => {
            this.esiChar = dto;
          });
        });
    });
  }
  ngOnDestroy() {
    this.locationObserver.unsubscribe();
    this.shipObserver.unsubscribe();
    this.destroyed$.next();
    this.destroyed$.complete();
  }

  updateWithSigId(sigId: number) {
    this._characterService.postApiCharacterUpdateEsiCharacter({mainToken: this.authControl.getMainToken(),
      body: this.esiChar, sigId}).subscribe(res => {
      this.esiChar = res;
      this.dataControl.setCharLocation({ id: this.esiChar.id, locationId: this.esiChar.currentSystemId.toString()});
      this.reportingLocation = false;
    });
  }

  goToSystem(systemId: number) {
    if (systemId > 10000000) {
      this.preferencesControl.setSelectedSystem({solarSystemId: systemId});
      this.dataControl.forceMapUpdate();
      // this.router.navigate(['./map/' + systemId]);
    }
  }
}
