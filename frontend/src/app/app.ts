import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { API_BASE_URL } from './api-config';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  /** Shown in the status bar so it is obvious which API the screen is talking to. */
  protected readonly apiBaseUrl = API_BASE_URL;
}
