import { Component } from '@angular/core';
import { QuoteListComponent } from './quote-list/quote-list';

@Component({
  selector: 'app-root',
  imports: [QuoteListComponent],
  template: `<app-quote-list />`,
  styles: [],
})
export class App {}
