import { Routes } from '@angular/router';

import { authGuard } from './auth-guard';
import { guestGuard } from './guest-guard';
import { CampaignForm } from './campaigns/campaign-form';
import { CampaignList } from './campaigns/campaign-list';
import { CampaignSuggestions } from './campaigns/campaign-suggestions';
import { Login } from './login/login';

export const routes: Routes = [
  { path: 'login', component: Login, title: 'Giriş', canActivate: [guestGuard] },

  { path: '', pathMatch: 'full', redirectTo: 'campaigns' },
  { path: 'campaigns', component: CampaignList, title: 'Kampanyalar', canActivate: [authGuard] },
  {
    path: 'campaigns/new',
    component: CampaignForm,
    title: 'Batch Kampanya Tanım',
    canActivate: [authGuard]
  },

  // Before campaigns/:id so "suggestions" is not read as an id.
  {
    path: 'campaigns/suggestions',
    component: CampaignSuggestions,
    title: 'Kampanya Önerileri',
    canActivate: [authGuard]
  },

  // Same component: an id in the route is what puts it into edit mode.
  {
    path: 'campaigns/:id',
    component: CampaignForm,
    title: 'Batch Kampanya Tanım',
    canActivate: [authGuard]
  },

  // Anything unrecognised goes to the campaign list rather than a blank page.
  { path: '**', redirectTo: 'campaigns' }
];
