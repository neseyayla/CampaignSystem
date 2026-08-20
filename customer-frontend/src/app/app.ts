import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/**
 * The shell does nothing but host the route in force: the sign-in screen, or the campaigns
 * behind it. There is no menu — everything a card holder came for is on one page.
 */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: '<router-outlet />'
})
export class App {}
