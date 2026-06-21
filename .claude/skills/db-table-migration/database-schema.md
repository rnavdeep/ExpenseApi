# Current Database Schema

Two SQL Server databases, each backed by its own EF Core DbContext.

---

## 1. `UserDocumentsDbContext` — application data

Connection: `ExpenseConnectionString`. Defined in `Data/UserDocumentsDbContext.cs`. Entities in `Models/Domain/`.

### Users (`User`)
| Column | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| Username | string | |
| Email | string | |
| CreatedAt | DateTime | default UtcNow |

### Expenses (`Expense`)
| Column | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| Title | string | |
| Description | string | |
| Amount | decimal | |
| CreatedAt | DateTime | default UtcNow |
| CreatedById | Guid | FK → User; `OnDelete(Restrict)` |

Navigation: `CreatedBy` (User), `Documents` (1‑to‑many), `ExpenseUsers` (many‑to‑many join).

### Documents (`Document`)
| Column | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| FileName | string | |
| FileExtension | string | |
| S3Url | string | location in AWS S3 |
| ETag | string | |
| VersionId | string | |
| Size | long | |
| UploadedAt | DateTime | default UtcNow |
| UserId | Guid | FK → User |
| ExpenseId | Guid | FK → Expense (one document → one expense) |

### ExpenseUsers (`ExpenseUser`) — join table (expense sharing)
| Column | Type | Notes |
|---|---|---|
| ExpenseId | Guid | **Composite PK** + FK → Expense |
| UserId | Guid | **Composite PK** + FK → User |
| UserShare | double | share value 0–1 |
| UserAmount | double? | share amount |

Composite key `{ ExpenseId, UserId }`; `Expense.ExpenseUsers` ↔ `Expense`, `User` via FK.

### DocumentJobResults (`DocumentJobResult`) — Textract OCR results
| Column | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| JobId | string | AWS Textract job id |
| Status | byte | 0 Pending, 1 Success, 2 Failed |
| CreatedAt | DateTime | default UtcNow |
| ResultCreatedAt | DateTime? | |
| CreatedById | Guid | FK → User |
| ExpenseId | Guid | FK → Expense |
| DocumentId | Guid | FK → Document |
| Total | decimal | extracted total, default 0.0 |
| ResultLineItems | string? | JSON line items |
| ColumnNames | string? | JSON column names |
| SummaryFields | string? | JSON summary fields |

### Notification (`Notification`)
| Column | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| CreatedAt | DateTime | default UtcNow |
| ReadAt | DateTime? | set when opened |
| IsRead | byte | 0 Unread, 1 Read |
| UserId | Guid | FK → User; `OnDelete(Cascade)` |
| Message | string | |
| Title | string? | |
| IsFriendRequest | byte? | 0 No, 1 Friend request |

### FriendRequests (`FriendRequest`)
| Column | Type | Notes |
|---|---|---|
| Id | Guid | PK |
| SentByUserId | Guid | FK → User |
| SentToUserId | Guid | FK → User |
| NotificationId | Guid | FK → Notification, **one‑to‑one**, unique index, required |
| CreatedAt | DateTime | default UtcNow |
| IsAccepted | byte | 0 Not accepted, 1 Accepted |
| AcceptedAt | DateTime? | |

### Relationship summary (`OnModelCreating`)
- `ExpenseUser`: composite key `{ExpenseId, UserId}`; FKs to `Expense` (with `ExpenseUsers` collection) and `User`.
- `Expense.CreatedBy` → `User`, FK `CreatedById`, `OnDelete(Restrict)`.
- `Notification.User` → `User`, FK `UserId`, `OnDelete(Cascade)`.
- `FriendRequest.Notification` → `Notification`, one‑to‑one, FK `NotificationId`, required, **unique index**.

```
User 1───* Expense          (CreatedBy / CreatedById, Restrict)
User 1───* Document
User 1───* Notification     (Cascade)
Expense 1──* Document
Expense *──* User           via ExpenseUser (UserShare, UserAmount)
Expense 1──* DocumentJobResult
Notification 1──1 FriendRequest   (unique)
User *──* User              via FriendRequest (SentBy / SentTo)
```

---

## 2. `ExpenseAuthDbContext` — authentication / identity

Connection: `ExpenseAuthConnectionString`. Defined in `Data/ExpenseAuthDbContext.cs`. Extends `IdentityDbContext` — standard ASP.NET Core Identity tables (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetRoleClaims`).

Seeded roles (`HasData`):
| Role | Id |
|---|---|
| Reader | 561855b0-fbc8-4064-ab99-17e2d85bc634 |
| Writer | ae4a84a0-55bd-4b85-a1c2-e9625173d4a4 |
| Admin | 31b735b9-1530-425c-a6d4-4d7e4efd783d |

> The app-side `User` table (in `UserDocumentsDbContext`) is distinct from Identity's `AspNetUsers`; they live in different databases and are correlated by Username/Email, not a DB foreign key.
