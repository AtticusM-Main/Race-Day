# RaceDay API Endpoint Plan

This is the plan for the API before we start coding it.

## Role Definitions

- **None** = Public endpoint, no token needed
- **Any** = Any logged-in user
- **Organiser** or **Participant** = Only that role can use it

## Authentication

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/auth/register | Create a new account as either an Organiser or Participant. | None | `{ fullName, email, password, role }` | 201 Created with new user id and role. 400 Bad Request if data is invalid. |
| POST | /api/auth/login | Log in a user and get a JWT token. | None | `{ email, password }` | 200 OK with JWT token and role. 401 Unauthorized if credentials are wrong. |

## User Profile

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/users/me | Get the logged-in user's profile info. | Any | None | 200 OK with profile details. 401 Unauthorized. |
| PUT | /api/users/me | Update the logged-in user's profile. | Any | `{ fullName, email }` | 200 OK with updated profile. 400 Bad Request. 401 Unauthorized. |

## Events

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/events | List all events so people can see what races exist. | None | None | 200 OK with array of events. |
| GET | /api/events/{id} | Get details for one specific event. | None | None | 200 OK with event details. 404 Not Found. |
| POST | /api/events | Create a new event (logged-in organiser owns it). | Organiser | `{ name, description, eventDate, location }` | 201 Created with new event id. 400 Bad Request. 403 Forbidden. |
| PUT | /api/events/{id} | Update an event owned by the logged-in organiser. | Organiser | `{ name, description, eventDate, location }` | 200 OK with updated event. 403 Forbidden if not the owner. 404 Not Found. |
| DELETE | /api/events/{id} | Delete an event owned by the logged-in organiser. | Organiser | None | 204 No Content. 403 Forbidden. 404 Not Found. |

## Categories

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/events/{eventId}/categories | List all categories for an event. | None | None | 200 OK with array of categories. 404 Not Found if event doesn't exist. |
| POST | /api/events/{eventId}/categories | Add a new race category to an event. | Organiser | `{ name, distance, maxParticipants }` | 201 Created with new category id. 403 Forbidden. 404 Not Found. |
| PUT | /api/categories/{id} | Update a category. | Organiser | `{ name, distance, maxParticipants }` | 200 OK with updated category. 403 Forbidden. 404 Not Found. |
| DELETE | /api/categories/{id} | Delete a category. | Organiser | None | 204 No Content. 403 Forbidden. 404 Not Found. |

## Event Enrolments

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/enrolments | Sign up a logged-in participant for a category. | Participant | `{ categoryId }` | 201 Created with new enrolment id. 404 Not Found if category doesn't exist. 409 Conflict if already enrolled. |
| GET | /api/enrolments/me | Get a logged-in participant's own enrolments. | Participant | None | 200 OK with array of enrolments. |
| DELETE | /api/enrolments/{id} | Cancel a logged-in participant's enrolment. | Participant | None | 204 No Content. 403 Forbidden if not the participant. 404 Not Found. |
| GET | /api/events/{eventId}/enrolments | List all enrolments for an event (organiser oversight). | Organiser | None | 200 OK with array of enrolments and participant details. 404 Not Found. |

## Results

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/results | Record a participant's result for an enrolment. | Organiser | `{ enrolmentId, finishTime, position }` | 201 Created with new result id. 403 Forbidden. 404 Not Found. |
| PUT | /api/results/{id} | Update a recorded result. | Organiser | `{ finishTime, position }` | 200 OK with updated result. 403 Forbidden. 404 Not Found. |
| GET | /api/results/me | Get a logged-in participant's own results. | Participant | None | 200 OK with array of results and event/category details. |

## Additional Endpoints

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/categories/{id}/results | Get public leaderboard for a category sorted by finishing position. | None | None | 200 OK with ordered array of results. 404 Not Found. |



## Part 3 Additions

Added for the MVC web application.

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/events?search=&type=&from=&to=&upcomingOnly= | List events with optional filters (all optional, combinable). | None | None | 200 OK with array of events. |
| GET | /api/events/mine | List the events created by the logged-in organiser. | Organiser | None | 200 OK with array of events and category enrolment counts. |
| POST | /api/events/{id}/banner | Upload or replace the event banner image (stored in Azure Blob Storage). | Organiser | multipart/form-data, field `file` (JPG, PNG or WebP, max 5 MB) | 200 OK with updated event. 400 Bad Request for an invalid image. 403 Forbidden if not the owner. 404 Not Found. 503 Service Unavailable if cloud storage is down. |
| POST | /api/users/me/picture | Upload or replace the logged-in user's profile picture. | Any | multipart/form-data, field `file` | 200 OK with updated profile. 400 Bad Request. 503 Service Unavailable. |
| PUT | /api/enrolments/{id}/status | Confirm an enrolment or set it back to pending. | Organiser | `{ status }` (`Pending` or `Confirmed`) | 200 OK with updated enrolment. 403 Forbidden if not the event owner. 404 Not Found. |

Response changes: events include `bannerImageUrl`; profiles include `profilePictureUrl`; enrolments include `status`, and `resultId`, `finishTime` and `position` once a result exists; results include `totalFinishers` (how many participants have a result in the category).
