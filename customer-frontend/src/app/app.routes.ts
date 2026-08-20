import { Routes } from '@angular/router';

import { CampaignDetail } from './campaign-detail/campaign-detail';
import { Login } from './login/login';
import { MyCampaigns } from './my-campaigns/my-campaigns';
import { Profile } from './profile/profile';
import { authGuard } from './auth-guard';

export const routes: Routes = [
  { path: 'login', component: Login, title: 'Giriş' },
  { path: 'kampanyalar', component: MyCampaigns, canActivate: [authGuard], title: 'Kampanyalarım' },
  { path: 'kampanyalar/:id', component: CampaignDetail, canActivate: [authGuard], title: 'Kampanya' },
  { path: 'profil', component: Profile, canActivate: [authGuard], title: 'Profil Ayarları' },
  { path: '', pathMatch: 'full', redirectTo: 'kampanyalar' },
  { path: '**', redirectTo: 'kampanyalar' }
];
