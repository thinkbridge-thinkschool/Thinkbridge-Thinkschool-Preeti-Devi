import { Routes } from '@angular/router';
import { LoginComponent } from './login/login';
import { QuoteFormComponent } from './quote-form/quote-form';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  { path: 'quotes', component: QuoteFormComponent },
];
