Day 12 — Piece 1: Read Models + CQRS-lite



Overview



This piece separates the quote feature into independent command and query paths using MediatR.



Write side



text

CreateQuoteCommand

&#x20;       ↓

CreateQuoteCommandHandler

&#x20;       ↓

Quote entity

&#x20;       ↓

EF Core / Database

