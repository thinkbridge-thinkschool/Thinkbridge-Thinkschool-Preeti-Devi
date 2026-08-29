# Quotes Routing App

The runnable Angular 22 workspace for Day 16 Piece 1 (`../Code`, `../README.md`). Talks to the real Week-1 backend at `day-5/Day-5-Piece-2` — see [Running it](#running-it) below.

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 22.1.5.

## Running it

1. Start the backend, in a separate terminal:
   ```bash
   cd ../../../day-5/Day-5-Piece-2
   ASPNETCORE_ENVIRONMENT=Development APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://local.invalid/" ConnectionStrings__Quotes="Data Source=quotes.db" dotnet run --urls http://localhost:5000
   ```
   (The `APPLICATIONINSIGHTS_CONNECTION_STRING` is only needed to satisfy the OpenTelemetry exporter for local runs — any syntactically valid value works, nothing is actually sent anywhere without a real Application Insights resource.)
2. Log in once with `testuser` / `password` (via the app's Login screen, or directly: `POST http://localhost:5000/api/auth/login`) and create a quote or two with the returned token so `/quotes` isn't empty — the database starts with no seed data.
3. In this folder:
   ```bash
   npm install
   npm start
   ```
4. Open `http://localhost:4200/`.

## Development server

To start a local development server, run:

```bash
npm start
```

(equivalent to `ng serve`). Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Vitest](https://vitest.dev/) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
