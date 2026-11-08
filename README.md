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

Part 1 (planning) produced the data model and the API contract in `docs/`. Part 2 implements that plan as an ASP.NET Core Web API. Part 3 adds an ASP.NET Core MVC web front end, Azure Blob Storage for images, Docker and CI/CD (see [Part 3](#part-3-the-mvc-web-application)).

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


## Part 3: The MVC web application

Part 3 builds the web front end for RaceDay. The MVC application (`src/RaceDay.Web`) never touches the database or Azure: every read and write is a call to the Part 2 REST API, which now also stores images in Azure Blob Storage.

```
Browser  -->  RaceDay.Web (MVC, session)  -->  RaceDay.Api (REST, JWT)  -->  SQL Server
                                                      |
                                                      +-->  Azure Blob Storage (banners, profile pictures)
```

### What was added

```
src/RaceDay.Web/                  ASP.NET Core MVC application (.NET 10, Bootstrap 5)
  Controllers/                    Account, Home, Events, Enrolments, Results, Profile (participant)
                                  Organiser, OrganiserEvents, OrganiserCategories,
                                  OrganiserEnrolments, OrganiserResults (organiser)
  Services/IRaceDayApi.cs         The only data source: a typed HttpClient wrapper around the API
  Services/UserSession.cs         Signed-in user, role and API token held in server-side session
  Filters/SessionAuthorize.cs     Redirects visitors to login and blocks the wrong role
  Models/                         API models and form/view models (validation attributes)
  Views/                          Razor views, one folder per controller, shared layout and partials
  wwwroot/css/site.css            RaceDay brand theme on top of Bootstrap
tests/RaceDay.Web.Tests/          55 tests of the MVC pipeline against a fake API
Dockerfile                        Container image for the MVC application
docker-compose.yml                SQL Server + API + web, started together
.github/workflows/ci.yml          Builds and tests API and MVC, builds both Docker images
```

API additions made for the web application (all covered by tests, 127 API tests in total):

| Change | Why |
|---|---|
| `Enrolment.Status` (`Pending` or `Confirmed`) and `PUT /api/enrolments/{id}/status` | Colour-coded enrolment statuses; organisers confirm entries |
| `Event.BannerImageUrl`, `User.ProfilePictureUrl`, `POST /api/events/{id}/banner`, `POST /api/users/me/picture` | Image uploads stored in Azure Blob Storage |
| `IImageStorage` / `AzureBlobImageStorage` | Uploads to a blob container and returns the public URL; validates JPG/PNG/WebP up to 5 MB |
| `ResultResponse.TotalFinishers` | "47th out of 312 finishers" on My Results |
| `GET /api/events/mine` and filters on `GET /api/events` (`search`, `type`, `from`, `to`, `upcomingOnly`) | Organiser dashboard and the participant filters, done in the API so the MVC app holds no business logic |
| Recorded result details on enrolments | Lets organisers see who still needs a result |

### Roles and navigation

Sessions: after login the MVC application stores the API token, role, user id and name in ASP.NET Core **server-side session**. The browser only receives the session cookie (HttpOnly). `SessionAuthorize` guards every protected controller: visitors without a session are redirected to the login page (and sent back to the page they wanted after logging in), and a user of the wrong role gets an "access denied" page. Logging out clears the session and returns to the login page. Sessions end when the API token expires (120 minutes).

| Participant | Organiser |
|---|---|
| **Events**: upcoming events with search, type and date filters | **Dashboard**: events created, enrolments per event, upcoming dates |
| **Event details**: full information, categories and places left, enter an event by choosing a category | **Manage Events**: create, edit, delete (name, description, date, location, distance, type) and upload a banner |
| **My Enrolments**: every entry with a status badge (green Confirmed, amber Pending) and cancel | **Categories**: add, edit and remove age or distance categories |
| **My Results**: event, date, category, finish time and position (for example "47th out of 312 finishers") | **Enrolments**: participants, categories and status per event; confirm entries |
| **Profile**: update name and email, upload a profile picture | **Enter results**: capture finish time and position after the event; saving publishes the result |
| | **Profile**: same as participants |

Invalid input is handled with client- and server-side validation. API errors (conflicts, not found, service down, expired session) are turned into friendly messages; raw exceptions are never shown.

### Running it locally (without Docker)

1. Start the API (see [Running it](#running-it)): `dotnet run --project src/RaceDay.Api`. It listens on `http://localhost:5259`.
2. In a second terminal start the web application: `dotnet run --project src/RaceDay.Web`. It listens on `http://localhost:5039` and reads the API address from `Api:BaseUrl` in `src/RaceDay.Web/appsettings.json`.
3. Open `http://localhost:5039`. Seeded accounts all use the password `Password123!`:
   - Organisers: `naledi.khumalo@raceday.co.za`, `johan.botha@raceday.co.za`
   - Participants: `thandiwe.nkosi@example.com`, `ryan.pillay@example.com`

### Azure Blob Storage

The API stores banners and profile pictures in a blob container (`raceday-images` by default). The web application only uploads the file to the API; it never talks to Azure.

1. In the Azure portal create a **Storage account** and copy its connection string (Access keys).
2. Give the API the connection string with .NET user secrets (keeps it out of git):

   ```
   dotnet user-secrets init --project src/RaceDay.Api
   dotnet user-secrets set "BlobStorage:ConnectionString" "<your connection string>" --project src/RaceDay.Api
   ```

   or set the environment variable `BlobStorage__ConnectionString`. `BlobStorage:ContainerName` is optional.
3. The container is created on first upload with public read access on blobs so that the stored URL can be shown in an `<img>` tag. If the storage account does not allow public access, the container is created private and images will not display until public blob access is enabled.

Without a connection string everything else works; uploads show "Image storage unavailable" instead of failing. For local testing without an Azure account you can use the [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) emulator with `BlobStorage:ConnectionString=UseDevelopmentStorage=true`.

### Docker

The MVC application runs in a container. It needs the API address in `Api__BaseUrl`.

**Start the web application with one command** (run it from the repository root; the API must be running on port 5259, for example with `dotnet run --project src/RaceDay.Api`):

```
docker build -t raceday-web . && docker run --rm -p 8080:8080 -e Api__BaseUrl=http://host.docker.internal:5259 raceday-web
```

Then open `http://localhost:8080`. On Linux add `--add-host=host.docker.internal:host-gateway` to the `docker run` command. To point the container at an API that is running somewhere else, change `Api__BaseUrl`.

The Dockerfile uses a multi-stage build (SDK image to build, the smaller ASP.NET runtime image to run), restores packages in their own cached layer, runs as the non-root `app` user, listens on port 8080, has a health check and keeps all settings in environment variables, so the container behaves the same as running locally.

**Run the whole platform (SQL Server, API and web) with Docker Compose:**

```
cp .env.example .env        # then edit .env (SQL password, JWT key, optional Azure connection string)
docker compose up --build
```

Web: `http://localhost:8080` &middot; API and Swagger: `http://localhost:5259/swagger`.

### Running the tests

```
dotnet test
```

- `RaceDay.Api.Tests` (127 tests): the API, including enrolment status, image uploads (against a fake image store), result totals and event filters.
- `RaceDay.Web.Tests` (55 tests): the MVC pipeline against a fake API: redirects for visitors, role-specific access, login/logout and session behaviour, antiforgery, registration validation, participant pages and friendly handling of an API outage.

### CI/CD

`.github/workflows/ci.yml` runs on every push and pull request: it restores, builds the API and the MVC project, runs all tests, then builds both Docker images and smoke-tests the MVC container.

### Part 3 video

YouTube (unlisted): _add the link here_

## CI
<img width="1090" height="702" alt="image" src="https://github.com/user-attachments/assets/188a025f-3eb9-4ee6-9513-72a49d167d1e" />

## YOUTUBE LINK
https://youtu.be/al-fq-1F6W8 
