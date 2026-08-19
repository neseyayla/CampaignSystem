import { Routes } from '@angular/router';

import { CampaignForm } from './campaigns/campaign-form';
import { CampaignList } from './campaigns/campaign-list';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'campaigns' },
  { path: 'campaigns', component: CampaignList, title: 'Kampanyalar' },
  { path: 'campaigns/new', component: CampaignForm, title: 'Batch Kampanya Tanım' },

  // Anything unrecognised goes to the campaign list rather than a blank page.
  { path: '**', redirectTo: 'campaigns' }
];
