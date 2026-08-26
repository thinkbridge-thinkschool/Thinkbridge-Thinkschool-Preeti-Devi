import { Component } from '@angular/core';
import { QuoteListComponent } from './features/quotes/components/quote-list/quote-list.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [QuoteListComponent],
  template: `<app-quote-list></app-quote-list>`,
  styles: [`
    :host {
      display: block;
      min-height: 100vh;
      background-color: #f8fafc;
    }
  `],
})
export class AppComponent {
  title = 'quotes-web';
}
