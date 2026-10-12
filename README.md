# RaceDay

RaceDay is a backend system for organizing running events, managing participant enrollments, and tracking race results. I planned this project as a second year software development student to practice database design and API planning before writing any server code.

## Designed concepts

Users in two roles
- Organisers: create and manage events, categories, enrollments and results
- Participants: browse events and enroll in categories

Events
- Examples: "Cape Town Spring Run" and "Durban Beachfront Race"
- Each event can have multiple categories

Categories
- Examples: 5km, 10km, 21km
- Categories sit inside events and define race types and distances

Enrollments
- Links participants to a category for a specific event
- Stores enrollment status and profile info needed to race

Results
- Captures finish times and finishing positions
- Used to build leaderboards and event summaries

## Key components

Database Schema (RaceDaySchema.sql)
- SQL Server database with 6 tables: Role, User, Event, Category, Enrolment, Result
- Includes sample data that uses real South African event names and example participant profiles
- Schema is designed to be clear and extendable for future features

API Endpoint Plan (EndpointPlan.md)
- RESTful API contract covering the planned endpoints
- Authentication: register and login with JWT
- User profiles: view and update profile data
- Event CRUD: organisers can create, update, delete and list events
- Category management: create and manage race categories within events
- Enrollment workflow: participants can sign up for categories and manage their enrollments
- Results capture: organisers or authorised systems can submit results and generate leaderboards
- Role based access control for endpoints so organisers and participants get the right permissions

## Purpose and current status

Part 1 (planning) produced the data model and the API contract in `docs/`. Part 2 implements that plan as an ASP.NET Core Web API.

## Part 2: The API

### Project structure

```
RaceDay.sln
src/RaceDay.Api/            ASP.NET Core Web API (.NET 10)
  Controllers/              Auth, Users, Events, Categories, Enrolments, Results
  Data/RaceDayDbContext.cs  EF Core code-first model + seed data
  Data/Migrations/          EF Core migrations (creates the database)
  DTOs/                     Request/response models with validation
  Models/                   Entities: Role, User, Event, Category, Enrolment, Result
  Services/TokenService.cs  Issues JWT session tokens
tests/RaceDay.Api.Tests/    xUnit tests (98) run against the real API pipeline
```

### Running it

1. Open `RaceDay.sln` in Visual Studio and press F5 (or run `dotnet run --project src/RaceDay.Api`).
2. On startup the API applies the EF Core migrations, creating the `RaceDayDb` database on `(localdb)\MSSQLLocalDB` with the sample data. Change `ConnectionStrings:RaceDayDb` in `appsettings.json` to use another SQL Server.
3. Swagger UI opens at `/swagger`. Every endpoint is listed with its description, request body, responses and the role it needs.

To recreate the database manually: `dotnet tool restore` then `dotnet ef database update --project src/RaceDay.Api`.

### Testing in Swagger

1. `POST /api/auth/login` with a seeded account (password for all of them is `Password123!`):
   - Organisers: `naledi.khumalo@raceday.co.za`, `johan.botha@raceday.co.za`
   - Participants: `thandiwe.nkosi@example.com`, `ryan.pillay@example.com`
2. Copy the `token`, click **Authorize**, paste it in.
3. Call the padlocked endpoints. Calling an endpoint for the other role returns 403; calling without a token returns 401.

### Authentication and roles

- Users register with their name, email, password and a role (`Organiser` or `Participant`). The role must match a row in the `Role` table, so no other roles can be created.
- Passwords are hashed with ASP.NET Core's `PasswordHasher` (PBKDF2 with HMAC-SHA512, 100,000 iterations, random salt). Plain-text passwords are never stored.
- Login returns a signed JWT holding the user's id and role. The token is the user's session: the client sends it with every request, which keeps them authenticated with their role until it expires (120 minutes).
- Every endpoint requires a logged-in user by default (fallback authorization policy). Public endpoints are explicitly marked `[AllowAnonymous]`.
- Role-only endpoints use `[Authorize(Roles = ...)]`. Organisers can also only change events, categories, enrolment lists and results for events they own.

### Changes from the Part 1 schema

- `Event` has two new columns required by the Part 2 brief: `Distance` (DECIMAL(5,2), km) and `EventType` (`Run`, `Walk` or `Cycle`, enforced by a check constraint).
- `Result.EnrolmentId` is unique, so an enrolment can only have one result.
- Deleting an event cascades to its categories, enrolments and results.
- Seed users have real password hashes instead of placeholders.

### Business rules beyond the plan

- A participant can only enrol once per event (409), not in a full category (409) and not in an event that has already happened (400).
- Category names are unique within an event (409). A finishing position can only be used once per category (409).
- An enrolment with a recorded result can't be cancelled (409).
- Registering or changing a profile to an email that is already used returns 409.

### Running the tests

```
dotnet test
```

The tests start the full API in memory (`WebApplicationFactory`) against an EF Core in-memory database. Real HTTP requests go through the same JWT authentication and role checks as the live API. They cover:

- **AuthTests**: registration (valid, duplicate email, invalid role, weak password, hashed storage), login (valid, wrong password, unknown email), and requests with no token, an invalid token or a valid token, plus profile updates.
- **EventManagementTests**: organisers creating, updating and deleting events and categories, ownership checks (403), validation (400), not found (404), and public viewing.
- **RoleAccessTests**: every Organiser-only endpoint rejects Participants (403), every Participant-only endpoint rejects Organisers (403), and every protected endpoint rejects anonymous callers (401).
- **EnrolmentTests**: participants enrolling, with checks on the saved participant, event and category links; duplicate, full, past and missing-category cases; organisers blocked from enrolling; cancellations.
- **ResultTests**: organisers recording and updating results, participants viewing their own results, and the leaderboard order.


## CI
<img width="1090" height="702" alt="image" src="https://github.com/user-attachments/assets/188a025f-3eb9-4ee6-9513-72a49d167d1e" />

## YOUTUBE LINK
https://youtu.be/al-fq-1F6W8 
