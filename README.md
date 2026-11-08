# User Management API

ASP.NET Core minimal API for creating, retrieving, updating, and deleting user records. It uses an in-memory concurrent store, validates user names and email addresses, returns 404 for missing IDs, and includes exception handling, request logging, and bearer-token authentication.

## Run

Requires the .NET 8 SDK. Set a token in the environment and run:

```bash
API_TOKEN="replace-with-a-long-random-token" dotnet run --urls http://localhost:5000
```

On PowerShell, set `$env:API_TOKEN="replace-with-a-long-random-token"` before running `dotnet run --urls http://localhost:5000`.

All `/users` endpoints require `Authorization: Bearer <API_TOKEN>`. The root endpoint `/` is public. User data is in memory and resets when the process restarts; this sample is not configured for production use.

## Endpoints

| Method | Path | Result |
|---|---|---|
| GET | `/users` | List users |
| GET | `/users/{id}` | Get one user; 404 if missing |
| POST | `/users` | Create a user; validates name and email |
| PUT | `/users/{id}` | Replace a user's name and email; 404 if missing |
| DELETE | `/users/{id}` | Delete a user; 204 or 404 |

Example request body:

```json
{"name":"Taylor Smith","email":"taylor@example.com"}
```

## Testing with curl

Set `TOKEN` to the same value as `API_TOKEN` and use the correct base URL. These commands exercise create, list, retrieve, update, and delete. Copy the `id` returned by POST into `USER_ID` for the remaining requests.

```bash
export TOKEN="replace-with-a-long-random-token"
curl -i http://localhost:5000/users -H "Authorization: Bearer $TOKEN"
curl -i -X POST http://localhost:5000/users -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"name":"Taylor Smith","email":"taylor@example.com"}'
curl -i http://localhost:5000/users/$USER_ID -H "Authorization: Bearer $TOKEN"
curl -i -X PUT http://localhost:5000/users/$USER_ID -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"name":"Taylor Jones","email":"taylor.jones@example.com"}'
curl -i -X DELETE http://localhost:5000/users/$USER_ID -H "Authorization: Bearer $TOKEN"
```

Requests without a valid bearer token return 401. Invalid user input returns 400 with validation details. Unhandled exceptions return a consistent JSON 500 response and are logged.

## Copilot assistance

Copilot was used to scaffold the minimal API and CRUD route patterns, suggest data annotations for validating input, and help structure reusable middleware for exception handling, bearer-token checks, and request/response status logging. The generated suggestions were reviewed and adapted to return appropriate HTTP statuses for missing resources and invalid input, avoid exposing exception details in responses, and use a thread-safe in-memory store.
