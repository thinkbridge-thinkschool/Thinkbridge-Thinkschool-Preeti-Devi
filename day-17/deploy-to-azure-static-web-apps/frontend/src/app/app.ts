import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  // <main> is the app's only landmark. Without it every routed page is anonymous
  // content with no way for assistive tech to skip to it — a real Lighthouse
  // accessibility finding against the live site, not a theoretical one.
  template: '<main><router-outlet></router-outlet></main>',
})
export class App {}
