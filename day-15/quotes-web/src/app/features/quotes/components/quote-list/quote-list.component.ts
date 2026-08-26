import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { QuotesState } from '../../state/quotes.state';
import { AuthService } from '../../../../core/services/auth.service';

@Component({
  selector: 'app-quote-list',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './quote-list.component.html',
  styleUrls: ['./quote-list.component.css'],
})
export class QuoteListComponent implements OnInit {
  readonly state = inject(QuotesState);
  readonly authService = inject(AuthService);

  ngOnInit(): void {
    this.state.loadQuotes();
  }

  toggleAuth(): void {
    if (this.authService.isAuthenticated()) {
      this.authService.clearToken();
    } else {
      this.authService.setToken('mock-jwt-token-quotes-user');
    }
  }
}
